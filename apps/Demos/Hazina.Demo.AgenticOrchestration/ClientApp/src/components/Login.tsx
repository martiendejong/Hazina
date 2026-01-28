import { useState } from 'react';
import { setCredentials, authFetch } from '../auth';

interface LoginProps {
  onLogin: () => void;
}

export function Login({ onLogin }: LoginProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    // Store credentials temporarily
    setCredentials(username, password);

    try {
      // Try to access a protected endpoint to validate credentials
      const response = await authFetch('/api/terminal/config');

      if (response.ok) {
        // Credentials are valid
        onLogin();
      } else if (response.status === 401) {
        setError('Invalid username or password');
        // Clear invalid credentials
        sessionStorage.removeItem('hazina_auth');
      } else {
        setError(`Authentication failed: ${response.statusText}`);
        sessionStorage.removeItem('hazina_auth');
      }
    } catch (err) {
      setError('Failed to connect to server');
      sessionStorage.removeItem('hazina_auth');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-box">
        <h1>🤖 Claude Terminal Orchestration</h1>
        <p className="login-subtitle">Please sign in to continue</p>

        <form onSubmit={handleSubmit}>
          {error && <div className="login-error">{error}</div>}

          <div className="form-group">
            <label htmlFor="username">Username</label>
            <input
              id="username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Enter username"
              required
              autoFocus
              disabled={loading}
            />
          </div>

          <div className="form-group">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Enter password"
              required
              disabled={loading}
            />
          </div>

          <button type="submit" className="btn-login" disabled={loading}>
            {loading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  );
}
