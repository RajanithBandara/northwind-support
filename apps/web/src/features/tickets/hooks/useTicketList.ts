import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({ isLoading: true });
  const searchRef = useRef(filters.search);

  const load = useCallback(async (currentFilters: TicketFilters) => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(currentFilters);
      setState({ isLoading: false, data, error: undefined });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
      setState((current) => ({ ...current, isLoading: false, error: message }));
    }
  }, []);

  useEffect(() => {
    const isSearchChange = searchRef.current !== filters.search;
    searchRef.current = filters.search;

    if (isSearchChange) {
      const handle = window.setTimeout(() => {
        void load(filters);
      }, DEBOUNCE_MS);

      return () => window.clearTimeout(handle);
    }

    void load(filters);
  }, [filters, load]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => {
      const hasChange = Object.entries(patch).some(
        ([key, value]) => current[key as keyof TicketFilters] !== value,
      );
      if (!hasChange) {
        return current;
      }

      return { ...current, ...patch, page: patch.page ?? 1 };
    });

  const resetFilters = () => {
    searchRef.current = emptyFilters.search;
    setFilters(emptyFilters);
  };

  return { filters, updateFilters, resetFilters, reload: () => load(filters), ...state };
}
