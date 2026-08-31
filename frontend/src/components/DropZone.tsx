import { useCallback, useRef, useState, type DragEvent } from 'react';

interface DropZoneProps {
  onFileSelected: (file: File) => void;
  disabled?: boolean;
}

export default function DropZone({ onFileSelected, disabled }: DropZoneProps) {
  const [isDragOver, setIsDragOver] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const validateAndEmit = useCallback(
    (file: File | undefined) => {
      if (!file) return;
      if (!file.name.toLowerCase().endsWith('.zip')) {
        setValidationError('Envie um arquivo .zip contendo os PDFs das aulas.');
        return;
      }
      setValidationError(null);
      onFileSelected(file);
    },
    [onFileSelected]
  );

  function handleDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragOver(false);
    if (disabled) return;
    validateAndEmit(e.dataTransfer.files?.[0]);
  }

  function handleDragOver(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    if (!disabled) setIsDragOver(true);
  }

  function handleDragLeave() {
    setIsDragOver(false);
  }

  return (
    <div className="dropzone-wrapper">
      <div
        className={`dropzone ${isDragOver ? 'dropzone--active' : ''} ${disabled ? 'dropzone--disabled' : ''}`}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => !disabled && inputRef.current?.click()}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => {
          if ((e.key === 'Enter' || e.key === ' ') && !disabled) inputRef.current?.click();
        }}
      >
        <input
          ref={inputRef}
          type="file"
          accept=".zip"
          hidden
          disabled={disabled}
          onChange={(e) => validateAndEmit(e.target.files?.[0])}
        />

        <div className="dropzone-bubbles" aria-hidden="true">
          <span className="bubble">A</span>
          <span className="bubble bubble--filled">B</span>
          <span className="bubble">C</span>
          <span className="bubble">D</span>
        </div>

        <p className="dropzone-title">
          {isDragOver ? 'Solte o arquivo aqui' : 'Arraste o .zip das suas aulas'}
        </p>
        <p className="dropzone-subtitle">
          ou clique para selecionar · PDFs dentro de um único .zip · até 100MB
        </p>
      </div>

      {validationError && <div className="error-banner" style={{ marginTop: 12 }}>{validationError}</div>}
    </div>
  );
}
