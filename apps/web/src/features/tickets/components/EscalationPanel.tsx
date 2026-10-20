import { useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { TicketDetail } from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (updatedTicket: TicketDetail) => void;
}

export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [clientError, setClientError] = useState<string>();
  const [serverError, setServerError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isCritical = ticket.priority === 'Critical';
  const isResolvedOrClosed = ticket.status === 'Resolved' || ticket.status === 'Closed';
  const canEscalate = !isCritical && !isResolvedOrClosed;

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setServerError(undefined);
    setClientError(undefined);

    const trimmedReason = reason.trim();

    if (trimmedReason.length < 5) {
      setClientError('Reason must be at least 5 characters.');
      return;
    }

    if (trimmedReason.length > 500) {
      setClientError('Reason cannot exceed 500 characters.');
      return;
    }

    setIsSubmitting(true);

    try {
      const updated = await ticketsApi.escalateTicket(ticket.id, {
        reason: trimmedReason,
      });

      setReason('');
      onEscalated(updated);
    } catch (err) {
      setServerError(toErrorMessage(err, 'Failed to escalate ticket.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="card">
      <h2>Escalate ticket</h2>

      {!canEscalate && (
        <p className="summary__note" role="note">
          {isCritical
            ? 'This ticket is already at Critical priority and cannot be escalated.'
            : `This ticket is ${ticket.status} and cannot be escalated.`}
        </p>
      )}

      <form onSubmit={handleSubmit} noValidate>
        <Field
          id="escalate-reason"
          label="Escalation reason"
          error={clientError}
          hint="Explain why priority is being raised (5-500 characters)."
        >
          {(fieldProps) => (
            <textarea
              {...fieldProps}
              rows={3}
              value={reason}
              disabled={!canEscalate || isSubmitting}
              placeholder="Provide context for this escalation..."
              onChange={(e) => {
                setReason(e.target.value);
                if (clientError) setClientError(undefined);
              }}
            />
          )}
        </Field>

        {serverError && (
          <p className="field__error" role="alert" style={{ marginBottom: '12px' }}>
            {serverError}
          </p>
        )}

        <div className="button-row">
          <button
            type="submit"
            className="button button--primary"
            disabled={!canEscalate || isSubmitting}
          >
            {isSubmitting ? 'Escalating ticket...' : 'Escalate ticket'}
          </button>
        </div>
      </form>
    </div>
  );
}
