import React, { useEffect, useState } from 'react';
import './App.css';
import UploadForm from './components/UploadForm';
import TransactionList from './components/TransactionList';
import LoginForm from './components/LoginForm';
import api, { setAuthToken, getStoredToken } from './services/api';

function App() {
  const [cpf, setCpf] = useState('');
  const [transactions, setTransactions] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [balance, setBalance] = useState(null);
  const [loading, setLoading] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [searched, setSearched] = useState(false);
  const [authError, setAuthError] = useState(null);
  const [token, setToken] = useState(null);
  const [userInfo, setUserInfo] = useState(null);

  const isAuthenticated = Boolean(token);

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

  const loadTransactions = async (searchCpf, targetPage = 1) => {
    if (!isAuthenticated) {
      setError('Please log in to search.');
      return;
    }
    if (!searchCpf || searchCpf.trim() === '') {
      setError('Please enter a CPF/CNPJ');
      return;
    }

    try {
      const isInitialLoad = targetPage === 1;
      if (isInitialLoad) {
        setLoading(true);
      } else {
        setIsLoadingMore(true);
      }
      setError(null);

      const [transRes, balanceRes] = await Promise.all([
        api.get(`/transactions/${searchCpf}`, {
          params: {
            page: targetPage,
            pageSize,
          },
        }),
        targetPage === 1 ? api.get(`/transactions/${searchCpf}/balance`) : Promise.resolve({ data: { balance: null } }),
      ]);

      const paged = transRes.data || {};
      const newItems = paged.items || [];

      // On initial load, replace; on pagination, append
      if (isInitialLoad) {
        setTransactions(newItems);
        setBalance(balanceRes.data.balance);
      } else {
        setTransactions((prev) => [...prev, ...newItems]);
      }

      setTotalCount(paged.totalCount || 0);
      setPage(targetPage);
      setSearched(true);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to load transactions');
      if (targetPage === 1) {
        setTransactions([]);
        setTotalCount(0);
      }
      setBalance(null);
    } finally {
      setLoading(false);
      setIsLoadingMore(false);
    }
  };

  const handleUpload = async (success, msg) => {
    if (success) {
      setMessage(msg);
      setTimeout(() => setMessage(null), 5000);
      // Clear previous search after upload
      setTransactions([]);
      setTotalCount(0);
      setBalance(null);
      setSearched(false);
      setCpf('');
      setPage(1);
    }
  };

  const handleSearch = (e) => {
    e.preventDefault();
    setPage(1);
    loadTransactions(cpf, 1);
  };

  const handlePageChange = (newPage) => {
    if (newPage < 1) return;
    loadTransactions(cpf, newPage);
  };

  const handleClear = async () => {
    if (!isAuthenticated) {
      setError('Please log in to clear data.');
      return;
    }
    if (window.confirm('Are you sure you want to delete all data?')) {
      try {
        await api.delete('/transactions');
        setMessage('All data cleared successfully');
        setTimeout(() => setMessage(null), 5000);
        setTransactions([]);
        setBalance(null);
        setSearched(false);
        setCpf('');
      } catch (err) {
        setError(err.response?.data?.error || 'Failed to clear data');
      }
    }
  };

  return (
    <div className="App">
      <header className="header">
        <h1>Transaction Manager</h1>
        <p>Upload and manage financial transactions</p>
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
            setTransactions([]);
            setBalance(null);
            setSearched(false);
          }}
          onGitHub={() => {
            const apiBase = process.env.REACT_APP_API_URL || 'http://localhost:5000/api';
            const state = encodeURIComponent(window.location.origin);
            window.location.href = `${apiBase}/auth/github/login?redirectUri=${state}`;
          }}
          isAuthenticated={isAuthenticated}
          userInfo={userInfo}
          error={authError}
        />

        {error && <div className="alert alert-error">{error}</div>}
        {message && <div className="alert alert-success">{message}</div>}

        <div className="content">
          {!isAuthenticated ? (
            <div className="locked-card">
              <h2>Authentication Required</h2>
              <p>Please log in to upload files and search transactions.</p>
            </div>
          ) : (
            <>
              <section className="upload-section">
                <UploadForm onUpload={handleUpload} isAuthenticated={isAuthenticated} />
                <button className="btn btn-danger" onClick={handleClear}>
                  Clear All Data
                </button>
              </section>

              <section className="search-section">
                <form onSubmit={handleSearch} className="search-form">
                  <div className="search-input-group">
                    <input
                      type="text"
                      placeholder="Enter CPF/CNPJ (11 digits)"
                      value={cpf}
                      onChange={(e) => setCpf(e.target.value)}
                      className="search-input"
                      maxLength="11"
                    />
                    <button type="submit" className="btn btn-primary" disabled={loading || !isAuthenticated}>
                      {loading ? 'Searching...' : 'Search Transactions'}
                    </button>
                  </div>
                </form>
              </section>

              <section className="data-section">
                {loading ? (
                  <div className="loading">Loading data...</div>
                ) : searched ? (
                  <>
                    {balance !== null && (
                      <div className="balance-card">
                        <h2>Balance for CPF: {cpf}</h2>
                        <p className={`balance-value ${balance >= 0 ? 'positive' : 'negative'}`}>
                          {new Intl.NumberFormat('pt-BR', {
                            style: 'currency',
                            currency: 'BRL',
                          }).format(balance)}
                        </p>
                      </div>
                    )}

                    <TransactionList
                      transactions={transactions}
                      cpf={cpf}
                      totalCount={totalCount}
                      pageSize={pageSize}
                      onPageChange={handlePageChange}
                      isLoadingMore={isLoadingMore}
                    />
                  </>
                ) : (
                  <div className="empty-state">
                    <p>Upload a CNAB file and search by CPF/CNPJ to view transactions</p>
                  </div>
                )}
              </section>
            </>
          )}
        </div>
      </main>

      <footer className="footer">
        <p>&copy; 2024 Transaction Manager. All rights reserved.</p>
      </footer>
    </div>
  );
}

export default App;
