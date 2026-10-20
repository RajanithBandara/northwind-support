import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';
import { PriorityBadge } from './components/TicketBadges';
import { SlaCell } from './components/SlaCell';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card notice" role="status">
          <h2>Ticket Created: {created.reference}</h2>
          <p>
            Raised <strong>{created.reference}</strong> for {created.customer.name} ({created.customer.tier}).
          </p>

          <dl className="summary" style={{ margin: '16px 0' }}>
            <div>
              <dt>Priority</dt>
              <dd>
                <PriorityBadge priority={created.priority} />
                {created.triage?.priorityReason && (
                  <span className="summary__note">{created.triage.priorityReason}</span>
                )}
              </dd>
            </div>

            <div>
              <dt>SLA Due Date</dt>
              <dd>
                <SlaCell dueAtUtc={created.dueAtUtc} slaStatus={created.slaStatus} />
                {created.triage?.slaReason && (
                  <span className="summary__note">{created.triage.slaReason}</span>
                )}
              </dd>
            </div>

            <div>
              <dt>Assigned Agent</dt>
              <dd>
                {created.assignedAgent ? (
                  <span>{created.assignedAgent.fullName}</span>
                ) : (
                  <strong>Unassigned</strong>
                )}
                {created.triage?.assignmentReason && (
                  <span className="summary__note">{created.triage.assignmentReason}</span>
                )}
              </dd>
            </div>
          </dl>

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button type="button" className="button button--ghost" onClick={() => setCreated(undefined)}>
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
