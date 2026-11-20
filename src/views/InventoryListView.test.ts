import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { InventoryItem } from '../types/inventory';

const { useAsyncMock } = vi.hoisted(() => ({ useAsyncMock: vi.fn() }));

vi.mock('../hooks/useAsync', () => ({ useAsync: useAsyncMock }));
vi.mock('../services/api', () => ({
  errorMessage: vi.fn(),
  inventoryApi: { getLocations: vi.fn() },
}));

import { InventoryListView } from './InventoryListView';

describe('InventoryListView aging column', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('places Aging (Days) immediately after Registration Date', () => {
    const markup = renderItems([makeItem(40)]);
    const headers = [...markup.matchAll(/<th[^>]*>(.*?)<\/th>/g)]
      .map(([, content]) => content.replace(/<[^>]+>/g, '').trim());

    expect(headers).toEqual([
      'Serial Number',
      'Tag Number',
      'Store Location',
      'Availability',
      'Responsible Personnel',
      'Registration Date',
      'Aging (Days)',
      'Actions',
    ]);
  });

  it('renders accumulated API totals for stored and issued items, including zero', () => {
    const markup = renderItems([
      makeItem(40, false),
      makeItem(37, true),
      makeItem(0, false),
    ]);

    expect(agingValues(markup)).toEqual(['40', '37', '0']);
    expect(markup).toContain('Details');
  });

  it('renders search results with the same API total field', () => {
    const markup = renderItems([makeItem(23)]);

    expect(agingValues(markup)).toEqual(['23']);
  });

  it('uses an em dash for null or missing totals', () => {
    const nullTotal = makeItem(0, false, 'null-total');
    Reflect.set(nullTotal, 'totalStorageDays', null);

    const missingTotal = makeItem(0, false, 'missing-total');
    Reflect.deleteProperty(missingTotal, 'totalStorageDays');

    expect(agingValues(renderItems([nullTotal, missingTotal]))).toEqual(['—', '—']);
  });

  it('keeps totals attached to every row when multiple pages are combined', () => {
    const markup = renderItems([makeItem(40), makeItem(37, true)], true);

    expect(agingValues(markup)).toEqual(['40', '37']);
    expect(markup).toContain('Load more');
  });
});

function renderItems(items: InventoryItem[], hasMore = false): string {
  let useAsyncCalls = 0;
  useAsyncMock.mockImplementation(() => {
    useAsyncCalls += 1;
    return useAsyncCalls === 1
      ? { data: [], error: null, loading: false, reload: vi.fn() }
      : { data: { items, hasMore }, error: null, loading: false, reload: vi.fn() };
  });

  return renderToStaticMarkup(
    React.createElement(InventoryListView, {
      onInspectItem: vi.fn(),
      onNavigateToRegister: vi.fn(),
    }),
  );
}

function agingValues(markup: string): string[] {
  return [...markup.matchAll(
    /<td class="py-3 px-4 text-right font-mono tabular-nums text-slate-700 whitespace-nowrap">([^<]+)<\/td>/g,
  )].map(([, value]) => value);
}

function makeItem(totalStorageDays: number, isIssued = false, id = `item-${totalStorageDays}-${isIssued}`): InventoryItem {
  return {
    id,
    serialNumber: 'SN-1',
    tagNumber: 'TAG-1',
    locationId: 'location-1',
    locationCode: 'WH-A',
    locationName: 'Warehouse A',
    registeredByPersonnelId: 'person-1',
    registeredByPersonnelCode: 'P-1',
    registeredByPersonnelName: 'Operator One',
    isIssued,
    currentStorageCycleId: isIssued ? null : 'cycle-1',
    currentAgingDays: isIssued ? null : 3,
    totalStorageDays,
    createdAt: '2026-01-01T10:00:00Z',
    updatedAt: '2026-01-01T10:00:00Z',
  };
}
