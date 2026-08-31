import type { StudySessionSummary } from '../types';

const STATUS_LABELS: Record<string, string> = {
  Recebido: 'Recebido',
  ExtraindoArquivos: 'Extraindo PDFs',
  ProcessandoComIA: 'Gerando com IA',
  Concluido: 'Concluído',
  Falhou: 'Falhou',
};

const STATUS_CLASS: Record<string, string> = {
  Recebido: 'status--pending',
  ExtraindoArquivos: 'status--pending',
  ProcessandoComIA: 'status--pending',
  Concluido: 'status--ok',
  Falhou: 'status--error',
};

interface Props {
  sessions: StudySessionSummary[];
  activeId: string | null;
  onSelect: (id: string) => void;
}

export default function SessionHistoryList({ sessions, activeId, onSelect }: Props) {
  if (sessions.length === 0) {
    return <p className="history-empty">Seus envios anteriores vão aparecer aqui.</p>;
  }

  return (
    <ul className="history-list">
      {sessions.map((s) => (
        <li key={s.id}>
          <button
            className={`history-item ${activeId === s.id ? 'history-item--active' : ''}`}
            onClick={() => onSelect(s.id)}
          >
            <div className="history-item-top">
              <span className="history-filename">{s.originalZipFileName}</span>
              <span className={`status-pill ${STATUS_CLASS[s.status] ?? ''}`}>
                {STATUS_LABELS[s.status] ?? s.status}
              </span>
            </div>
            <span className="history-meta">
              {s.lectureCount} aula{s.lectureCount === 1 ? '' : 's'} ·{' '}
              {new Date(s.createdAtUtc).toLocaleString('pt-BR')}
            </span>
          </button>
        </li>
      ))}
    </ul>
  );
}
