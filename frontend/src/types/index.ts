export interface AuthResponse {
  token: string;
  refreshToken: string;
  name: string;
  email: string;
  userId: string;
}

export type ProcessingStatus =
  | 'Recebido'
  | 'ExtraindoArquivos'
  | 'ProcessandoComIA'
  | 'Concluido'
  | 'Falhou';

export interface Lecture {
  id: string;
  fileName: string;
  fileSizeBytes: number;
}

export interface QuestionOption {
  label: string;
  text: string;
  isCorrect: boolean;
}

export interface Question {
  order: number;
  statement: string;
  explanation: string;
  options: QuestionOption[];
}

export interface StudySessionSummary {
  id: string;
  originalZipFileName: string;
  status: ProcessingStatus;
  createdAtUtc: string;
  processedAtUtc: string | null;
  errorMessage: string | null;
  lectureCount: number;
}

export interface StudySessionDetail {
  id: string;
  originalZipFileName: string;
  status: ProcessingStatus;
  createdAtUtc: string;
  processedAtUtc: string | null;
  errorMessage: string | null;
  lectures: Lecture[];
  summaryMarkdown: string | null;
  questions: Question[] | null;
}
