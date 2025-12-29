import React, { useEffect, useState, useRef } from 'react';
import './App.css';
import UploadForm from './components/UploadForm';
import LoginForm from './components/LoginForm';
import AdminPanel from './components/AdminPanel';
import Spinner from './components/Spinner';
import Toast from './components/Toast';
import api, { setAuthToken, getStoredToken } from './services/api';

function App() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [authError, setAuthError] = useState(null);
  const [token, setToken] = useState(null);
  const [userInfo, setUserInfo] = useState(null);
  const [currentView, setCurrentView] = useState('main'); // 'main' or 'admin'
  const loadingUserInfoRef = useRef(false);

  const isAuthenticated = Boolean(token);
  const isAdmin = userInfo?.role === 'Admin';

  useEffect(() => {
    const existing = getStoredToken();
    if (existing) {
      setAuthToken(existing);
      setToken(existing);
    }
  }, []);

  useEffect(() => {
    // Handle GitHub OAuth redirect with tokens in hash
    const hash = window.location.hash?.slice(1);
    if (hash) {
      const params = new URLSearchParams(hash);
      const access = params.get('accessToken');
      const refresh = params.get('refreshToken');
      const username = params.get('username');
      const role = params.get('role');
      if (access) {
        setAuthToken(access);
        setToken(access);
        setUserInfo({ username: username || 'github_user', role: role || 'User' });
        // Clean URL
        window.history.replaceState({}, document.title, window.location.pathname + window.location.search);
      }
      if (refresh) {
        // Optionally store refresh if needed later; currently unused.
      }
    }
  }, []);

  useEffect(() => {
    // Load user info when token exists but userInfo is missing
    const loadUserInfo = async () => {
      if (token && !userInfo && !loadingUserInfoRef.current) {
        loadingUserInfoRef.current = true;
        try {
          const res = await api.get('/auth/me');
          setUserInfo({ username: res.data.username, role: res.data.role });
        } catch (err) {
          // If token is invalid, clear it
          if (err.response?.status === 401) {
            setAuthToken(null);
            setToken(null);
          }
        } finally {
          loadingUserInfoRef.current = false;
        }
      }
    };

    loadUserInfo();
  }, [token, userInfo]);

  const handleUpload = async (success, msg) => {
    if (success) {
      setMessage(msg);
      setTimeout(() => setMessage(null), 5000);
    }
  };

  return (
    <div className="App">
      <header className="header">
        <h1>Transaction Manager</h1>
        <p>Upload and manage financial transactions</p>
        {isAuthenticated && (
          <nav className="main-nav">
            <button
              className={`nav-btn ${currentView === 'main' ? 'active' : ''}`}
              onClick={() => setCurrentView('main')}
            >
              Main
            </button>
            <button
              className={`nav-btn ${currentView === 'admin' ? 'active' : ''}`}
              onClick={() => setCurrentView('admin')}
            >
              Administration
            </button>
          </nav>
        )}
      </header>

      <main className="container">
        <LoginForm
          onLogin={async (credentials) => {
            try {
              setAuthError(null);
              const res = await api.post('/auth/login', credentials);
              setAuthToken(res.data.accessToken);
              setToken(res.data.accessToken);
              setUserInfo({ username: res.data.username, role: res.data.role });
            } catch (err) {
              setAuthError(err.response?.data?.error || 'Falha ao autenticar');
              setToken(null);
            }
          }}
          onLogout={() => {
            setAuthToken(null);
            setToken(null);
            setUserInfo(null);
          }}
          onGitHub={() => {
            const apiBase = process.env.REACT_APP_API_URL || 'http://localhost:5000/api/v1';
            const state = encodeURIComponent(window.location.origin);
            window.location.href = `${apiBase}/auth/github/login?redirectUri=${state}`;
            // Note: apiBase should end with /api/v1 or include /v1 in REACT_APP_API_URL
          }}
          isAuthenticated={isAuthenticated}
          userInfo={userInfo}
          error={authError}
        />

        {error && <div className="alert alert-error">{error}</div>}
        {message && <div className="alert alert-success">{message}</div>}

        <Toast
          message={error}
          type="error"
          onClose={() => setError(null)}
        />
        <Toast
          message={message}
          type="success"
          onClose={() => setMessage(null)}
        />

        <div className="content">
          {!isAuthenticated ? (
            <div className="locked-card">
              <h2>Authentication Required</h2>
              <p>Please log in to upload files and search transactions.</p>
            </div>
          ) : currentView === 'admin' ? (
            <AdminPanel userInfo={userInfo} />
          ) : (
            <>
              <section className="upload-section">
                <UploadForm onUpload={handleUpload} isAuthenticated={isAuthenticated} />
              </section>

              <section className="data-section">
                <div className="empty-state">
                  <p>Upload a CNAB file and go to Administration to view transactions grouped by store</p>
                </div>
              </section>
            </>
          )}
        </div>
      </main>

      <footer className="footer">
        <p>&copy; 2025 Transaction Manager. All rights reserved.</p>
      </footer>
    </div>
  );
}

export default App;
