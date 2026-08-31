import { useEffect, useRef, useState } from 'react';
import { getStudySession } from '../services/api';
import type { StudySessionDetail } from '../types';

const POLL_INTERVAL_MS = 4000;
const TERMINAL_STATUSES = new Set(['Concluido', 'Falhou']);

/// Faz polling de uma StudySession enquanto ela estiver em processamento
/// (extração dos PDFs / geração via IA), parando ao atingir um estado final.
export function useStudySessionPolling(id: string | null) {
  const [session, setSession] = useState<StudySessionDetail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (!id) {
      setSession(null);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(null);

    async function poll() {
      try {
        const data = await getStudySession(id!);
        if (cancelled) return;
        setSession(data);
        setIsLoading(false);

        if (!TERMINAL_STATUSES.has(data.status)) {
          timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
        }
      } catch (err) {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : 'Erro ao consultar status.');
        setIsLoading(false);
      }
    }

    poll();

    return () => {
      cancelled = true;
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [id]);

  return { session, isLoading, error };
}
