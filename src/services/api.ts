import {
  AppVersion,
  DashboardSummary,
  DeployNotice,
  DuplicateConflictError,
  InventoryItem,
  InventoryItemIdentification,
  InventoryMovement,
  IssueInventoryPayload,
  IssueInventoryResponse,
  PagedResponse,
  Personnel,
  RegisterInventoryPayload,
  SignedInUser,
  StoreLocation,
} from '../types/inventory';

/**
 * Calls the ASP.NET Core Web API. Paths are relative: in development Vite proxies
 * /api to the backend, and in IIS both are served from the same origin.
 */

const BASE = '/api';
const DEFAULT_TIMEOUT_MS = 60_000;

const NETWORK_MESSAGE = "Can't reach the server. Check the network connection and try again.";
const TIMEOUT_MESSAGE = 'The server took too long to respond. Try again in a moment.';

export class ApiError extends Error {
  readonly response: { status: number; data: unknown };

  constructor(message: string, status: number, data: unknown) {
    super(message);
    this.name = 'ApiError';
    this.response = { status, data };
  }

  get status(): number {
    return this.response.status;
  }
}

/** Called when the server reports the session is gone, so the app can show the sign-in screen. */
let unauthorizedHandler: (() => void) | null = null;
export const onUnauthorized = (handler: (() => void) | null): void => {
  unauthorizedHandler = handler;
};

export const newIdempotencyKey = (): string =>
  globalThis.crypto?.randomUUID?.() ?? `k-${Date.now()}-${Math.random().toString(16).slice(2)}`;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

/** Reads the server's { error } body, then validation errors, then a ProblemDetails title. */
export function errorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (error instanceof ApiError) {
    const data = error.response.data;
    if (isRecord(data)) {
      for (const key of ['error', 'message', 'title', 'detail']) {
        const value = data[key];
        if (typeof value === 'string' && value.trim()) return value.trim();
      }
      if (isRecord(data.errors)) {
        const messages = Object.values(data.errors)
          .flatMap((value) => (Array.isArray(value) ? value : [value]))
          .filter((value): value is string => typeof value === 'string' && Boolean(value.trim()));
        if (messages.length > 0) return messages.join(' ');
      }
    }
    return error.message || fallback;
  }
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  idempotencyKey?: string;
  timeoutMs?: number;
  signal?: AbortSignal;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, idempotencyKey, timeoutMs = DEFAULT_TIMEOUT_MS, signal } = options;

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  if (signal) signal.addEventListener('abort', () => controller.abort(), { once: true });

  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (idempotencyKey) headers['Idempotency-Key'] = idempotencyKey;

  let response: Response;
  try {
    response = await fetch(`${BASE}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      cache: 'no-store',
      credentials: 'same-origin',
      signal: controller.signal,
    });
  } catch (cause) {
    const aborted = cause instanceof DOMException && cause.name === 'AbortError';
    throw new ApiError(aborted ? TIMEOUT_MESSAGE : NETWORK_MESSAGE, 0, undefined);
  } finally {
    clearTimeout(timeout);
  }

  const text = await response.text();
  let data: unknown;
  try {
    data = text ? JSON.parse(text) : undefined;
  } catch {
    data = text;
  }

  if (!response.ok) {
    if (response.status === 401) unauthorizedHandler?.();
    const message =
      isRecord(data) && typeof data.error === 'string'
        ? data.error
        : `Request failed (${response.status}).`;
    throw new ApiError(message, response.status, data);
  }

  return data as T;
}

export const isDuplicateConflictError = (value: unknown): value is DuplicateConflictError =>
  isRecord(value) &&
  value.errorCode === 'DUPLICATE_IDENTIFIER' &&
  typeof value.message === 'string' &&
  (value.conflictingField === 'SerialNumber' || value.conflictingField === 'TagNumber');

export const authApi = {
  login: (userName: string, password: string): Promise<SignedInUser> =>
    request<SignedInUser>('/auth/login', { method: 'POST', body: { userName, password } }),

  logout: (): Promise<void> => request<void>('/auth/logout', { method: 'POST' }),

  /** Resolves to null when nobody is signed in, rather than throwing. */
  async me(): Promise<SignedInUser | null> {
    try {
      return await request<SignedInUser>('/auth/me');
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return null;
      throw error;
    }
  },

  changePassword: (currentPassword: string, newPassword: string): Promise<SignedInUser> =>
    request<SignedInUser>('/auth/change-password', {
      method: 'POST',
      body: { currentPassword, newPassword },
    }),
};

export const systemApi = {
  /** Liveness probe used by the boot overlay. Short timeout so a dead server is obvious. */
  health: (): Promise<{ status: string }> => request('/health', { timeoutMs: 8_000 }),

  version: (): Promise<AppVersion> => request('/system/version'),

  /** null (204) when no update is scheduled. Short timeout: it is polled every few seconds. */
  deployNotice: async (): Promise<DeployNotice | null> =>
    (await request<DeployNotice | undefined>('/system/deploy-notice', { timeoutMs: 10_000 })) ?? null,
};

export const inventoryApi = {
  getDashboardSummary: (): Promise<DashboardSummary> => request('/dashboard/summary'),

  getInventory: (locationId?: string, page = 1, pageSize = 50): Promise<PagedResponse<InventoryItem>> => {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (locationId) query.set('locationId', locationId);
    return request(`/inventory?${query.toString()}`);
  },

  getInventoryById: (id: string): Promise<InventoryItem> =>
    request(`/inventory/${encodeURIComponent(id)}`),

  identifyInventory: (serialNumber: string, tagNumber: string): Promise<InventoryItemIdentification> =>
    request(`/inventory/identify?${new URLSearchParams({ serialNumber, tagNumber }).toString()}`),

  issueInventory: (payload: IssueInventoryPayload): Promise<IssueInventoryResponse> =>
    request('/inventory/issue', {
      method: 'POST',
      body: payload,
      idempotencyKey: newIdempotencyKey(),
    }),

  searchInventory: (query: string, locationId?: string): Promise<PagedResponse<InventoryItem>> => {
    const params = new URLSearchParams({ q: query });
    if (locationId) params.set('locationId', locationId);
    return request(`/inventory/search?${params.toString()}`);
  },

  async registerInventory(payload: RegisterInventoryPayload): Promise<InventoryItem> {
    try {
      return await request<InventoryItem>('/inventory', {
        method: 'POST',
        body: payload,
        idempotencyKey: newIdempotencyKey(),
      });
    } catch (error) {
      // A duplicate serial or tag is a structured body the register screen renders itself.
      if (error instanceof ApiError && isDuplicateConflictError(error.response.data)) {
        throw error.response.data;
      }
      throw error;
    }
  },

  getInventoryHistory: (itemId: string): Promise<InventoryMovement[]> =>
    request(`/inventory/${encodeURIComponent(itemId)}/history`),

  getLocations: (activeOnly = false): Promise<StoreLocation[]> =>
    request(`/locations?${new URLSearchParams({ activeOnly: String(activeOnly) }).toString()}`),

  createLocation: (locationCode: string, locationName: string): Promise<StoreLocation> =>
    request('/locations', { method: 'POST', body: { locationCode, locationName } }),

  updateLocation: (id: string, locationCode: string, locationName: string): Promise<StoreLocation> =>
    request(`/locations/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: { locationCode, locationName },
    }),

  toggleLocationStatus: (id: string, isActive: boolean): Promise<StoreLocation> =>
    request(`/locations/${encodeURIComponent(id)}/status`, { method: 'PATCH', body: { isActive } }),

  getPersonnel: (activeOnly = false): Promise<Personnel[]> =>
    request(`/personnel?${new URLSearchParams({ activeOnly: String(activeOnly) }).toString()}`),

  createPersonnel: (personnelCode: string, name: string): Promise<Personnel> =>
    request('/personnel', { method: 'POST', body: { personnelCode, name } }),

  updatePersonnel: (id: string, personnelCode: string, name: string): Promise<Personnel> =>
    request(`/personnel/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: { personnelCode, name },
    }),

  togglePersonnelStatus: (id: string, isActive: boolean): Promise<Personnel> =>
    request(`/personnel/${encodeURIComponent(id)}/status`, { method: 'PATCH', body: { isActive } }),
};
