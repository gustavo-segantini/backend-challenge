import React, { useState, useEffect } from 'react';
import api from '../services/api';
import Spinner from './Spinner';
import StoreGroupedTransactions from './StoreGroupedTransactions';
import './AdminPanel.css';

function AdminPanel({ userInfo }) {
  const [uploads, setUploads] = useState([]);
  const [incompleteUploads, setIncompleteUploads] = useState([]);
  const [loading, setLoading] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [selectedUploadId, setSelectedUploadId] = useState(null);
  const [selectedUpload, setSelectedUpload] = useState(null);
  const [groupedTransactions, setGroupedTransactions] = useState([]);
  const [loadingTransactions, setLoadingTransactions] = useState(false);
  const [stats, setStats] = useState({
    total: 0,
    pending: 0,
    processing: 0,
    success: 0,
    failed: 0,
    duplicate: 0,
    partiallyCompleted: 0
  });

  const isAdmin = userInfo?.role === 'Admin';

  useEffect(() => {
    loadUploads();
    loadIncompleteUploads();
  }, [page, statusFilter]);

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
      
      // Calculate stats from all uploads (would need separate endpoint for accurate stats)
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
      setIncompleteUploads(response.data.incompleteUploads || []);
    } catch (err) {
      console.error('Failed to load incomplete uploads:', err);
    }
  };

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
      await loadTransactionsForUpload(upload.id);
    } else {
      setSelectedUploadId(null);
      setSelectedUpload(null);
      setGroupedTransactions([]);
    }
  };

  const loadTransactionsForUpload = async (uploadId) => {
    try {
      setLoadingTransactions(true);
      setError(null);
      const response = await api.get(`/transactions/stores/${uploadId}`);
      setGroupedTransactions(response.data || []);
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to load transactions');
      setGroupedTransactions([]);
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

  return (
    <div className="admin-panel">
      <div className="admin-header">
        <h2>Administration Panel</h2>
        <p>Manage file uploads and system operations</p>
      </div>

      {error && <div className="alert alert-error">{error}</div>}
      {message && <div className="alert alert-success">{message}</div>}

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
                            width: `${upload.progressPercentage || 0}%`,
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
                                  width: `${upload.progressPercentage || 0}%`,
                                  backgroundColor:
                                    upload.status === 'Processing' ? '#3498db' :
                                    upload.status === 'Success' ? '#27ae60' :
                                    upload.status === 'Failed' ? '#e74c3c' :
                                    upload.status === 'PartiallyCompleted' ? '#f39c12' : '#95a5a6'
                                }}
                              />
                              <span className="progress-text">
                                {upload.progressPercentage || 0}%
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
                          {upload.status === 'Processing' && upload.lastCheckpointLine > 0 && isAdmin && (
                            <button
                              className="btn btn-sm btn-primary"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleResumeUpload(upload.id);
                              }}
                              disabled={actionLoading}
                              title={`Resume from line ${upload.lastCheckpointLine}`}
                            >
                              Resume
                            </button>
                          )}
                          {upload.status === 'Success' && (
                            <span className="select-hint">Click to view summary</span>
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
            <StoreGroupedTransactions stores={groupedTransactions} />
          )}
        </div>
      )}
    </div>
  );
}

export default AdminPanel;

