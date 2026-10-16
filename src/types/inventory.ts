/**
 * Industrial Inventory Management System
 * Shared TypeScript Data Contracts & Interfaces
 * Matches C# ASP.NET Core DTOs and MySQL 8.0 schema
 */

export interface StoreLocation {
  id: string;
  locationCode: string;
  locationName: string;
  isActive: boolean;
  itemCount?: number;
  createdAt: string;
  updatedAt: string;
}

export interface Personnel {
  id: string;
  personnelCode: string;
  name: string;
  isActive: boolean;
  registeredItemsCount?: number;
  createdAt: string;
  updatedAt: string;
}

export interface InventoryItem {
  id: string;
  serialNumber: string;
  tagNumber: string;
  locationId: string;
  locationCode: string;
  locationName: string;
  registeredByPersonnelId: string;
  registeredByPersonnelCode: string;
  registeredByPersonnelName: string;
  createdAt: string;
  updatedAt: string;
}

export interface InventoryMovement {
  id: string;
  inventoryItemId: string;
  fromLocationId: string | null;
  fromLocationName: string | null;
  toLocationId: string;
  toLocationName: string;
  performedByPersonnelId: string;
  performedByPersonnelName: string;
  eventType: 'INITIAL_REGISTRATION' | 'TRANSFER' | 'AUDIT';
  movedAt: string;
}

export interface RegisterInventoryPayload {
  serialNumber: string;
  tagNumber: string;
  storeLocationId: string;
  scanByPersonnelId: string;
}

export interface DuplicateConflictError {
  errorCode: 'DUPLICATE_IDENTIFIER';
  message: string;
  conflictingField: 'SerialNumber' | 'TagNumber';
  conflictingValue: string;
  existingItem: InventoryItem;
}

export interface LocationInventoryCount {
  locationId: string;
  locationCode: string;
  locationName: string;
  itemCount: number;
  isActive: boolean;
}

export interface DashboardSummary {
  totalRegisteredUnits: number;
  activeStoreLocationsCount: number;
  activePersonnelCount: number;
  inventoryByStoreLocation: LocationInventoryCount[];
  recentlyRegisteredInventory: InventoryItem[];
}

export type ScanClassificationType = 'SERIAL_NUMBER' | 'TAG_NUMBER' | 'UNKNOWN';

export interface ScanClassificationResult {
  type: ScanClassificationType;
  confidence: number;
  raw: string;
  reason: string;
}
