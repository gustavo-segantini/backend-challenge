import React from 'react';
import './TransactionList.css';

function TransactionList({ transactions }) {
  const getTypeColor = (type) => {
    const colors = {
      1: '#3498db', // Debit - Blue
      2: '#e74c3c', // Boleto - Red
      3: '#e74c3c', // Financing - Red
      4: '#27ae60', // Credit - Green
      5: '#27ae60', // Loan Receipt - Green
      6: '#27ae60', // Sales - Green
      7: '#27ae60', // TED Receipt - Green
      8: '#27ae60', // DOC Receipt - Green
      9: '#e74c3c', // Rent - Red
    };
    return colors[type] || '#95a5a6';
  };

  return (
    <div className="transaction-list-container">
      <h2>Recent Transactions</h2>
      {transactions.length === 0 ? (
        <p className="empty-message">No transactions found. Upload a CNAB file first.</p>
      ) : (
        <div className="transaction-list">
          {transactions.slice(0, 20).map((transaction, index) => (
            <div key={index} className="transaction-item">
              <div className="transaction-main">
                <div className="transaction-header">
                  <span
                    className="transaction-type-badge"
                    style={{ backgroundColor: getTypeColor(transaction.type) }}
                  >
                    {transaction.type}
                  </span>
                  <span className="transaction-description">
                    {transaction.transactionDescription}
                  </span>
                  <span className="transaction-date">
                    {new Date(transaction.date).toLocaleDateString('pt-BR')}
                  </span>
                </div>
                <div className="transaction-details">
                  <p className="store-info">
                    <strong>{transaction.storeName}</strong> - {transaction.storeOwner}
                  </p>
                  <p className="transaction-meta">
                    CPF: {transaction.cpf} | Card: {transaction.card}
                  </p>
                </div>
              </div>
              <div className="transaction-amount-container">
                <span
                  className={`transaction-amount ${
                    transaction.signedAmount >= 0 ? 'positive' : 'negative'
                  }`}
                >
                  {transaction.signedAmount >= 0 ? '+' : ''}
                  {new Intl.NumberFormat('pt-BR', {
                    style: 'currency',
                    currency: 'BRL',
                  }).format(transaction.signedAmount)}
                </span>
              </div>
            </div>
          ))}
          {transactions.length > 20 && (
            <p className="more-transactions">
              ... and {transactions.length - 20} more transactions
            </p>
          )}
        </div>
      )}
    </div>
  );
}

export default TransactionList;
