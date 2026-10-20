import { Badge, type BadgeTone } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';

interface SlaIndicatorConfig {
  text: string;
  tone: BadgeTone;
  icon: string;
}

const slaConfigs: Record<SlaStatus, SlaIndicatorConfig> = {
  WithinSla: { text: 'Within SLA', tone: 'info', icon: '●' },
  AtRisk: { text: 'At risk', tone: 'warning', icon: '▲' },
  Breached: { text: 'Breached', tone: 'danger', icon: '✕' },
  Met: { text: 'Met', tone: 'success', icon: '✓' },
  NotApplicable: { text: 'No SLA', tone: 'neutral', icon: '—' },
};

export interface SlaIndicatorProps {
  status: SlaStatus;
}

/** Reusable SLA indicator for all 5 SLA states, conveying state by icon and text, not color alone. */
export function SlaIndicator({ status }: SlaIndicatorProps) {
  const config = slaConfigs[status] ?? { text: status, tone: 'neutral' as BadgeTone, icon: '—' };

  return (
    <Badge tone={config.tone}>
      <span aria-hidden="true" style={{ marginRight: '0.25rem' }}>
        {config.icon}
      </span>
      <span>{config.text}</span>
    </Badge>
  );
}

interface SlaCellProps {
  dueAtUtc: string | null;
  slaStatus: SlaStatus;
}

/** The due date and where the ticket stands against it, as shown in the ticket list and details. */
export function SlaCell({ dueAtUtc, slaStatus }: SlaCellProps) {
  return (
    <div className="sla-cell">
      <SlaIndicator status={slaStatus} />
      <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}
