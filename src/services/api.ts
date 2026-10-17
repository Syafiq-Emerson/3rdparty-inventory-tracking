import {
  DashboardSummary,
  DuplicateConflictError,
  InventoryItem,
  InventoryItemIdentification,
  InventoryMovement,
  IssueInventoryPayload,
  IssueInventoryResponse,
  Personnel,
  RegisterInventoryPayload,
  StoreLocation,
} from '../types/inventory';
import { dbEngine } from './db';

/**
 * Enterprise API Service Layer
 * Interfaces identically to ASP.NET Core Web API endpoints.
 * Simulates network latency (80-150ms) to ensure true asynchronous UX behavior.
 */

const delay = (ms = 90) => new Promise((resolve) => setTimeout(resolve, ms));

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000').replace(/\/+$/, '');

export class ApiRequestError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = 'ApiRequestError';
  }
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

async function getHttpErrorMessage(
  response: Response,
  requestName: string,
  responseText?: string
): Promise<string> {
  const fallback = `${requestName} failed (${response.status}${response.statusText ? ` ${response.statusText}` : ''}).`;
  const bodyText = responseText ?? await response.text();
  if (!bodyText.trim()) return fallback;

  let body: unknown;
  try {
    body = JSON.parse(bodyText);
  } catch {
    return bodyText.trim();
  }

  if (!isRecord(body)) return fallback;

  for (const key of ['message', 'title', 'detail']) {
    if (typeof body[key] === 'string' && body[key].trim()) {
      return body[key].trim();
    }
  }

  if (isRecord(body.errors)) {
    const validationMessages = Object.values(body.errors)
      .flatMap((value) => Array.isArray(value) ? value : [value])
      .filter((value): value is string => typeof value === 'string' && Boolean(value.trim()));
    if (validationMessages.length > 0) return validationMessages.join(' ');
  }

  return fallback;
}

function isInventoryItem(value: unknown): value is InventoryItem {
  return isRecord(value) &&
    typeof value.isIssued === 'boolean' &&
    ['id', 'serialNumber', 'tagNumber', 'locationId', 'locationCode', 'locationName',
      'registeredByPersonnelId', 'registeredByPersonnelCode', 'registeredByPersonnelName',
      'createdAt', 'updatedAt'].every((key) => typeof value[key] === 'string');
}

function isDuplicateConflictError(value: unknown): value is DuplicateConflictError {
  return isRecord(value) &&
    value.errorCode === 'DUPLICATE_IDENTIFIER' &&
    typeof value.message === 'string' &&
    (value.conflictingField === 'SerialNumber' || value.conflictingField === 'TagNumber') &&
    typeof value.conflictingValue === 'string' &&
    isInventoryItem(value.existingItem);
}

async function fetchJson<T>(
  url: string,
  requestName: string,
  options?: RequestInit
): Promise<T> {
  const response = await fetch(url, options);
  if (!response.ok) {
    throw new ApiRequestError(
      await getHttpErrorMessage(response, requestName),
      response.status
    );
  }
  return response.json() as Promise<T>;
}

export const inventoryApi = {
  // Dashboard Endpoints
  async getDashboardSummary(): Promise<DashboardSummary> {
    return fetchJson<DashboardSummary>(
      `${API_BASE_URL}/api/dashboard/summary`,
      'Dashboard request'
    );
  },

  // Inventory Endpoints
  async getInventory(locationId?: string): Promise<InventoryItem[]> {
    const query = new URLSearchParams();
    if (locationId) query.set('locationId', locationId);
    const queryString = query.toString();
    const url = `${API_BASE_URL}/api/inventory${queryString ? `?${queryString}` : ''}`;
    return fetchJson<InventoryItem[]>(url, 'Inventory request');
  },

  async getInventoryById(id: string): Promise<InventoryItem> {
    return fetchJson<InventoryItem>(
      `${API_BASE_URL}/api/inventory/${encodeURIComponent(id)}`,
      'Inventory item request'
    );
  },

  async identifyInventory(serialNumber: string, tagNumber: string): Promise<InventoryItemIdentification> {
    const params = new URLSearchParams({ serialNumber, tagNumber });
    return fetchJson<InventoryItemIdentification>(
      `${API_BASE_URL}/api/inventory/identify?${params.toString()}`,
      'Asset identification request'
    );
  },

  async issueInventory(payload: IssueInventoryPayload): Promise<IssueInventoryResponse> {
    return fetchJson<IssueInventoryResponse>(
      `${API_BASE_URL}/api/inventory/issue`,
      'Issue for packing request',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      }
    );
  },

  async searchInventory(query: string, locationId?: string): Promise<InventoryItem[]> {
    const params = new URLSearchParams({ q: query });
    if (locationId) params.set('locationId', locationId);
    return fetchJson<InventoryItem[]>(
      `${API_BASE_URL}/api/inventory/search?${params.toString()}`,
      'Inventory search request'
    );
  },

  async registerInventory(payload: RegisterInventoryPayload): Promise<InventoryItem> {
    const response = await fetch(`${API_BASE_URL}/api/inventory`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        serialNumber: payload.serialNumber,
        tagNumber: payload.tagNumber,
        storeLocationId: payload.storeLocationId,
        scanByPersonnelId: payload.scanByPersonnelId,
      }),
    });

    if (!response.ok) {
      const responseText = await response.text();
      let errorBody: unknown;
      try {
        errorBody = JSON.parse(responseText);
      } catch {
        errorBody = undefined;
      }

      if (isDuplicateConflictError(errorBody)) {
        throw errorBody;
      }

      throw new Error(await getHttpErrorMessage(
        response,
        'Inventory registration request',
        responseText
      ));
    }

    return response.json() as Promise<InventoryItem>;
  },

  async getInventoryHistory(itemId: string): Promise<InventoryMovement[]> {
    return fetchJson<InventoryMovement[]>(
      `${API_BASE_URL}/api/inventory/${encodeURIComponent(itemId)}/history`,
      'Inventory history request'
    );
  },

  // Store Location Endpoints
  async getLocations(activeOnly = false): Promise<StoreLocation[]> {
    const params = new URLSearchParams({ activeOnly: String(activeOnly) });
    return fetchJson<StoreLocation[]>(
      `${API_BASE_URL}/api/locations?${params.toString()}`,
      'Store locations request'
    );
  },

  async createLocation(code: string, name: string): Promise<StoreLocation> {
    return fetchJson<StoreLocation>(`${API_BASE_URL}/api/locations`, 'Location creation request', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ locationCode: code, locationName: name }),
    });
  },

  async updateLocation(id: string, code: string, name: string): Promise<StoreLocation> {
    return fetchJson<StoreLocation>(
      `${API_BASE_URL}/api/locations/${encodeURIComponent(id)}`,
      'Location update request',
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ locationCode: code, locationName: name }),
      }
    );
  },

  async toggleLocationStatus(id: string, isActive: boolean): Promise<StoreLocation> {
    return fetchJson<StoreLocation>(
      `${API_BASE_URL}/api/locations/${encodeURIComponent(id)}/status`,
      'Location status update request',
      {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isActive }),
      }
    );
  },

  // Personnel Endpoints
  async getPersonnel(activeOnly = false): Promise<Personnel[]> {
    const params = new URLSearchParams({ activeOnly: String(activeOnly) });
    return fetchJson<Personnel[]>(
      `${API_BASE_URL}/api/personnel?${params.toString()}`,
      'Personnel request'
    );
  },

  async createPersonnel(code: string, name: string): Promise<Personnel> {
    return fetchJson<Personnel>(`${API_BASE_URL}/api/personnel`, 'Personnel creation request', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ personnelCode: code, name }),
    });
  },

  async updatePersonnel(id: string, code: string, name: string): Promise<Personnel> {
    return fetchJson<Personnel>(
      `${API_BASE_URL}/api/personnel/${encodeURIComponent(id)}`,
      'Personnel update request',
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ personnelCode: code, name }),
      }
    );
  },

  async togglePersonnelStatus(id: string, isActive: boolean): Promise<Personnel> {
    return fetchJson<Personnel>(
      `${API_BASE_URL}/api/personnel/${encodeURIComponent(id)}/status`,
      'Personnel status update request',
      {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isActive }),
      }
    );
  },

  // Factory Database Reset
  async resetDatabase(): Promise<void> {
    await delay(150);
    dbEngine.resetDatabase();
  },
};
