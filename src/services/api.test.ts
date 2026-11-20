import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ApiError,
  authApi,
  errorMessage,
  inventoryApi,
  isDuplicateConflictError,
  newIdempotencyKey,
} from './api';

describe('errorMessage', () => {
  it('reads the server error envelope first', () => {
    const error = new ApiError('Request failed (409).', 409, { error: 'Location code already exists.' });
    expect(errorMessage(error)).toBe('Location code already exists.');
  });

  it('joins model validation errors when there is no envelope', () => {
    const error = new ApiError('Request failed (400).', 400, {
      errors: { SerialNumber: ['Serial Number is required.'], TagNumber: ['Tag Number is required.'] },
    });
    expect(errorMessage(error)).toBe('Serial Number is required. Tag Number is required.');
  });

  it('falls back to the ProblemDetails title', () => {
    expect(errorMessage(new ApiError('x', 400, { title: 'Bad request' }))).toBe('Bad request');
  });

  it('uses the error message, then the fallback', () => {
    expect(errorMessage(new Error('Network down'))).toBe('Network down');
    expect(errorMessage('not an error', 'Try again.')).toBe('Try again.');
  });

  it('exposes the status for callers that branch on it', () => {
    expect(new ApiError('x', 404, undefined).status).toBe(404);
  });
});

describe('isDuplicateConflictError', () => {
  const conflict = {
    errorCode: 'DUPLICATE_IDENTIFIER',
    message: "Serial Number 'F1' is already registered in the system.",
    conflictingField: 'SerialNumber',
    conflictingValue: 'F1',
    existingItem: {},
  };

  it('recognises the duplicate body the register endpoint returns', () => {
    expect(isDuplicateConflictError(conflict)).toBe(true);
  });

  it('rejects anything else', () => {
    expect(isDuplicateConflictError({ ...conflict, errorCode: 'OTHER' })).toBe(false);
    expect(isDuplicateConflictError({ ...conflict, conflictingField: 'Name' })).toBe(false);
    expect(isDuplicateConflictError({ error: 'x' })).toBe(false);
    expect(isDuplicateConflictError(null)).toBe(false);
  });
});

describe('inventory aging list contracts', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('preserves backend totals for later pages and search results', async () => {
    const calls: string[] = [];
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL): Promise<Response> => {
        const url = new URL(String(input), 'http://localhost');
        calls.push(`${url.pathname}${url.search}`);

        const body = url.pathname.endsWith('/search')
          ? { items: [{ totalStorageDays: 37, isIssued: true }], hasMore: false }
          : { items: [{ totalStorageDays: 40, isIssued: false }], hasMore: true };
        return new Response(JSON.stringify(body), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }),
    );

    const laterPage = await inventoryApi.getInventory('location-1', 2, 50);
    const serialSearch = await inventoryApi.searchInventory('SN-1', 'location-1');
    const tagSearch = await inventoryApi.searchInventory('TAG-1', 'location-1');

    expect(calls[0]).toContain('/api/inventory?page=2&pageSize=50&locationId=location-1');
    expect(calls[1]).toContain('/api/inventory/search?q=SN-1&locationId=location-1');
    expect(calls[2]).toContain('/api/inventory/search?q=TAG-1&locationId=location-1');
    expect(laterPage.items[0].totalStorageDays).toBe(40);
    expect(serialSearch.items[0].totalStorageDays).toBe(37);
    expect(tagSearch.items[0].totalStorageDays).toBe(37);
  });
});

describe('newIdempotencyKey', () => {
  it('returns a different key on every call that fits the 64-character column', () => {
    const keys = new Set(Array.from({ length: 100 }, () => newIdempotencyKey()));
    expect(keys.size).toBe(100);
    for (const key of keys) expect(key.length).toBeLessThanOrEqual(64);
  });

  describe('antiforgery request handling', () => {
    afterEach(() => {
      vi.unstubAllGlobals();
    });

    it('fetches a token for unsafe requests, refreshes after auth changes, and retries one token failure', async () => {
      const calls: Array<{
        path: string;
        method: string;
        headers: Headers;
        body: string | undefined;
        credentials: RequestCredentials | undefined;
      }> = [];
      let tokenNumber = 0;
      let rejectFirstPasswordChange = true;
      const fetchMock = vi.fn(
        async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
          const url = new URL(String(input), 'http://localhost');
          const method = init?.method ?? 'GET';
          const headers = new Headers(init?.headers);
          const call = {
            path: `${url.pathname}${url.search}`,
            method,
            headers,
            body: typeof init?.body === 'string' ? init.body : undefined,
            credentials: init?.credentials,
          };
          calls.push(call);

          if (url.pathname === '/api/auth/csrf-token') {
            tokenNumber++;
            return jsonResponse({ requestToken: `token-${tokenNumber}` });
          }
          if (url.pathname === '/api/auth/change-password' && rejectFirstPasswordChange) {
            rejectFirstPasswordChange = false;
            return jsonResponse(
              { error: 'The anti-forgery token is missing or invalid.', errorCode: 'CSRF_VALIDATION_FAILED' },
              400,
            );
          }
          if (url.pathname === '/api/auth/logout') return new Response(null, { status: 204 });
          return jsonResponse({ id: 'user-1', userName: 'admin', role: 'Admin', mustChangePassword: false });
        },
      );
      vi.stubGlobal('fetch', fetchMock);

      await inventoryApi.identifyInventory('SN-1', 'TAG-1');
      await inventoryApi.issueInventory({
        serialNumber: 'SN-1',
        tagNumber: 'TAG-1',
        performedByPersonnelId: 'person-1',
      });
      await authApi.login('admin', 'correct horse battery');
      await inventoryApi.issueInventory({
        serialNumber: 'SN-1',
        tagNumber: 'TAG-1',
        performedByPersonnelId: 'person-1',
      });
      await authApi.changePassword('old password', 'new password long enough');
      await authApi.logout();

      const tokenCalls = calls.filter((call) => call.path === '/api/auth/csrf-token');
      expect(tokenCalls).toHaveLength(5);
      expect(calls[0].method).toBe('GET');
      expect(calls[0].headers.has('X-CSRF-TOKEN')).toBe(false);

      const unsafeCalls = calls.filter((call) => call.method !== 'GET');
      expect(unsafeCalls.every((call) => call.credentials === 'same-origin')).toBe(true);
      expect(unsafeCalls.map((call) => call.headers.get('X-CSRF-TOKEN'))).toEqual([
        'token-1',
        'token-2',
        'token-3',
        'token-3',
        'token-4',
        'token-5',
      ]);

      const passwordCalls = calls.filter((call) => call.path === '/api/auth/change-password');
      expect(passwordCalls).toHaveLength(2);
      expect(passwordCalls[0].body).toBe(passwordCalls[1].body);
      expect(calls.every((call) => !call.path.includes('token-'))).toBe(true);
    });

    it('sends Return to Storage with the caller-supplied retry key', async () => {
      const calls: Array<{ path: string; method: string; headers: Headers }> = [];
      vi.stubGlobal(
        'fetch',
        vi.fn(async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
          const url = new URL(String(input), 'http://localhost');
          const call = {
            path: `${url.pathname}${url.search}`,
            method: init?.method ?? 'GET',
            headers: new Headers(init?.headers),
          };
          calls.push(call);
          if (url.pathname === '/api/auth/csrf-token') {
            return jsonResponse({ requestToken: 'csrf-token' });
          }
          return jsonResponse({}, 201);
        }),
      );

      await inventoryApi.returnToStorage(
        {
          serialNumber: 'SN-1',
          tagNumber: 'TAG-1',
          storeLocationId: 'location-1',
          performedByPersonnelId: 'person-1',
        },
        'return-retry-key',
      );

      const post = calls.find((call) => call.path === '/api/inventory/return-to-storage');
      expect(post?.method).toBe('POST');
      expect(post?.headers.get('Idempotency-Key')).toBe('return-retry-key');
      expect(post?.headers.get('X-CSRF-TOKEN')).toBe('csrf-token');
    });
  });

  function jsonResponse(body: unknown, status = 200): Response {
    return new Response(JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    });
  }
});
