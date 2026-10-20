import { formatDateTime } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  escalations: TicketEscalation[];
  isLoading?: boolean;
}

export function EscalationHistory({ escalations, isLoading = false }: EscalationHistoryProps) {
  return (
    <div className="card">
      <h2>Escalation history</h2>

      {isLoading && <p className="summary__note">Loading history...</p>}

      {!isLoading && escalations.length === 0 && (
        <p className="summary__note">No escalations recorded for this ticket.</p>
      )}

      {escalations.length > 0 && (
        <div className="escalation-list">
          {escalations.map((item) => (
            <div
              key={item.id}
              className="card"
              style={{
                marginBottom: '12px',
                padding: '12px',
                backgroundColor: 'var(--colour-page)',
              }}
            >
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginBottom: '8px',
                }}
              >
                <div>
                  <strong>{item.escalatedBy}</strong>
                  <span className="summary__note" style={{ marginLeft: '8px' }}>
                    {formatDateTime(item.escalatedAtUtc)}
                  </span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <PriorityBadge priority={item.fromPriority} />
                  <span>→</span>
                  <PriorityBadge priority={item.toPriority} />
                </div>
              </div>

              <p style={{ margin: '8px 0', fontStyle: 'italic' }}>"{item.reason}"</p>

              <dl
                className="summary"
                style={{
                  gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                  fontSize: '0.85rem',
                }}
              >
                <div>
                  <dt>Agent</dt>
                  <dd>
                    {item.fromAgent?.fullName ?? 'Unassigned'} → {item.toAgent?.fullName ?? 'Unassigned'}
                  </dd>
                </div>
                <div>
                  <dt>Due date</dt>
                  <dd>
                    {formatDateTime(item.fromDueAtUtc)} → {formatDateTime(item.toDueAtUtc)}
                  </dd>
                </div>
              </dl>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
