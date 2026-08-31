import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { listStudySessions, uploadZip } from '../services/api';
import DropZone from '../components/DropZone';
import SummaryViewer from '../components/SummaryViewer';
import ExamViewer from '../components/ExamViewer';
import SessionHistoryList from '../components/SessionHistoryList';
import { useStudySessionPolling } from '../hooks/useStudySessionPolling';
import type { StudySessionSummary } from '../types';

type ViewTab = 'resumo' | 'prova';

export default function Dashboard() {
  const { user, logout } = useAuth();
  const [sessions, setSessions] = useState<StudySessionSummary[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [tab, setTab] = useState<ViewTab>('resumo');

  const { session: activeSession, error: pollError } = useStudySessionPolling(activeId);

  useEffect(() => {
    refreshHistory();
  }, []);

  async function refreshHistory() {
    try {
      const data = await listStudySessions();
      setSessions(data);
    } catch {
      // Silencioso: o histórico é secundário à ação principal da tela.
    }
  }

  async function handleFileSelected(file: File) {
    setUploadError(null);
    setIsUploading(true);
    setUploadProgress(0);
    try {
      const result = await uploadZip(file, setUploadProgress);
      setActiveId(result.id);
      setTab('resumo');
      await refreshHistory();
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : 'Falha ao enviar o arquivo.');
    } finally {
      setIsUploading(false);
    }
  }

  const isProcessing =
    activeSession &&
    (activeSession.status === 'Recebido' ||
      activeSession.status === 'ExtraindoArquivos' ||
      activeSession.status === 'ProcessandoComIA');

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="auth-mark">§</span>
          <div>
            <p className="sidebar-title">Pós FIAP</p>
            <p className="sidebar-subtitle">Resumo & Prova com IA</p>
          </div>
        </div>

        <div className="sidebar-user">
          <p className="sidebar-user-name">{user?.name}</p>
          <p className="sidebar-user-email">{user?.email}</p>
          <button className="btn btn-ghost" onClick={logout}>Sair</button>
        </div>

        <div className="sidebar-history">
          <span className="eyebrow">Histórico</span>
          <SessionHistoryList sessions={sessions} activeId={activeId} onSelect={setActiveId} />
        </div>
      </aside>

      <main className="main-content">
        <section className="upload-section">
          <span className="eyebrow">Novo envio</span>
          <h1 className="page-title">Solte o .zip das aulas da disciplina</h1>
          <p className="page-subtitle">
            Reunimos todos os PDFs em um único material de estudo: um resumo consolidado
            e uma prova de 20 questões de múltipla escolha, cobrindo o conteúdo de todas as aulas.
          </p>

          <DropZone onFileSelected={handleFileSelected} disabled={isUploading} />

          {isUploading && (
            <div className="upload-progress">
              <div className="upload-progress-bar" style={{ width: `${uploadProgress}%` }} />
              <span>Enviando… {uploadProgress}%</span>
            </div>
          )}

          {uploadError && <div className="error-banner" style={{ marginTop: 12 }}>{uploadError}</div>}
        </section>

        {activeSession && (
          <section className="result-section">
            <div className="result-header">
              <div>
                <span className="eyebrow">{activeSession.originalZipFileName}</span>
                <h2 className="result-title">
                  {activeSession.lectures.length} aula{activeSession.lectures.length === 1 ? '' : 's'} processada
                  {activeSession.lectures.length === 1 ? '' : 's'}
                </h2>
              </div>

              {activeSession.status === 'Concluido' && (
                <div className="tab-switch">
                  <button
                    className={tab === 'resumo' ? 'tab-active' : ''}
                    onClick={() => setTab('resumo')}
                  >
                    Resumo
                  </button>
                  <button
                    className={tab === 'prova' ? 'tab-active' : ''}
                    onClick={() => setTab('prova')}
                  >
                    Prova
                  </button>
                </div>
              )}
            </div>

            {isProcessing && (
              <div className="processing-panel">
                <div className="processing-spinner" aria-hidden="true" />
                <p>
                  {activeSession.status === 'ExtraindoArquivos' && 'Extraindo os PDFs do arquivo .zip…'}
                  {activeSession.status === 'ProcessandoComIA' &&
                    'A IA está lendo as aulas e montando o resumo e a prova. Isso pode levar alguns minutos.'}
                  {activeSession.status === 'Recebido' && 'Preparando o processamento…'}
                </p>
              </div>
            )}

            {activeSession.status === 'Falhou' && (
              <div className="error-banner">
                Não foi possível gerar o material: {activeSession.errorMessage}
              </div>
            )}

            {pollError && <div className="error-banner">{pollError}</div>}

            {activeSession.status === 'Concluido' && tab === 'resumo' && activeSession.summaryMarkdown && (
              <SummaryViewer markdown={activeSession.summaryMarkdown} />
            )}

            {activeSession.status === 'Concluido' && tab === 'prova' && activeSession.questions && (
              <ExamViewer questions={activeSession.questions} />
            )}
          </section>
        )}
      </main>
    </div>
  );
}
