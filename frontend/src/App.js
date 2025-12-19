import React, { useState, useEffect } from 'react';
import './App.css';
import UploadForm from './components/UploadForm';
import TransactionList from './components/TransactionList';
import StoreList from './components/StoreList';
import api from './services/api';

function App() {
  const [transactions, setTransactions] = useState([]);
  const [stores, setStores] = useState([]);
  const [totalBalance, setTotalBalance] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [transRes, storesRes, balanceRes] = await Promise.all([
        api.get('/transactions'),
        api.get('/transactions/stores'),
        api.get('/transactions/balance'),
      ]);

      setTransactions(transRes.data);
      setStores(storesRes.data);
      setTotalBalance(balanceRes.data.totalBalance);
      setError(null);
    } catch (err) {
      setError(err.message || 'Failed to load data');
    } finally {
      setLoading(false);
    }
  };

  const handleUpload = async (success, msg) => {
    if (success) {
      setMessage(msg);
      setTimeout(() => setMessage(null), 5000);
      await loadData();
    }
  };

  const handleClear = async () => {
    if (window.confirm('Are you sure you want to delete all data?')) {
      try {
        await api.delete('/transactions');
        setMessage('All data cleared');
        setTimeout(() => setMessage(null), 5000);
        await loadData();
      } catch (err) {
        setError(err.message || 'Failed to clear data');
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

          <section className="data-section">
            {loading ? (
              <div className="loading">Loading data...</div>
            ) : (
              <>
                <div className="balance-card">
                  <h2>Total Balance</h2>
                  <p className={`balance-value ${totalBalance >= 0 ? 'positive' : 'negative'}`}>
                    {new Intl.NumberFormat('pt-BR', {
                      style: 'currency',
                      currency: 'BRL',
                    }).format(totalBalance)}
                  </p>
                </div>

                <div className="two-column">
                  <StoreList stores={stores} />
                  <TransactionList transactions={transactions} />
                </div>
              </>
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
