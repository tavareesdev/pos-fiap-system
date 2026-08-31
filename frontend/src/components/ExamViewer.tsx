import { useState } from 'react';
import type { Question } from '../types';

export default function ExamViewer({ questions }: { questions: Question[] }) {
  const [answers, setAnswers] = useState<Record<number, string>>({});
  const [revealed, setRevealed] = useState<Record<number, boolean>>({});

  function selectOption(order: number, label: string) {
    if (revealed[order]) return;
    setAnswers((prev) => ({ ...prev, [order]: label }));
  }

  function checkAnswer(order: number) {
    setRevealed((prev) => ({ ...prev, [order]: true }));
  }

  const answeredCount = Object.keys(revealed).length;
  const correctCount = questions.filter((q) => {
    const correct = q.options.find((o) => o.isCorrect)?.label;
    return revealed[q.order] && answers[q.order] === correct;
  }).length;

  return (
    <div className="exam">
      <div className="exam-scorebar">
        <span className="eyebrow">Prova · {questions.length} questões</span>
        {answeredCount > 0 && (
          <span className="exam-score">
            {correctCount} / {answeredCount} corretas até agora
          </span>
        )}
      </div>

      {questions.map((q) => {
        const isRevealed = !!revealed[q.order];
        const selected = answers[q.order];

        return (
          <div key={q.order} className="question-card">
            <div className="question-header">
              <span className="question-number">{String(q.order).padStart(2, '0')}</span>
              <p className="question-statement">{q.statement}</p>
            </div>

            <div className="question-options">
              {q.options.map((opt) => {
                const isSelected = selected === opt.label;
                let stateClass = '';
                if (isRevealed) {
                  if (opt.isCorrect) stateClass = 'option--correct';
                  else if (isSelected && !opt.isCorrect) stateClass = 'option--wrong';
                } else if (isSelected) {
                  stateClass = 'option--selected';
                }

                return (
                  <button
                    key={opt.label}
                    className={`question-option ${stateClass}`}
                    onClick={() => selectOption(q.order, opt.label)}
                    disabled={isRevealed}
                  >
                    <span className="option-label">{opt.label}</span>
                    <span>{opt.text}</span>
                  </button>
                );
              })}
            </div>

            <div className="question-footer">
              {!isRevealed ? (
                <button
                  className="btn btn-ghost"
                  onClick={() => checkAnswer(q.order)}
                  disabled={!selected}
                >
                  Conferir resposta
                </button>
              ) : (
                q.explanation && <p className="question-explanation">{q.explanation}</p>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
