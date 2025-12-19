import React, { useState } from 'react';
import api from '../services/api';
import './UploadForm.css';

function UploadForm({ onUpload, isAuthenticated }) {
  const [file, setFile] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleFileChange = (event) => {
    const selectedFile = event.target.files[0];
    if (selectedFile) {
      setFile(selectedFile);
      setError(null);
    }
  };

  const handleSubmit = async (event) => {
    event.preventDefault();

    if (!isAuthenticated) {
      setError('Please log in to upload.');
      return;
    }

    if (!file) {
      setError('Please select a file');
      return;
    }

    const formData = new FormData();
    formData.append('file', file);

    try {
      setLoading(true);
      setError(null);

      const response = await api.post('/transactions/upload', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      });

      onUpload(true, response.data.message);
      setFile(null);
      document.getElementById('file-input').value = '';
    } catch (err) {
      setError(err.response?.data?.error || 'Error uploading file');
      onUpload(false, null);
    } finally {
      setLoading(false);
    }
  };

  return (
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

      <button
        type="submit"
        className="btn-upload"
        disabled={!file || loading}
      >
        {loading ? 'Uploading...' : 'Upload File'}
      </button>
    </form>
  );
}

export default UploadForm;
