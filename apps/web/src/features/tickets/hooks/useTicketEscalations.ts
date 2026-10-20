import { useCallback } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketEscalation } from '../../../types/api';

/** Loads escalation history for a ticket and refetches when the id changes or reload() is called. */
export function useTicketEscalations(ticketId: number) {
  const load = useCallback(async (): Promise<TicketEscalation[]> => {
    if (Number.isNaN(ticketId) || ticketId <= 0) {
      return [];
    }

    return ticketsApi.getEscalations(ticketId);
  }, [ticketId]);

  return useAsyncData(load, 'Unable to load escalation history.');
}
