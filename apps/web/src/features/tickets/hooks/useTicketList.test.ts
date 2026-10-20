import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  it('triggers an immediate request when changing a non-search filter like status', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
    });

    // An immediate request must be triggered without waiting for debounce timer
    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress', page: 1 }),
    );
  });

  it('combines dropdown filters and search, debouncing only search keystrokes and clearing immediately', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // 1. Changing priority triggers an immediate request
    await act(async () => {
      result.current.updateFilters({ priority: 'High' });
    });
    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ priority: 'High', page: 1 }),
    );

    // 2. Typing in search is debounced
    act(() => {
      result.current.updateFilters({ search: 'laptop' });
    });
    expect(getTickets).toHaveBeenCalledTimes(2);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    expect(getTickets).toHaveBeenCalledTimes(3);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ priority: 'High', search: 'laptop', page: 1 }),
    );

    // 3. Resetting filters clears immediately
    await act(async () => {
      result.current.resetFilters();
    });
    expect(getTickets).toHaveBeenCalledTimes(4);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ priority: undefined, search: '', page: 1 }),
    );
  });

  it('ignores accidental duplicate clicks on the same filter value', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    // First click: changes priority from undefined to High -> dispatches request
    await act(async () => {
      result.current.updateFilters({ priority: 'High' });
    });
    expect(getTickets).toHaveBeenCalledTimes(2);

    // Second click (accidental re-click of High): identical value -> no extra request
    await act(async () => {
      result.current.updateFilters({ priority: 'High' });
    });
    expect(getTickets).toHaveBeenCalledTimes(2);
  });
});
