import React, { useState, useEffect, useCallback } from 'react';
import api from '../services/api';
import { useUploadStatusPolling } from '../hooks/useUploadStatusPolling';
import Spinner from './Spinner';
import StoreGroupedTransactions from './StoreGroupedTransactions';
import './UploadManager.css';

function UploadManager({ userInfo, onUploadSuccess }) {
  const [file, setFile] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [uploads, setUploads] = useState([]);
  const [incompleteUploads, setIncompleteUploads] = useState([]);
  const [actionLoading, setActionLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [selectedUploadId, setSelectedUploadId] = useState(null);
  const [selectedUpload, setSelectedUpload] = useState(null);
  const [groupedTransactions, setGroupedTransactions] = useState([]);
  const [loadingTransactions, setLoadingTransactions] = useState(false);
  const [transactionsPage, setTransactionsPage] = useState(1);
  const [transactionsTotalPages, setTransactionsTotalPages] = useState(1);
  const [transactionsTotalCount, setTransactionsTotalCount] = useState(0);
  const [stats, setStats] = useState({
    total: 0,
    pending: 0,
    processing: 0,
    success: 0,
    failed: 0,
    duplicate: 0,
    partiallyCompleted: 0
  });
  const [isPollingActive, setIsPollingActive] = useState(false);

  const isAdmin = userInfo?.role === 'Admin';

  // Função para calcular estatísticas
  const calculateStats = (items) => {
    const newStats = {
      total: items.length,
      pending: 0,
      processing: 0,
      success: 0,
      failed: 0,
      duplicate: 0,
      partiallyCompleted: 0
    };

    items.forEach(upload => {
      const status = upload.status?.toLowerCase();
      if (status === 'pending') newStats.pending++;
      else if (status === 'processing') newStats.processing++;
      else if (status === 'success') newStats.success++;
      else if (status === 'failed') newStats.failed++;
      else if (status === 'duplicate') newStats.duplicate++;
      else if (status === 'partiallycompleted') newStats.partiallyCompleted++;
    });

    setStats(newStats);
  };

  // Função para buscar status preciso de um upload individual
  const fetchUploadStatus = async (uploadId) => {
    try {
      const response = await api.get(`/transactions/uploads/${uploadId}`);
      return response.data;
    } catch (error) {
      console.error(`Error fetching upload ${uploadId}:`, error);
      return null;
    }
  };

  // Callback para atualização de uploads via polling
  const handleUploadsUpdate = useCallback(async (updatedUploads, pagedData) => {
    // Identificar uploads ativos que precisam de contagem precisa
    const activeUploads = updatedUploads.filter(
      upload => upload.status === 'Processing' || upload.status === 'Pending'
    );

    // Se houver uploads ativos, buscar status preciso de cada um
    if (activeUploads.length > 0) {
      const preciseStatuses = await Promise.all(
        activeUploads.map(upload => fetchUploadStatus(upload.id))
      );

      // Atualizar uploads com informações precisas
      const updatedWithPrecise = updatedUploads.map(upload => {
        const preciseStatus = preciseStatuses.find(s => s && s.id === upload.id);
        if (preciseStatus) {
          return {
            ...upload,
            processedLineCount: preciseStatus.processedLineCount,
            progressPercentage: preciseStatus.progressPercentage
          };
        }
        return upload;
      });

      setUploads(updatedWithPrecise);
      calculateStats(updatedWithPrecise);
    } else {
      setUploads(updatedUploads);
      calculateStats(updatedUploads);
    }

    if (pagedData) {
      setTotalPages(pagedData.totalPages || 1);
    }
    
    // Verificar se há uploads ativos para manter o polling
    const hasActiveUploads = activeUploads.length > 0;
    setIsPollingActive(hasActiveUploads);
  }, []);

  // Configurar polling - mais frequente quando há uploads ativos
  const pollingInterval = isPollingActive ? 2000 : 10000; // 2s se ativo, 10s se inativo
  
  useUploadStatusPolling(handleUploadsUpdate, {
    interval: pollingInterval,
    enabled: true,
    statusFilter: statusFilter || null,
    page,
    pageSize: 20
  });

  // Carregar uploads iniciais e incompletos
  useEffect(() => {
    loadIncompleteUploads();
    // O polling vai atualizar os uploads automaticamente
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadUploads = async () => {
    try {
      setLoading(true);
      setError(null);
      const params = {
        page,
        pageSize: 20,
        ...(statusFilter && { status: statusFilter })
      };
      const response = await api.get('/transactions/uploads', { params });
      setUploads(response.data.items || []);
      setTotalPages(response.data.totalPages || 1);
      calculateStats(response.data.items || []);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to load uploads');
    } finally {
      setLoading(false);
    }
  };

  const loadIncompleteUploads = async () => {
    try {
      const response = await api.get('/transactions/uploads/incomplete');
      const incomplete = response.data.incompleteUploads || [];
      
      // Buscar status preciso para cada upload incompleto
      const preciseIncomplete = await Promise.all(
        incomplete.map(async (upload) => {
          const preciseStatus = await fetchUploadStatus(upload.id);
          if (preciseStatus) {
            return {
              ...upload,
              processedLineCount: preciseStatus.processedLineCount,
              progressPercentage: preciseStatus.progressPercentage
            };
          }
          return upload;
        })
      );
      
      setIncompleteUploads(preciseIncomplete);
    } catch (err) {
      console.error('Failed to load incomplete uploads:', err);
    }
  };

  const handleFileChange = (event) => {
    const selectedFile = event.target.files[0];
    if (selectedFile) {
      setFile(selectedFile);
      setError(null);
    }
  };

  const handleClearFile = (event) => {
    event.preventDefault();
    event.stopPropagation();
    setFile(null);
    setError(null);
    const fileInput = document.getElementById('file-input');
    if (fileInput) {
      fileInput.value = '';
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();

    if (!file) {
      setError('Please select a file');
      return;
    }

    const formData = new FormData();
    formData.append('file', file);

    try {
      setLoading(true);
      setError(null);
      setMessage(null);

      const response = await api.post('/transactions/upload', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      });

      const message = response.status === 202 
        ? response.data.message || 'File accepted and queued for background processing'
        : response.data.message;
      
      setMessage(message);
      if (onUploadSuccess) {
        onUploadSuccess(true, message);
      }
      
      setFile(null);
      document.getElementById('file-input').value = '';
      
      // Recarregar uploads para mostrar o novo upload
      setTimeout(() => {
        loadUploads();
        loadIncompleteUploads();
        setIsPollingActive(true); // Ativar polling para acompanhar o novo upload
      }, 500);
    } catch (err) {
      const errorMsg = err.response?.data?.error || 'Error uploading file';
      setError(errorMsg);
      if (onUploadSuccess) {
        onUploadSuccess(false, null);
      }
    } finally {
      setLoading(false);
    }
  };

  const handleResumeUpload = async (uploadId) => {
    if (!window.confirm('Resume processing of this upload?')) return;

    try {
      setActionLoading(true);
      setError(null);
      const response = await api.post(`/transactions/uploads/${uploadId}/resume`);
      setMessage(response.data.message || 'Upload resumed successfully');
      setTimeout(() => setMessage(null), 5000);
      loadUploads();
      loadIncompleteUploads();
      setIsPollingActive(true);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to resume upload');
    } finally {
      setActionLoading(false);
    }
  };

  const handleResumeAll = async () => {
    if (!window.confirm('Resume processing of all incomplete uploads?')) return;

    try {
      setActionLoading(true);
      setError(null);
      const response = await api.post('/transactions/uploads/resume-all');
      setMessage(response.data.message || 'All incomplete uploads resumed');
      setTimeout(() => setMessage(null), 5000);
      loadUploads();
      loadIncompleteUploads();
      setIsPollingActive(true);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to resume uploads');
    } finally {
      setActionLoading(false);
    }
  };

  const handleClearData = async () => {
    if (!window.confirm('Are you sure you want to delete ALL data? This action cannot be undone!')) return;

    try {
      setActionLoading(true);
      setError(null);
      await api.delete('/transactions');
      setMessage('All data cleared successfully');
      setTimeout(() => setMessage(null), 5000);
      loadUploads();
      loadIncompleteUploads();
      setSelectedUploadId(null);
      setSelectedUpload(null);
      setGroupedTransactions([]);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to clear data');
    } finally {
      setActionLoading(false);
    }
  };

  const handleSelectUpload = async (upload) => {
    if (upload.status === 'Success') {
      setSelectedUploadId(upload.id);
      setSelectedUpload(upload);
      setTransactionsPage(1);
      await loadTransactionsForUpload(upload.id, 1);
    } else {
      setSelectedUploadId(null);
      setSelectedUpload(null);
      setGroupedTransactions([]);
      setTransactionsPage(1);
      setTransactionsTotalPages(1);
      setTransactionsTotalCount(0);
    }
  };

  const loadTransactionsForUpload = async (uploadId, page = 1) => {
    try {
      setLoadingTransactions(true);
      setError(null);
      const response = await api.get(`/transactions/stores/${uploadId}`, {
        params: { page, pageSize: 50 }
      });
      const pagedData = response.data || {};
      setGroupedTransactions(pagedData.items || []);
      setTransactionsTotalPages(pagedData.totalPages || 1);
      setTransactionsTotalCount(pagedData.totalCount || 0);
      setTransactionsPage(page);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to load transactions');
      setGroupedTransactions([]);
      setTransactionsTotalPages(1);
      setTransactionsTotalCount(0);
    } finally {
      setLoadingTransactions(false);
    }
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString('pt-BR');
  };

  const formatFileSize = (bytes) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  };

  const getStatusBadgeClass = (status) => {
    const statusLower = status?.toLowerCase();
    switch (statusLower) {
      case 'success': return 'badge-success';
      case 'processing': return 'badge-processing';
      case 'failed': return 'badge-failed';
      case 'pending': return 'badge-pending';
      case 'duplicate': return 'badge-duplicate';
      case 'partiallycompleted': return 'badge-partial';
      default: return 'badge-default';
    }
  };

  // Verifica se um upload pode ser resumido (está stuck/incompleto)
  const canResumeUpload = (upload) => {
    // Se está na lista de incompletos, pode ser resumido (já foi identificado como stuck)
    const isInIncompleteList = incompleteUploads.some(u => u.id === upload.id);
    if (isInIncompleteList) {
      return true;
    }

    // Se não está em Processing, não pode ser resumido
    if (upload.status !== 'Processing') {
      return false;
    }

    // Se está em Processing mas não tem checkpoint, não pode ser resumido ainda
    // (ainda não começou a processar ou está no início)
    if (!upload.lastCheckpointAt || !upload.lastCheckpointLine || upload.lastCheckpointLine === 0) {
      return false;
    }

    // Verificar se o processamento começou há mais de 30 minutos
    // e o último checkpoint foi há mais de 30 minutos - indica que está stuck
    const now = new Date();
    const lastCheckpoint = new Date(upload.lastCheckpointAt);
    const minutesSinceCheckpoint = (now - lastCheckpoint) / (1000 * 60);
    
    // Se o checkpoint foi há mais de 30 minutos, o upload está stuck e pode ser resumido
    // Isso indica que não há progresso há pelo menos 30 minutos
    return minutesSinceCheckpoint > 30;
  };

  // Função para calcular a porcentagem de progresso corretamente
  const calculateProgressPercentage = (upload) => {
    if (!upload || !upload.totalLineCount || upload.totalLineCount === 0) {
      return 0;
    }
    
    // Usar processedLineCount como base, mas garantir que não ultrapasse totalLineCount
    const processed = Math.min(upload.processedLineCount || 0, upload.totalLineCount);
    const percentage = (processed / upload.totalLineCount) * 100;
    
    // Garantir que a porcentagem não ultrapasse 100%
    return Math.min(Math.round(percentage * 100) / 100, 100);
  };

  return (
    <div className="upload-manager">
      {/* Upload Section */}
      <section className="upload-section">
        <form className="upload-form" onSubmit={handleSubmit}>
          <h2>Upload CNAB File</h2>
          <p className="upload-description">
            Select a CNAB file to import financial transactions
          </p>

          <div className="form-group">
            <label htmlFor="file-input" className="file-label">
              <span className="file-icon">📁</span>
              <span className="file-text">
                {file ? file.name : 'Choose file or drag and drop'}
              </span>
              {file && (
                <button
                  type="button"
                  className="file-clear-btn"
                  onClick={handleClearFile}
                  disabled={loading}
                  aria-label="Clear file selection"
                >
                  ✕
                </button>
              )}
            </label>
            <input
              id="file-input"
              type="file"
              onChange={handleFileChange}
              disabled={loading}
              className="file-input"
              accept=".txt"
            />
          </div>

          {error && <div className="error-message">{error}</div>}
          {message && <div className="success-message">{message}</div>}

          <button
            type="submit"
            className="btn-upload"
            disabled={!file || loading}
          >
            {loading ? 'Uploading...' : 'Upload File'}
          </button>
        </form>
      </section>

      {/* Statistics Cards */}
      <div className="stats-grid">
        <div className="stat-card">
          <h3>Total Uploads</h3>
          <p className="stat-value">{stats.total}</p>
        </div>
        <div className="stat-card">
          <h3>Processing</h3>
          <p className="stat-value processing">{stats.processing}</p>
        </div>
        <div className="stat-card">
          <h3>Success</h3>
          <p className="stat-value success">{stats.success}</p>
        </div>
        <div className="stat-card">
          <h3>Failed</h3>
          <p className="stat-value failed">{stats.failed}</p>
        </div>
        <div className="stat-card">
          <h3>Pending</h3>
          <p className="stat-value pending">{stats.pending}</p>
        </div>
        <div className="stat-card">
          <h3>Incomplete</h3>
          <p className="stat-value incomplete">{incompleteUploads.length}</p>
        </div>
      </div>

      {/* Real-time Status Indicator */}
      {isPollingActive && (
        <div className="realtime-indicator">
          <span className="pulse-dot"></span>
          <span>Monitoring uploads in real-time</span>
        </div>
      )}

      {/* Actions Section */}
      {isAdmin && (
        <div className="admin-actions">
          <h3>Actions</h3>
          <div className="actions-buttons">
            {incompleteUploads.length > 0 && (
              <button
                className="btn btn-warning"
                onClick={handleResumeAll}
                disabled={actionLoading}
              >
                {actionLoading ? 'Processing...' : `Resume All Incomplete (${incompleteUploads.length})`}
              </button>
            )}
            <button
              className="btn btn-danger"
              onClick={handleClearData}
              disabled={actionLoading}
            >
              {actionLoading ? 'Processing...' : 'Clear All Data'}
            </button>
          </div>
        </div>
      )}

      {/* Incomplete Uploads Section */}
      {incompleteUploads.length > 0 && (
        <div className="incomplete-section">
          <h3>Incomplete Uploads (Requiring Attention)</h3>
          <div className="uploads-table-container">
            <table className="uploads-table">
              <thead>
                <tr>
                  <th>File Name</th>
                  <th>Status</th>
                  <th>Progress</th>
                  <th>Lines</th>
                  <th>Last Checkpoint</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {incompleteUploads.map(upload => (
                  <tr key={upload.id}>
                    <td>{upload.fileName}</td>
                    <td>
                      <span className={`badge ${getStatusBadgeClass(upload.status)}`}>
                        {upload.status}
                      </span>
                    </td>
                    <td>
                      <div className="progress-bar">
                        <div
                          className="progress-fill"
                          style={{
                            width: `${calculateProgressPercentage(upload)}%`,
                            backgroundColor: upload.status === 'Processing' ? '#3498db' : '#e74c3c'
                          }}
                        />
                        <span className="progress-text">
                          {upload.processedLineCount || 0} / {upload.totalLineCount || 0}
                        </span>
                      </div>
                    </td>
                    <td>
                      <div className="line-stats">
                        <span className="line-stat success">✓ {upload.processedLineCount || 0}</span>
                        <span className="line-stat failed">✗ {upload.failedLineCount || 0}</span>
                        <span className="line-stat skipped">⊘ {upload.skippedLineCount || 0}</span>
                      </div>
                    </td>
                    <td>
                      {upload.lastCheckpointAt
                        ? formatDate(upload.lastCheckpointAt)
                        : 'No checkpoint'}
                      <br />
                      <small>Line: {upload.lastCheckpointLine || 0}</small>
                    </td>
                    <td>
                      {isAdmin && (
                        <button
                          className="btn btn-sm btn-primary"
                          onClick={() => handleResumeUpload(upload.id)}
                          disabled={actionLoading}
                        >
                          Resume
                        </button>
                      )}
                      {!isAdmin && (
                        <span className="text-muted">Admin only</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* All Uploads Section */}
      <div className="uploads-section">
        <div className="section-header">
          <h3>All Uploads</h3>
          <div className="filter-controls">
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setPage(1);
              }}
              className="filter-select"
            >
              <option value="">All Status</option>
              <option value="Pending">Pending</option>
              <option value="Processing">Processing</option>
              <option value="Success">Success</option>
              <option value="Failed">Failed</option>
              <option value="Duplicate">Duplicate</option>
              <option value="PartiallyCompleted">Partially Completed</option>
            </select>
          </div>
        </div>

        {loading ? (
          <Spinner />
        ) : (
          <>
            <div className="uploads-table-container">
              <table className="uploads-table">
                <thead>
                  <tr>
                    <th>File Name</th>
                    <th>Status</th>
                    <th>Size</th>
                    <th>Progress</th>
                    <th>Lines</th>
                    <th>Uploaded At</th>
                    <th>Completed At</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {uploads.length === 0 ? (
                    <tr>
                      <td colSpan="8" className="empty-state">
                        No uploads found
                      </td>
                    </tr>
                  ) : (
                    uploads.map(upload => (
                      <tr 
                        key={upload.id}
                        className={selectedUploadId === upload.id ? 'selected-row' : ''}
                        onClick={() => handleSelectUpload(upload)}
                        style={{ cursor: upload.status === 'Success' ? 'pointer' : 'default' }}
                      >
                        <td>{upload.fileName}</td>
                        <td>
                          <span className={`badge ${getStatusBadgeClass(upload.status)}`}>
                            {upload.status}
                          </span>
                        </td>
                        <td>{formatFileSize(upload.fileSize || 0)}</td>
                        <td>
                          {upload.totalLineCount > 0 ? (
                            <div className="progress-bar">
                              <div
                                className="progress-fill"
                                style={{
                                  width: `${calculateProgressPercentage(upload)}%`,
                                  backgroundColor:
                                    upload.status === 'Processing' ? '#3498db' :
                                    upload.status === 'Success' ? '#27ae60' :
                                    upload.status === 'Failed' ? '#e74c3c' :
                                    upload.status === 'PartiallyCompleted' ? '#f39c12' : '#95a5a6'
                                }}
                              />
                              <span className="progress-text">
                                {calculateProgressPercentage(upload).toFixed(2)}%
                              </span>
                            </div>
                          ) : (
                            'N/A'
                          )}
                        </td>
                        <td>
                          <div className="line-stats">
                            <span className="line-stat success" title="Processed">
                              ✓ {upload.processedLineCount || 0}
                            </span>
                            <span className="line-stat failed" title="Failed">
                              ✗ {upload.failedLineCount || 0}
                            </span>
                            <span className="line-stat skipped" title="Skipped (duplicates)">
                              ⊘ {upload.skippedLineCount || 0}
                            </span>
                            <span className="line-stat total" title="Total">
                              / {upload.totalLineCount || 0}
                            </span>
                          </div>
                        </td>
                        <td>{formatDate(upload.uploadedAt)}</td>
                        <td>{formatDate(upload.processingCompletedAt)}</td>
                        <td>
                          {canResumeUpload(upload) && isAdmin && (
                            <button
                              className="btn btn-sm btn-primary"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleResumeUpload(upload.id);
                              }}
                              disabled={actionLoading}
                              title={`Resume from line ${upload.lastCheckpointLine || 0}`}
                            >
                              Resume
                            </button>
                          )}
                          {upload.status === 'Success' && (
                            <span className="select-hint">Click to view summary</span>
                          )}
                          {upload.status === 'Processing' && !canResumeUpload(upload) && (
                            <span className="text-muted" style={{ fontSize: '11px' }}>
                              Processing...
                            </span>
                          )}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="pagination">
                <button
                  className="btn btn-sm"
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1 || loading}
                >
                  Previous
                </button>
                <span className="page-info">
                  Page {page} of {totalPages}
                </span>
                <button
                  className="btn btn-sm"
                  onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages || loading}
                >
                  Next
                </button>
              </div>
            )}
          </>
        )}
      </div>

      {/* Transactions Summary Section */}
      {selectedUploadId && selectedUpload && (
        <div className="transactions-summary-section">
          <h3>Summary for: {selectedUpload.fileName}</h3>
          {loadingTransactions ? (
            <Spinner />
          ) : (
            <>
              <StoreGroupedTransactions stores={groupedTransactions} />
              {/* Pagination for transactions */}
              {transactionsTotalPages > 1 && (
                <div className="pagination" style={{ marginTop: '20px' }}>
                  <button
                    className="btn btn-sm"
                    onClick={() => {
                      const newPage = Math.max(1, transactionsPage - 1);
                      loadTransactionsForUpload(selectedUploadId, newPage);
                    }}
                    disabled={transactionsPage === 1 || loadingTransactions}
                  >
                    Previous
                  </button>
                  <span className="page-info">
                    Page {transactionsPage} of {transactionsTotalPages} 
                    {transactionsTotalCount > 0 && ` (${transactionsTotalCount} stores)`}
                  </span>
                  <button
                    className="btn btn-sm"
                    onClick={() => {
                      const newPage = Math.min(transactionsTotalPages, transactionsPage + 1);
                      loadTransactionsForUpload(selectedUploadId, newPage);
                    }}
                    disabled={transactionsPage === transactionsTotalPages || loadingTransactions}
                  >
                    Next
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}

export default UploadManager;

