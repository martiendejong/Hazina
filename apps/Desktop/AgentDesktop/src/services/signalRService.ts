import * as signalR from '@microsoft/signalr';
import { EventStoreEntry } from '../types/events';

export type EventCallback = (event: EventStoreEntry) => void;
export type ConnectionCallback = (connected: boolean) => void;

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private eventCallbacks: EventCallback[] = [];
  private connectionCallbacks: ConnectionCallback[] = [];

  async connect(baseURL: string = 'http://localhost:5000'): Promise<void> {
    if (this.connection) {
      await this.disconnect();
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseURL}/hubs/terminal`)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 2s, 4s, 8s, 16s, max 30s
          return Math.min(2000 * Math.pow(2, retryContext.previousRetryCount), 30000);
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Event handlers
    this.connection.on('EventReceived', (event: EventStoreEntry) => {
      this.eventCallbacks.forEach((callback) => callback(event));
    });

    // Connection state handlers
    this.connection.onreconnecting(() => {
      console.log('SignalR reconnecting...');
      this.notifyConnectionState(false);
    });

    this.connection.onreconnected(() => {
      console.log('SignalR reconnected');
      this.notifyConnectionState(true);
    });

    this.connection.onclose(() => {
      console.log('SignalR connection closed');
      this.notifyConnectionState(false);
    });

    try {
      await this.connection.start();
      console.log('SignalR connected');
      this.notifyConnectionState(true);
    } catch (error) {
      console.error('SignalR connection error:', error);
      this.notifyConnectionState(false);
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch (error) {
        console.error('Error stopping SignalR connection:', error);
      }
      this.connection = null;
      this.notifyConnectionState(false);
    }
  }

  onEvent(callback: EventCallback): () => void {
    this.eventCallbacks.push(callback);
    return () => {
      this.eventCallbacks = this.eventCallbacks.filter((cb) => cb !== callback);
    };
  }

  onConnectionChange(callback: ConnectionCallback): () => void {
    this.connectionCallbacks.push(callback);
    // Immediately notify current state
    if (this.connection) {
      callback(this.connection.state === signalR.HubConnectionState.Connected);
    }
    return () => {
      this.connectionCallbacks = this.connectionCallbacks.filter((cb) => cb !== callback);
    };
  }

  private notifyConnectionState(connected: boolean): void {
    this.connectionCallbacks.forEach((callback) => callback(connected));
  }

  getConnectionState(): signalR.HubConnectionState | null {
    return this.connection?.state ?? null;
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

export const signalRService = new SignalRService();
export default signalRService;
