/**
 * Terminal Client for Hazina.AgenticOrchestration
 *
 * This TypeScript module provides a client for connecting to the terminal hub
 * and rendering output using xterm.js.
 *
 * Usage:
 *   npm install xterm @xterm/addon-fit @xterm/addon-web-links @microsoft/signalr
 *
 *   import { ClaudeTerminalClient } from './terminal-client';
 *
 *   const client = new ClaudeTerminalClient({
 *     hubUrl: '/hubs/terminal',
 *     containerId: 'terminal-container',
 *   });
 *
 *   await client.connect();
 *   const sessionId = await client.startSession({ command: 'claude', workingDirectory: 'C:/Projects' });
 */

import { Terminal } from 'xterm';
import { FitAddon } from '@xterm/addon-fit';
import { WebLinksAddon } from '@xterm/addon-web-links';
import * as signalR from '@microsoft/signalr';

export interface TerminalClientConfig {
    /** SignalR hub URL (default: /hubs/terminal) */
    hubUrl?: string;
    /** HTML container element ID for the terminal */
    containerId: string;
    /** Terminal theme */
    theme?: {
        background?: string;
        foreground?: string;
        cursor?: string;
        selection?: string;
    };
    /** Font settings */
    fontFamily?: string;
    fontSize?: number;
    /** Called when session exits */
    onExit?: (sessionId: string, exitCode: number) => void;
    /** Called on connection error */
    onError?: (error: Error) => void;
}

export interface StartSessionOptions {
    command?: string;
    arguments?: string[];
    workingDirectory?: string;
    columns?: number;
    rows?: number;
    mergeStderr?: boolean;
    environment?: Record<string, string>;
}

export interface SessionInfo {
    sessionId: string;
    command: string;
    startedAt: Date;
    isRunning: boolean;
    exitCode?: number;
}

export class ClaudeTerminalClient {
    private terminal: Terminal;
    private fitAddon: FitAddon;
    private connection: signalR.HubConnection;
    private config: TerminalClientConfig;
    private currentSessionId: string | null = null;
    private isConnected: boolean = false;

    constructor(config: TerminalClientConfig) {
        this.config = {
            hubUrl: '/hubs/terminal',
            fontFamily: 'Cascadia Code, Consolas, Monaco, monospace',
            fontSize: 14,
            theme: {
                background: '#1e1e1e',
                foreground: '#d4d4d4',
                cursor: '#ffffff',
                selection: '#264f78',
            },
            ...config,
        };

        // Initialize terminal
        this.terminal = new Terminal({
            cursorBlink: true,
            fontSize: this.config.fontSize,
            fontFamily: this.config.fontFamily,
            theme: this.config.theme,
            scrollback: 10000,
            convertEol: true,
        });

        // Add fit addon for automatic resizing
        this.fitAddon = new FitAddon();
        this.terminal.loadAddon(this.fitAddon);

        // Add web links addon for clickable URLs
        this.terminal.loadAddon(new WebLinksAddon());

        // Initialize SignalR connection
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(this.config.hubUrl!)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.setupEventHandlers();
    }

    private setupEventHandlers(): void {
        // Receive output from the process
        this.connection.on('OnOutput', (sessionId: string, data: Uint8Array) => {
            if (sessionId === this.currentSessionId) {
                // Write raw bytes to terminal - preserves ANSI escape codes
                this.terminal.write(data);
            }
        });

        // Process exited
        this.connection.on('OnExit', (sessionId: string, exitCode: number) => {
            if (sessionId === this.currentSessionId) {
                this.terminal.writeln(`\r\n\x1b[90m[Process exited with code ${exitCode}]\x1b[0m`);
                this.config.onExit?.(sessionId, exitCode);
            }
        });

        // Error handling
        this.connection.on('OnError', (sessionId: string, error: string) => {
            console.error(`Terminal error for ${sessionId}:`, error);
            this.terminal.writeln(`\r\n\x1b[31m[Error: ${error}]\x1b[0m`);
        });

        // New session created (optional notification)
        this.connection.on('OnSessionCreated', (info: SessionInfo) => {
            console.log('Session created:', info);
        });

        // Handle user input - forward keystrokes to the process
        this.terminal.onData((data: string) => {
            if (this.currentSessionId && this.isConnected) {
                // Convert string to bytes and send
                const encoder = new TextEncoder();
                const bytes = encoder.encode(data);
                this.connection.invoke('SendInput', this.currentSessionId, Array.from(bytes))
                    .catch(err => console.error('Failed to send input:', err));
            }
        });

        // Handle terminal resize
        this.terminal.onResize(({ cols, rows }) => {
            if (this.currentSessionId && this.isConnected) {
                this.connection.invoke('Resize', this.currentSessionId, cols, rows)
                    .catch(err => console.error('Failed to send resize:', err));
            }
        });

        // Connection state changes
        this.connection.onreconnecting(err => {
            console.log('Reconnecting...', err);
            this.terminal.writeln('\r\n\x1b[33m[Reconnecting...]\x1b[0m');
        });

        this.connection.onreconnected(connectionId => {
            console.log('Reconnected:', connectionId);
            this.terminal.writeln('\r\n\x1b[32m[Reconnected]\x1b[0m');
            // Rejoin session after reconnect
            if (this.currentSessionId) {
                this.connection.invoke('JoinSession', this.currentSessionId);
            }
        });

        this.connection.onclose(err => {
            this.isConnected = false;
            console.log('Connection closed:', err);
            this.terminal.writeln('\r\n\x1b[31m[Disconnected]\x1b[0m');
        });
    }

    /**
     * Connect to the SignalR hub
     */
    async connect(): Promise<void> {
        // Open terminal in container
        const container = document.getElementById(this.config.containerId);
        if (!container) {
            throw new Error(`Container element '${this.config.containerId}' not found`);
        }

        this.terminal.open(container);
        this.fitAddon.fit();

        // Handle window resize
        window.addEventListener('resize', () => {
            this.fitAddon.fit();
        });

        // Connect to SignalR
        try {
            await this.connection.start();
            this.isConnected = true;
            console.log('Connected to terminal hub');
        } catch (err) {
            this.config.onError?.(err as Error);
            throw err;
        }
    }

    /**
     * Start a new terminal session
     */
    async startSession(options: StartSessionOptions = {}): Promise<string> {
        if (!this.isConnected) {
            throw new Error('Not connected to hub');
        }

        const request = {
            command: options.command || 'claude',
            arguments: options.arguments || [],
            workingDirectory: options.workingDirectory,
            columns: options.columns || this.terminal.cols,
            rows: options.rows || this.terminal.rows,
            mergeStderr: options.mergeStderr ?? true,
            environment: options.environment,
        };

        const sessionInfo = await this.connection.invoke<SessionInfo>('StartSession', request);
        this.currentSessionId = sessionInfo.sessionId;

        this.terminal.clear();
        this.terminal.writeln(`\x1b[90m[Session ${sessionInfo.sessionId} started]\x1b[0m`);
        this.terminal.writeln(`\x1b[90m[Running: ${sessionInfo.command}]\x1b[0m\r\n`);

        return sessionInfo.sessionId;
    }

    /**
     * Join an existing session
     */
    async joinSession(sessionId: string): Promise<boolean> {
        if (!this.isConnected) {
            throw new Error('Not connected to hub');
        }

        const success = await this.connection.invoke<boolean>('JoinSession', sessionId);
        if (success) {
            this.currentSessionId = sessionId;
            this.terminal.writeln(`\x1b[90m[Joined session ${sessionId}]\x1b[0m\r\n`);
        }
        return success;
    }

    /**
     * Send Ctrl+C to the process
     */
    async sendInterrupt(): Promise<void> {
        if (this.currentSessionId && this.isConnected) {
            await this.connection.invoke('SendSignal', this.currentSessionId, 'interrupt');
        }
    }

    /**
     * Terminate the current session
     */
    async terminate(): Promise<void> {
        if (this.currentSessionId && this.isConnected) {
            await this.connection.invoke('Terminate', this.currentSessionId);
            this.currentSessionId = null;
        }
    }

    /**
     * Get all active sessions
     */
    async getSessions(): Promise<SessionInfo[]> {
        if (!this.isConnected) {
            throw new Error('Not connected to hub');
        }
        return await this.connection.invoke<SessionInfo[]>('GetSessions');
    }

    /**
     * Write text directly to the terminal (local echo, not sent to process)
     */
    write(text: string): void {
        this.terminal.write(text);
    }

    /**
     * Get the current session ID
     */
    get sessionId(): string | null {
        return this.currentSessionId;
    }

    /**
     * Disconnect and cleanup
     */
    async dispose(): Promise<void> {
        if (this.currentSessionId) {
            await this.terminate();
        }
        await this.connection.stop();
        this.terminal.dispose();
    }
}

// Export for vanilla JS usage
(window as any).ClaudeTerminalClient = ClaudeTerminalClient;
