import React from 'react';
import './StoreGroupedTransactions.css';

function StoreGroupedTransactions({ stores }) {
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

  const formatDate = (date) => {
    const dateObj = new Date(date);
    return dateObj.toLocaleDateString('pt-BR');
  };

  const formatTime = (time) => {
    if (!time) return '00:00:00';
    if (typeof time === 'string') {
      // TimeSpan is serialized as "HH:mm:ss" or "HH:mm:ss.fffffff"
      return time.split('.')[0]; // Take only HH:mm:ss part
    }
    return '00:00:00';
  };

  const formatCard = (card) => {
    if (!card || card.length < 4) return card;
    return `${card.substring(0, 4)}****${card.substring(card.length - 4)}`;
  };

  if (!stores || stores.length === 0) {
    return (
      <div className="store-grouped-container">
        <p className="empty-message">No transactions found. Upload a CNAB file first.</p>
      </div>
    );
  }

  return (
    <div className="store-grouped-container">
      <h2>Summary by Store</h2>
      <div className="stores-table">
        <table>
          <thead>
            <tr>
              <th>STORE NAME</th>
              <th>TYPE</th>
              <th>DATE</th>
              <th>TIME</th>
              <th>VALUE</th>
              <th>CARD</th>
              <th>TAX ID</th>
              <th>BALANCE</th>
            </tr>
          </thead>
          <tbody>
            {stores.map((store, storeIndex) => (
              <React.Fragment key={`${store.storeName}-${store.storeOwner}-${storeIndex}`}>
                {store.transactions.map((transaction, transIndex) => {
                  const typeInfo = transactionTypes[transaction.natureCode] || { name: 'Unknown', nature: 'Unknown', sign: '+' };
                  const signedAmount = getSignedAmount(transaction);
                  
                  return (
                    <tr key={transaction.id || `${store.storeName}-${transIndex}`} className="transaction-row">
                      <td>{transaction.storeName || store.storeName}</td>
                      <td>
                        <span className="type-badge" style={{ backgroundColor: getTypeColor(transaction.natureCode) }}>
                          {typeInfo.name}
                        </span>
                      </td>
                      <td>{formatDate(transaction.transactionDate)}</td>
                      <td>{formatTime(transaction.transactionTime)}</td>
                      <td className={signedAmount >= 0 ? 'value-positive' : 'value-negative'}>
                        {new Intl.NumberFormat('pt-BR', {
                          style: 'currency',
                          currency: 'BRL',
                        }).format(Math.abs(transaction.amount))}
                      </td>
                      <td>{formatCard(transaction.card)}</td>
                      <td>{transaction.cpf || store.storeOwner || transaction.storeOwner || ''}</td>
                      <td></td>
                    </tr>
                  );
                })}
                <tr key={`balance-${store.storeName}-${storeIndex}`} className="balance-row">
                  <td colSpan="7" className="balance-label">
                    Balance for {store.storeName}:
                  </td>
                  <td className={`balance-value ${store.balance >= 0 ? 'positive' : 'negative'}`}>
                    {new Intl.NumberFormat('pt-BR', {
                      style: 'currency',
                      currency: 'BRL',
                    }).format(store.balance)}
                  </td>
                </tr>
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default StoreGroupedTransactions;

