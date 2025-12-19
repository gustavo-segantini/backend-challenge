import React, { useState } from 'react';
import './LoginForm.css';

function LoginForm({ onLogin, onLogout, isAuthenticated, userInfo, error, onGitHub }) {
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('Admin123!');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!onLogin) return;
    setLoading(true);
    await onLogin({ username, password });
    setLoading(false);
  };

  return (
    <div className="login-card">
      <div className="login-card__header">
        <h2>{isAuthenticated ? 'Sessão ativa' : 'Autenticação'}</h2>
        <p>{isAuthenticated ? 'Token carregado para chamadas protegidas.' : 'Faça login para usar a API protegida.'}</p>
      </div>

      {error && <div className="login-card__error">{error}</div>}

      {isAuthenticated ? (
        <div className="login-card__session">
          <div>
            <strong>Usuário:</strong> {userInfo?.username || 'admin'}
          </div>
          <div>
            <strong>Papel:</strong> {userInfo?.role || 'Admin'}
          </div>
          <button className="btn btn-secondary" onClick={onLogout}>
            Sair
          </button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="login-card__form">
          <label className="login-card__label">
            Usuário
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="login-card__input"
              placeholder="admin"
            />
          </label>

          <label className="login-card__label">
            Senha
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="login-card__input"
              placeholder="Admin123!"
            />
          </label>

          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Entrando...' : 'Entrar'}
          </button>
          <button type="button" className="btn btn-ghost" onClick={onGitHub}>
            Entrar com GitHub
          </button>
        </form>
      )}
    </div>
  );
}

export default LoginForm;
