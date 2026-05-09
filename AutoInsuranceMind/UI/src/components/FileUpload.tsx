import React, { useState, useRef } from 'react';
import { UploadedDocument } from '../types/upload';
import { uploadDocument, getDocuments, deleteDocument } from '../services/uploadService';
import '../styles/FileUpload.css';

interface FileUploadProps {
  onDocumentUploaded?: (doc: UploadedDocument) => void;
}

const FileUpload: React.FC<FileUploadProps> = ({ onDocumentUploaded }) => {
  const [dragging, setDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [documents, setDocuments] = useState<UploadedDocument[]>([]);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [loaded, setLoaded] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadDocuments = async () => {
    try {
      const docs = await getDocuments();
      setDocuments(docs);
      setLoaded(true);
    } catch {
      setError('Failed to load documents');
    }
  };

  const handleFile = async (file: File) => {
    setError('');
    setMessage('');
    const allowed = ['.pdf', '.txt', '.docx', '.doc'];
    const ext = '.' + file.name.split('.').pop()?.toLowerCase();
    if (!allowed.includes(ext)) {
      setError('Only PDF, TXT, and DOCX files are supported');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      setError('File must be under 10 MB');
      return;
    }

    setUploading(true);
    try {
      const doc = await uploadDocument(file);
      setMessage(`✓ ${doc.fileName} uploaded and indexed`);
      onDocumentUploaded?.(doc);
      await loadDocuments();
    } catch (err: any) {
      setError(err.message ?? 'Upload failed');
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteDocument(id);
      setDocuments((prev) => prev.filter((d) => d.id !== id));
    } catch {
      setError('Failed to delete document');
    }
  };

  return (
    <div className="file-upload-panel">
      <h3 className="panel-title">📄 Upload Policy Documents</h3>
      <p className="panel-subtitle">Upload PDF, DOCX, or TXT files to enable AI-powered policy Q&amp;A</p>

      <div
        className={`drop-zone ${dragging ? 'dragging' : ''} ${uploading ? 'uploading' : ''}`}
        onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          const f = e.dataTransfer.files[0];
          if (f) handleFile(f);
        }}
        onClick={() => !uploading && fileInputRef.current?.click()}
      >
        <input
          ref={fileInputRef}
          type="file"
          accept=".pdf,.txt,.docx,.doc"
          style={{ display: 'none' }}
          onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f); }}
        />
        {uploading ? (
          <div className="drop-content">
            <div className="spinner" />
            <p>Processing document…</p>
          </div>
        ) : (
          <div className="drop-content">
            <span className="drop-icon">☁️</span>
            <p>Drag & drop a file here, or <span className="drop-link">browse</span></p>
            <p className="drop-hint">PDF, DOCX, TXT · Max 10 MB</p>
          </div>
        )}
      </div>

      {message && <p className="upload-success">{message}</p>}
      {error && <p className="upload-error">{error}</p>}

      <div className="docs-section">
        <div className="docs-header">
          <h4>Uploaded Documents</h4>
          <button className="btn-link" onClick={loadDocuments}>Refresh</button>
        </div>
        {!loaded ? (
          <button className="btn-secondary btn-sm" onClick={loadDocuments}>Load Documents</button>
        ) : documents.length === 0 ? (
          <p className="empty-state">No documents uploaded yet</p>
        ) : (
          <ul className="docs-list">
            {documents.map((doc) => (
              <li key={doc.id} className="doc-item">
                <div className="doc-info">
                  <span className="doc-name">{doc.fileName}</span>
                  <span className="doc-meta">{(doc.fileSize / 1024).toFixed(1)} KB · {doc.status}</span>
                </div>
                <button className="btn-delete" onClick={() => handleDelete(doc.id)}>✕</button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
};

export default FileUpload;
