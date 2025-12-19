import React from 'react';
import './TransactionList.css';

function TransactionList({ transactions, cpf }) {
  const transactionTypes = {
    '1': { name: 'Debit', nature: 'Income', sign: '+' },
    '2': { name: 'Boleto', nature: 'Expense', sign: '-' },
    '3': { name: 'Financing', nature: 'Expense', sign: '-' },
    '4': { name: 'Credit', nature: 'Income', sign: '+' },
    '5': { name: 'Loan Receipt', nature: 'Income', sign: '+' },
    '6': { name: 'Sales', nature: 'Income', sign: '+' },
    '7': { name: 'TED Receipt', nature: 'Income', sign: '+' },
    '8': { name: 'DOC Receipt', nature: 'Income', sign: '+' },
    '9': { name: 'Rent', nature: 'Expense', sign: '-' },
  };

  const getTypeColor = (natureCode) => {
    const type = transactionTypes[natureCode];
    return type?.nature === 'Income' ? '#27ae60' : '#e74c3c';
  };

  const getSignedAmount = (transaction) => {
    const type = transactionTypes[transaction.natureCode];
    const multiplier = type?.sign === '+' ? 1 : -1;
    return transaction.amount * multiplier;
  };

  const formatDateTime = (date, time) => {
    const dateObj = new Date(date);
    const dateStr = dateObj.toLocaleDateString('pt-BR');
    const timeStr = time ? time.substring(0, 8) : '00:00:00';
    return `${dateStr} ${timeStr}`;
  };

  return (
    <div className="transaction-list-container">
      <h2>Transactions {cpf && `for CPF: ${cpf}`}</h2>
      {transactions.length === 0 ? (
        <p className="empty-message">No transactions found for this CPF.</p>
      ) : (
        <>
          <p className="transaction-count">Showing {transactions.length} transaction(s)</p>
          <div className="transaction-list">
            {transactions.map((transaction) => {
              const typeInfo = transactionTypes[transaction.natureCode] || { name: 'Unknown', nature: 'Unknown', sign: '+' };
              const signedAmount = getSignedAmount(transaction);
              
              return (
                <div key={transaction.id} className="transaction-item">
                  <div className="transaction-main">
                    <div className="transaction-header">
                      <span
                        className="transaction-type-badge"
                        style={{ backgroundColor: getTypeColor(transaction.natureCode) }}
                      >
                        {transaction.natureCode}
                      </span>
                      <span className="transaction-description">
                        {typeInfo.name}
                      </span>
                      <span className="transaction-nature">
                        ({typeInfo.nature})
                      </span>
                      <span className="transaction-date">
                        {formatDateTime(transaction.transactionDate, transaction.transactionTime)}
                      </span>
                    </div>
                    <div className="transaction-details">
                      <p className="card-info">
                        Card: <strong>{transaction.card}</strong>
                      </p>
                      <p className="transaction-meta">
                        Bank Code: {transaction.bankCode}
                      </p>
                    </div>
                  </div>
                  <div className="transaction-amount-container">
                    <span
                      className={`transaction-amount ${
                        signedAmount >= 0 ? 'positive' : 'negative'
                      }`}
                    >
                      {signedAmount >= 0 ? '+' : ''}
                      {new Intl.NumberFormat('pt-BR', {
                        style: 'currency',
                        currency: 'BRL',
                      }).format(Math.abs(signedAmount))}
                    </span>
                  </div>
                </div>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}

export default TransactionList;
