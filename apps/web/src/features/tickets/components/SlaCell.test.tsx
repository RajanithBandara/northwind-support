import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaCell';

describe('SlaIndicator', () => {
  const states: { status: SlaStatus; expectedText: string; expectedTone: string; expectedIcon: string }[] = [
    { status: 'WithinSla', expectedText: 'Within SLA', expectedTone: 'badge--info', expectedIcon: '●' },
    { status: 'AtRisk', expectedText: 'At risk', expectedTone: 'badge--warning', expectedIcon: '▲' },
    { status: 'Breached', expectedText: 'Breached', expectedTone: 'badge--danger', expectedIcon: '✕' },
    { status: 'Met', expectedText: 'Met', expectedTone: 'badge--success', expectedIcon: '✓' },
    { status: 'NotApplicable', expectedText: 'No SLA', expectedTone: 'badge--neutral', expectedIcon: '—' },
  ];

  it.each(states)(
    'renders $status distinguishably with label "$expectedText", tone "$expectedTone", and icon "$expectedIcon"',
    ({ status, expectedText, expectedTone, expectedIcon }) => {
      const { container } = render(<SlaIndicator status={status} />);

      expect(screen.getByText(expectedText)).toBeInTheDocument();
      expect(screen.getByText(expectedIcon)).toBeInTheDocument();

      const badge = container.querySelector('.badge');
      expect(badge).not.toBeNull();
      expect(badge).toHaveClass(expectedTone);
    },
  );
});
