import React from 'react';
import './StoreList.css';

function StoreList({ stores }) {
  return (
    <div className="store-list-container">
      <h2>Stores</h2>
      {stores.length === 0 ? (
        <p className="empty-message">No stores found. Upload a CNAB file first.</p>
      ) : (
        <div className="store-grid">
          {stores.map((store) => (
            <div key={`${store.owner}-${store.name}`} className="store-card">
              <h3 className="store-name">{store.name}</h3>
              <p className="store-owner">Owner: {store.owner}</p>
              <div className="store-balance">
                <span className="balance-label">Balance:</span>
                <span className={`balance-amount ${store.balance >= 0 ? 'positive' : 'negative'}`}>
                  {new Intl.NumberFormat('pt-BR', {
                    style: 'currency',
                    currency: 'BRL',
                  }).format(store.balance)}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default StoreList;
