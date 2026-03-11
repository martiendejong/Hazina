/**
 * Basic Authentication utilities for the Hazina Agentic Orchestration frontend.
 * Stores credentials in sessionStorage and provides helpers for authenticated requests.
 */

const AUTH_KEY = 'hazina_auth';

export interface AuthCredentials {
  username: string;
  password: string;
}

/**
 * Get the Basic auth header value
 */
export function getAuthHeader(): string | null {
  const credentials = getCredentials();
  if (!credentials) return null;
  const encoded = btoa(`${credentials.username}:${credentials.password}`);
  return `Basic ${encoded}`;
}

/**
 * Store credentials in session storage
 */
export function setCredentials(username: string, password: string): void {
  sessionStorage.setItem(AUTH_KEY, JSON.stringify({ username, password }));
}

/**
 * Get stored credentials
 */
export function getCredentials(): AuthCredentials | null {
  const stored = sessionStorage.getItem(AUTH_KEY);
  if (!stored) return null;
  try {
    return JSON.parse(stored);
  } catch {
    return null;
  }
}

/**
 * Clear stored credentials
 */
export function clearCredentials(): void {
  sessionStorage.removeItem(AUTH_KEY);
}

/**
 * Check if we have stored credentials
 */
export function hasCredentials(): boolean {
  return getCredentials() !== null;
}

/**
 * Create fetch options with authentication header
 */
export function authFetch(url: string, options: RequestInit = {}): Promise<Response> {
  const authHeader = getAuthHeader();
  const headers = new Headers(options.headers);

  if (authHeader) {
    headers.set('Authorization', authHeader);
  }

  return fetch(url, {
    ...options,
    headers
  });
}

/**
 * Get SignalR connection options with auth
 */
export function getSignalRAuthOptions(): { accessTokenFactory?: () => string } {
  const authHeader = getAuthHeader();
  if (!authHeader) return {};

  // SignalR can use query string for auth in some cases
  // But for basic auth, we'll handle it differently via withCredentials
  return {};
}
