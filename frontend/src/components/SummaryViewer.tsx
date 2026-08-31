import ReactMarkdown from 'react-markdown';

export default function SummaryViewer({ markdown }: { markdown: string }) {
  return (
    <div className="summary card">
      <span className="eyebrow">Resumo consolidado</span>
      <div className="summary-content">
        <ReactMarkdown>{markdown}</ReactMarkdown>
      </div>
    </div>
  );
}
