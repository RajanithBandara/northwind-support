import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import { EscalationPanel } from './EscalationPanel';

describe('EscalationPanel', () => {
  it('blocks submission and does not call the API when reason is too short', async () => {
    const user = userEvent.setup();
    const escalateSpy = vi.spyOn(ticketsApi, 'escalateTicket');
    const onEscalated = vi.fn();
    const ticket = ticketDetail({ priority: 'Medium', status: 'Open' });

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });

    // Type 4 characters (less than minimum 5)
    await user.type(textarea, 'help');
    await user.click(submitButton);

    expect(
      screen.getByText(/reason must be at least 5 characters/i),
    ).toBeInTheDocument();
    expect(escalateSpy).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('shows 409 conflict error and leaves ticket unchanged when server rejects escalation', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    const ticket = ticketDetail({ priority: 'High', status: 'Open' });

    vi.spyOn(ticketsApi, 'escalateTicket').mockRejectedValue(
      new ApiError(409, 'Ticket is already at Critical priority and cannot be escalated.', {
        status: 409,
        title: 'Conflict',
        detail: 'Ticket is already at Critical priority and cannot be escalated.',
      }),
    );

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });

    await user.type(textarea, 'Urgent customer outage needing executive attention.');
    await user.click(submitButton);

    await waitFor(() => {
      expect(
        screen.getByRole('alert'),
      ).toHaveTextContent(/ticket is already at critical priority and cannot be escalated/i);
    });

    // onEscalated callback is NOT called, so the ticket remains unchanged
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('disables escalation control when ticket is Critical', () => {
    const ticket = ticketDetail({ priority: 'Critical', status: 'Open' });

    render(<EscalationPanel ticket={ticket} onEscalated={vi.fn()} />);

    expect(
      screen.getByText(/this ticket is already at critical priority and cannot be escalated/i),
    ).toBeInTheDocument();

    const textarea = screen.getByLabelText(/escalation reason/i);
    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });

    expect(textarea).toBeDisabled();
    expect(submitButton).toBeDisabled();
  });

  it('disables escalation control when ticket is Resolved or Closed', () => {
    const resolvedTicket = ticketDetail({ priority: 'Medium', status: 'Resolved' });

    const { rerender } = render(
      <EscalationPanel ticket={resolvedTicket} onEscalated={vi.fn()} />,
    );

    expect(
      screen.getByText(/this ticket is resolved and cannot be escalated/i),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /escalate ticket/i })).toBeDisabled();

    const closedTicket = ticketDetail({ priority: 'Medium', status: 'Closed' });
    rerender(<EscalationPanel ticket={closedTicket} onEscalated={vi.fn()} />);

    expect(
      screen.getByText(/this ticket is closed and cannot be escalated/i),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /escalate ticket/i })).toBeDisabled();
  });

  it('successfully escalates when valid reason is provided', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    const ticket = ticketDetail({ priority: 'Medium', status: 'Open' });
    const escalatedTicket = ticketDetail({ priority: 'High', status: 'Open' });

    const escalateSpy = vi
      .spyOn(ticketsApi, 'escalateTicket')
      .mockResolvedValue(escalatedTicket);

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });

    await user.type(textarea, 'Severity increased due to SLA approaching risk threshold.');
    await user.click(submitButton);

    await waitFor(() => {
      expect(escalateSpy).toHaveBeenCalledWith(ticket.id, {
        reason: 'Severity increased due to SLA approaching risk threshold.',
        escalatedBy: 'team-lead',
      });
      expect(onEscalated).toHaveBeenCalledWith(escalatedTicket);
    });
  });
});
