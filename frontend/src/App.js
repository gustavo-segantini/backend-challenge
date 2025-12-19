import React, { useState } from 'react';
import './App.css';
import UploadForm from './components/UploadForm';
import TransactionList from './components/TransactionList';
import api from './services/api';

function App() {
  const [cpf, setCpf] = useState('');
  const [transactions, setTransactions] = useState([]);
  const [balance, setBalance] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [searched, setSearched] = useState(false);

  const loadTransactions = async (searchCpf) => {
    if (!searchCpf || searchCpf.trim() === '') {
      setError('Please enter a CPF/CNPJ');
      return;
    }

    try {
      setLoading(true);
      setError(null);
      
      const [transRes, balanceRes] = await Promise.all([
        api.get(`/transactions/${searchCpf}`),
        api.get(`/transactions/${searchCpf}/balance`),
      ]);

      setTransactions(transRes.data || []);
      setBalance(balanceRes.data.balance);
      setSearched(true);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to load transactions');
      setTransactions([]);
      setBalance(null);
    } finally {
      setLoading(false);
    }
  };

  const handleUpload = async (success, msg) => {
    if (success) {
      setMessage(msg);
      setTimeout(() => setMessage(null), 5000);
      // Clear previous search after upload
      setTransactions([]);
      setBalance(null);
      setSearched(false);
      setCpf('');
    }
  };

  const handleSearch = (e) => {
    e.preventDefault();
    loadTransactions(cpf);
  };

  const handleClear = async () => {
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
        <h1>CNAB Transaction Manager</h1>
        <p>Upload and manage financial transactions</p>
      </header>

      <main className="container">
        {error && <div className="alert alert-error">{error}</div>}
        {message && <div className="alert alert-success">{message}</div>}

        <div className="content">
          <section className="upload-section">
            <UploadForm onUpload={handleUpload} />
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
                <button type="submit" className="btn btn-primary" disabled={loading}>
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

                <TransactionList transactions={transactions} cpf={cpf} />
              </>
            ) : (
              <div className="empty-state">
                <p>Upload a CNAB file and search by CPF/CNPJ to view transactions</p>
              </div>
            )}
          </section>
        </div>
      </main>

      <footer className="footer">
        <p>&copy; 2024 CNAB Transaction Manager. All rights reserved.</p>
      </footer>
    </div>
  );
}

export default App;
