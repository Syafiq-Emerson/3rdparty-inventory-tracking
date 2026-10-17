import {
  StoreLocation,
  Personnel,
  InventoryItem,
  InventoryMovement,
  RegisterInventoryPayload,
  DuplicateConflictError,
  DashboardSummary,
} from '../types/inventory';

const STORAGE_KEYS = {
  LOCATIONS: 'industrial_inv_store_locations',
  PERSONNEL: 'industrial_inv_personnel',
  ITEMS: 'industrial_inv_items',
  MOVEMENTS: 'industrial_inv_movements',
  SEEDED: 'industrial_inv_seeded_v1',
};

// Initial Enterprise Baseline Seed Data
const INITIAL_LOCATIONS: StoreLocation[] = [
  {
    id: 'loc-001',
    locationCode: 'WH-A-01',
    locationName: 'Warehouse A - Bay 1 (Heavy Machinery & Actuators)',
    isActive: true,
    createdAt: '2026-03-01T08:00:00.000Z',
    updatedAt: '2026-03-01T08:00:00.000Z',
  },
  {
    id: 'loc-002',
    locationCode: 'WH-B-03',
    locationName: 'Warehouse B - Rack 3 (Valves & Regulators)',
    isActive: true,
    createdAt: '2026-03-01T08:00:00.000Z',
    updatedAt: '2026-03-01T08:00:00.000Z',
  },
  {
    id: 'loc-003',
    locationCode: 'CAL-LAB-01',
    locationName: 'Calibration Lab - Clean Room A',
    isActive: true,
    createdAt: '2026-03-02T09:15:00.000Z',
    updatedAt: '2026-03-02T09:15:00.000Z',
  },
  {
    id: 'loc-004',
    locationCode: 'FAB-SHOP-2',
    locationName: 'Fabrication Shop - Assembly Zone 2',
    isActive: true,
    createdAt: '2026-03-03T11:30:00.000Z',
    updatedAt: '2026-03-03T11:30:00.000Z',
  },
  {
    id: 'loc-005',
    locationCode: 'HOLD-INSP',
    locationName: 'Receiving Dock - Quarantine & Inspection',
    isActive: true,
    createdAt: '2026-03-05T07:45:00.000Z',
    updatedAt: '2026-03-05T07:45:00.000Z',
  },
  {
    id: 'loc-006',
    locationCode: 'DECOMM-BAY',
    locationName: 'Decommissioned Equipment Holding',
    isActive: false,
    createdAt: '2026-02-15T14:00:00.000Z',
    updatedAt: '2026-02-15T14:00:00.000Z',
  },
];

const INITIAL_PERSONNEL: Personnel[] = [
  {
    id: 'per-001',
    personnelCode: 'EMP-1042',
    name: 'Marcus Vance (Lead Instrumentation Tech)',
    isActive: true,
    createdAt: '2026-01-10T08:00:00.000Z',
    updatedAt: '2026-01-10T08:00:00.000Z',
  },
  {
    id: 'per-002',
    personnelCode: 'EMP-1189',
    name: 'Elena Rostova (QA & Compliance Specialist)',
    isActive: true,
    createdAt: '2026-01-12T08:30:00.000Z',
    updatedAt: '2026-01-12T08:30:00.000Z',
  },
  {
    id: 'per-003',
    personnelCode: 'EMP-2055',
    name: 'David Kim (Materials Handling Supervisor)',
    isActive: true,
    createdAt: '2026-02-01T09:00:00.000Z',
    updatedAt: '2026-02-01T09:00:00.000Z',
  },
  {
    id: 'per-004',
    personnelCode: 'EMP-3120',
    name: 'Sarah Jenkins (Calibration Engineer)',
    isActive: true,
    createdAt: '2026-02-10T10:00:00.000Z',
    updatedAt: '2026-02-10T10:00:00.000Z',
  },
  {
    id: 'per-005',
    personnelCode: 'EMP-9001',
    name: 'Arthur Pendelton (Contractor - Inactive)',
    isActive: false,
    createdAt: '2025-11-01T08:00:00.000Z',
    updatedAt: '2026-01-01T08:00:00.000Z',
  },
];

const INITIAL_ITEMS: InventoryItem[] = [
  {
    id: 'item-001',
    serialNumber: 'F005327261',
    tagNumber: 'PV-16501',
    locationId: 'loc-002',
    locationCode: 'WH-B-03',
    locationName: 'Warehouse B - Rack 3 (Valves & Regulators)',
    registeredByPersonnelId: 'per-001',
    registeredByPersonnelCode: 'EMP-1042',
    registeredByPersonnelName: 'Marcus Vance (Lead Instrumentation Tech)',
    isIssued: false,
    createdAt: '2026-10-10T09:24:12.000Z',
    updatedAt: '2026-10-10T09:24:12.000Z',
  },
  {
    id: 'item-002',
    serialNumber: 'F005327262',
    tagNumber: 'FT-10402',
    locationId: 'loc-001',
    locationCode: 'WH-A-01',
    locationName: 'Warehouse A - Bay 1 (Heavy Machinery & Actuators)',
    registeredByPersonnelId: 'per-002',
    registeredByPersonnelCode: 'EMP-1189',
    registeredByPersonnelName: 'Elena Rostova (QA & Compliance Specialist)',
    isIssued: false,
    createdAt: '2026-10-11T14:15:30.000Z',
    updatedAt: '2026-10-11T14:15:30.000Z',
  },
  {
    id: 'item-003',
    serialNumber: 'F005327289',
    tagNumber: 'LT-20415',
    locationId: 'loc-003',
    locationCode: 'CAL-LAB-01',
    locationName: 'Calibration Lab - Clean Room A',
    registeredByPersonnelId: 'per-004',
    registeredByPersonnelCode: 'EMP-3120',
    registeredByPersonnelName: 'Sarah Jenkins (Calibration Engineer)',
    isIssued: false,
    createdAt: '2026-10-12T11:05:44.000Z',
    updatedAt: '2026-10-12T11:05:44.000Z',
  },
  {
    id: 'item-004',
    serialNumber: 'S991048210',
    tagNumber: 'PT-33018',
    locationId: 'loc-004',
    locationCode: 'FAB-SHOP-2',
    locationName: 'Fabrication Shop - Assembly Zone 2',
    registeredByPersonnelId: 'per-003',
    registeredByPersonnelCode: 'EMP-2055',
    registeredByPersonnelName: 'David Kim (Materials Handling Supervisor)',
    isIssued: false,
    createdAt: '2026-10-14T16:40:10.000Z',
    updatedAt: '2026-10-14T16:40:10.000Z',
  },
  {
    id: 'item-005',
    serialNumber: 'S991048211',
    tagNumber: 'TT-40291',
    locationId: 'loc-005',
    locationCode: 'HOLD-INSP',
    locationName: 'Receiving Dock - Quarantine & Inspection',
    registeredByPersonnelId: 'per-001',
    registeredByPersonnelCode: 'EMP-1042',
    registeredByPersonnelName: 'Marcus Vance (Lead Instrumentation Tech)',
    isIssued: false,
    createdAt: '2026-10-15T08:12:00.000Z',
    updatedAt: '2026-10-15T08:12:00.000Z',
  },
];

const INITIAL_MOVEMENTS: InventoryMovement[] = [
  {
    id: 'mov-001',
    inventoryItemId: 'item-001',
    fromLocationId: null,
    fromLocationName: null,
    toLocationId: 'loc-002',
    toLocationName: 'Warehouse B - Rack 3 (Valves & Regulators)',
    performedByPersonnelId: 'per-001',
    performedByPersonnelName: 'Marcus Vance (Lead Instrumentation Tech)',
    eventType: 'INITIAL_REGISTRATION',
    movedAt: '2026-10-10T09:24:12.000Z',
  },
  {
    id: 'mov-002',
    inventoryItemId: 'item-002',
    fromLocationId: null,
    fromLocationName: null,
    toLocationId: 'loc-001',
    toLocationName: 'Warehouse A - Bay 1 (Heavy Machinery & Actuators)',
    performedByPersonnelId: 'per-002',
    performedByPersonnelName: 'Elena Rostova (QA & Compliance Specialist)',
    eventType: 'INITIAL_REGISTRATION',
    movedAt: '2026-10-11T14:15:30.000Z',
  },
  {
    id: 'mov-003',
    inventoryItemId: 'item-003',
    fromLocationId: null,
    fromLocationName: null,
    toLocationId: 'loc-003',
    toLocationName: 'Calibration Lab - Clean Room A',
    performedByPersonnelId: 'per-004',
    performedByPersonnelName: 'Sarah Jenkins (Calibration Engineer)',
    eventType: 'INITIAL_REGISTRATION',
    movedAt: '2026-10-12T11:05:44.000Z',
  },
  {
    id: 'mov-004',
    inventoryItemId: 'item-004',
    fromLocationId: null,
    fromLocationName: null,
    toLocationId: 'loc-004',
    toLocationName: 'Fabrication Shop - Assembly Zone 2',
    performedByPersonnelId: 'per-003',
    performedByPersonnelName: 'David Kim (Materials Handling Supervisor)',
    eventType: 'INITIAL_REGISTRATION',
    movedAt: '2026-10-14T16:40:10.000Z',
  },
  {
    id: 'mov-005',
    inventoryItemId: 'item-005',
    fromLocationId: null,
    fromLocationName: null,
    toLocationId: 'loc-005',
    toLocationName: 'Receiving Dock - Quarantine & Inspection',
    performedByPersonnelId: 'per-001',
    performedByPersonnelName: 'Marcus Vance (Lead Instrumentation Tech)',
    eventType: 'INITIAL_REGISTRATION',
    movedAt: '2026-10-15T08:12:00.000Z',
  },
];

class IndustrialDatabaseEngine {
  constructor() {
    this.ensureInitialized();
  }

  private ensureInitialized() {
    if (typeof window === 'undefined') return;

    if (!localStorage.getItem(STORAGE_KEYS.SEEDED)) {
      localStorage.setItem(STORAGE_KEYS.LOCATIONS, JSON.stringify(INITIAL_LOCATIONS));
      localStorage.setItem(STORAGE_KEYS.PERSONNEL, JSON.stringify(INITIAL_PERSONNEL));
      localStorage.setItem(STORAGE_KEYS.ITEMS, JSON.stringify(INITIAL_ITEMS));
      localStorage.setItem(STORAGE_KEYS.MOVEMENTS, JSON.stringify(INITIAL_MOVEMENTS));
      localStorage.setItem(STORAGE_KEYS.SEEDED, 'true');
    }
  }

  // --- Store Locations ---
  public getLocations(activeOnly = false): StoreLocation[] {
    const raw = localStorage.getItem(STORAGE_KEYS.LOCATIONS);
    const locations: StoreLocation[] = raw ? JSON.parse(raw) : INITIAL_LOCATIONS;
    const items = this.getItems();

    const withCounts = locations.map((loc) => ({
      ...loc,
      itemCount: items.filter((i) => i.locationId === loc.id).length,
    }));

    if (activeOnly) {
      return withCounts.filter((l) => l.isActive);
    }
    return withCounts;
  }

  public getLocationById(id: string): StoreLocation | undefined {
    return this.getLocations().find((l) => l.id === id);
  }

  public createLocation(code: string, name: string): StoreLocation {
    const locations = this.getLocations();
    const cleanCode = code.trim().toUpperCase();

    if (locations.some((l) => l.locationCode.toUpperCase() === cleanCode)) {
      throw new Error(`Location code '${cleanCode}' already exists in the system.`);
    }

    const newLoc: StoreLocation = {
      id: `loc-${Date.now()}`,
      locationCode: cleanCode,
      locationName: name.trim(),
      isActive: true,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      itemCount: 0,
    };

    locations.push(newLoc);
    localStorage.setItem(STORAGE_KEYS.LOCATIONS, JSON.stringify(locations));
    return newLoc;
  }

  public updateLocation(id: string, code: string, name: string): StoreLocation {
    const locations = this.getLocations();
    const locIndex = locations.findIndex((l) => l.id === id);
    if (locIndex === -1) throw new Error(`Location '${id}' not found.`);

    const cleanCode = code.trim().toUpperCase();
    if (locations.some((l) => l.id !== id && l.locationCode.toUpperCase() === cleanCode)) {
      throw new Error(`Location code '${cleanCode}' is already assigned to another location.`);
    }

    const updated = {
      ...locations[locIndex],
      locationCode: cleanCode,
      locationName: name.trim(),
      updatedAt: new Date().toISOString(),
    };

    locations[locIndex] = updated;
    localStorage.setItem(STORAGE_KEYS.LOCATIONS, JSON.stringify(locations));
    return updated;
  }

  public toggleLocationStatus(id: string, isActive: boolean): StoreLocation {
    const locations = this.getLocations();
    const locIndex = locations.findIndex((l) => l.id === id);
    if (locIndex === -1) throw new Error(`Location '${id}' not found.`);

    const updated = {
      ...locations[locIndex],
      isActive,
      updatedAt: new Date().toISOString(),
    };

    locations[locIndex] = updated;
    localStorage.setItem(STORAGE_KEYS.LOCATIONS, JSON.stringify(locations));
    return updated;
  }

  // --- Personnel ---
  public getPersonnel(activeOnly = false): Personnel[] {
    const raw = localStorage.getItem(STORAGE_KEYS.PERSONNEL);
    const personnel: Personnel[] = raw ? JSON.parse(raw) : INITIAL_PERSONNEL;
    const items = this.getItems();

    const withCounts = personnel.map((per) => ({
      ...per,
      registeredItemsCount: items.filter((i) => i.registeredByPersonnelId === per.id).length,
    }));

    if (activeOnly) {
      return withCounts.filter((p) => p.isActive);
    }
    return withCounts;
  }

  public getPersonnelById(id: string): Personnel | undefined {
    return this.getPersonnel().find((p) => p.id === id);
  }

  public createPersonnel(code: string, name: string): Personnel {
    const personnel = this.getPersonnel();
    const cleanCode = code.trim().toUpperCase();

    if (personnel.some((p) => p.personnelCode.toUpperCase() === cleanCode)) {
      throw new Error(`Personnel ID '${cleanCode}' already exists.`);
    }

    const newPerson: Personnel = {
      id: `per-${Date.now()}`,
      personnelCode: cleanCode,
      name: name.trim(),
      isActive: true,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      registeredItemsCount: 0,
    };

    personnel.push(newPerson);
    localStorage.setItem(STORAGE_KEYS.PERSONNEL, JSON.stringify(personnel));
    return newPerson;
  }

  public updatePersonnel(id: string, code: string, name: string): Personnel {
    const personnel = this.getPersonnel();
    const idx = personnel.findIndex((p) => p.id === id);
    if (idx === -1) throw new Error(`Personnel '${id}' not found.`);

    const cleanCode = code.trim().toUpperCase();
    if (personnel.some((p) => p.id !== id && p.personnelCode.toUpperCase() === cleanCode)) {
      throw new Error(`Personnel ID '${cleanCode}' is already in use.`);
    }

    const updated = {
      ...personnel[idx],
      personnelCode: cleanCode,
      name: name.trim(),
      updatedAt: new Date().toISOString(),
    };

    personnel[idx] = updated;
    localStorage.setItem(STORAGE_KEYS.PERSONNEL, JSON.stringify(personnel));
    return updated;
  }

  public togglePersonnelStatus(id: string, isActive: boolean): Personnel {
    const personnel = this.getPersonnel();
    const idx = personnel.findIndex((p) => p.id === id);
    if (idx === -1) throw new Error(`Personnel '${id}' not found.`);

    const updated = {
      ...personnel[idx],
      isActive,
      updatedAt: new Date().toISOString(),
    };

    personnel[idx] = updated;
    localStorage.setItem(STORAGE_KEYS.PERSONNEL, JSON.stringify(personnel));
    return updated;
  }

  // --- Inventory Items ---
  public getItems(locationId?: string): InventoryItem[] {
    const raw = localStorage.getItem(STORAGE_KEYS.ITEMS);
    const storedItems: InventoryItem[] = raw ? JSON.parse(raw) : INITIAL_ITEMS;
    const items = storedItems.map((item) => ({
      ...item,
      isIssued: item.isIssued ?? false,
    }));

    if (locationId) {
      return items.filter((i) => i.locationId === locationId);
    }
    return items.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  }

  public getItemById(id: string): InventoryItem | undefined {
    return this.getItems().find((i) => i.id === id);
  }

  public searchItems(query: string, locationId?: string): InventoryItem[] {
    let items = this.getItems(locationId);
    if (!query || query.trim() === '') return items;

    const q = query.trim().toUpperCase();
    return items.filter(
      (item) =>
        item.serialNumber.toUpperCase().includes(q) ||
        item.tagNumber.toUpperCase().includes(q)
    );
  }

  // --- Registration & Atomic Transaction ---
  public registerItem(payload: RegisterInventoryPayload): InventoryItem {
    const serialClean = payload.serialNumber.trim();
    const tagClean = payload.tagNumber.trim();

    if (!serialClean) throw new Error('Serial Number is required.');
    if (!tagClean) throw new Error('Tag Number is required.');
    if (!payload.storeLocationId) throw new Error('Store Location is required.');
    if (!payload.scanByPersonnelId) throw new Error('Scan By (Responsible Personnel) is required.');

    const items = this.getItems();

    // 1. Check Duplicate Serial Number
    const existingSerial = items.find(
      (i) => i.serialNumber.toLowerCase() === serialClean.toLowerCase()
    );
    if (existingSerial) {
      const conflict: DuplicateConflictError = {
        errorCode: 'DUPLICATE_IDENTIFIER',
        message: `Serial Number '${serialClean}' is already registered in the system.`,
        conflictingField: 'SerialNumber',
        conflictingValue: serialClean,
        existingItem: existingSerial,
      };
      throw conflict;
    }

    // 2. Check Duplicate Tag Number
    const existingTag = items.find(
      (i) => i.tagNumber.toLowerCase() === tagClean.toLowerCase()
    );
    if (existingTag) {
      const conflict: DuplicateConflictError = {
        errorCode: 'DUPLICATE_IDENTIFIER',
        message: `Tag Number '${tagClean}' is already registered in the system.`,
        conflictingField: 'TagNumber',
        conflictingValue: tagClean,
        existingItem: existingTag,
      };
      throw conflict;
    }

    // 3. Verify Referenced Location
    const location = this.getLocationById(payload.storeLocationId);
    if (!location) throw new Error(`Selected store location does not exist.`);
    if (!location.isActive) throw new Error(`Selected store location '${location.locationName}' is inactive.`);

    // 4. Verify Referenced Personnel
    const personnel = this.getPersonnelById(payload.scanByPersonnelId);
    if (!personnel) throw new Error(`Selected personnel does not exist.`);
    if (!personnel.isActive) throw new Error(`Selected personnel '${personnel.name}' is inactive.`);

    // 5. ATOMIC COMMIT: Create Item + Initial Movement Record
    const now = new Date().toISOString();
    const newItemId = `item-${Date.now()}`;

    const newItem: InventoryItem = {
      id: newItemId,
      serialNumber: serialClean,
      tagNumber: tagClean,
      locationId: location.id,
      locationCode: location.locationCode,
      locationName: location.locationName,
      registeredByPersonnelId: personnel.id,
      registeredByPersonnelCode: personnel.personnelCode,
      registeredByPersonnelName: personnel.name,
      isIssued: false,
      createdAt: now,
      updatedAt: now,
    };

    const initialMovement: InventoryMovement = {
      id: `mov-${Date.now()}`,
      inventoryItemId: newItemId,
      fromLocationId: null,
      fromLocationName: null,
      toLocationId: location.id,
      toLocationName: location.locationName,
      performedByPersonnelId: personnel.id,
      performedByPersonnelName: personnel.name,
      eventType: 'INITIAL_REGISTRATION',
      movedAt: now,
    };

    // Commit both synchronously
    items.unshift(newItem);
    localStorage.setItem(STORAGE_KEYS.ITEMS, JSON.stringify(items));

    const movementsRaw = localStorage.getItem(STORAGE_KEYS.MOVEMENTS);
    const movements: InventoryMovement[] = movementsRaw ? JSON.parse(movementsRaw) : [];
    movements.unshift(initialMovement);
    localStorage.setItem(STORAGE_KEYS.MOVEMENTS, JSON.stringify(movements));

    return newItem;
  }

  // --- Movements & History ---
  public getItemHistory(itemId: string): InventoryMovement[] {
    const raw = localStorage.getItem(STORAGE_KEYS.MOVEMENTS);
    const movements: InventoryMovement[] = raw ? JSON.parse(raw) : INITIAL_MOVEMENTS;
    return movements
      .filter((m) => m.inventoryItemId === itemId)
      .sort((a, b) => new Date(b.movedAt).getTime() - new Date(a.movedAt).getTime());
  }

  // --- Dashboard Aggregations ---
  public getDashboardSummary(): DashboardSummary {
    const items = this.getItems();
    const locations = this.getLocations();
    const personnel = this.getPersonnel();

    const activeStoreLocationsCount = locations.filter((l) => l.isActive).length;
    const activePersonnelCount = personnel.filter((p) => p.isActive).length;

    const locationDistribution = locations.map((loc) => ({
      locationId: loc.id,
      locationCode: loc.locationCode,
      locationName: loc.locationName,
      itemCount: items.filter((i) => i.locationId === loc.id && !i.isIssued).length,
      isActive: loc.isActive,
    })).sort((a, b) => b.itemCount - a.itemCount);

    const recentlyRegistered = items.slice(0, 6);

    return {
      totalRegisteredUnits: items.length,
      availableUnits: items.filter((item) => !item.isIssued).length,
      issuedForPackingUnits: items.filter((item) => item.isIssued).length,
      activeStoreLocationsCount,
      activePersonnelCount,
      inventoryByStoreLocation: locationDistribution,
      recentlyRegisteredInventory: recentlyRegistered,
    };
  }

  // Reset to Factory Database
  public resetDatabase() {
    localStorage.setItem(STORAGE_KEYS.LOCATIONS, JSON.stringify(INITIAL_LOCATIONS));
    localStorage.setItem(STORAGE_KEYS.PERSONNEL, JSON.stringify(INITIAL_PERSONNEL));
    localStorage.setItem(STORAGE_KEYS.ITEMS, JSON.stringify(INITIAL_ITEMS));
    localStorage.setItem(STORAGE_KEYS.MOVEMENTS, JSON.stringify(INITIAL_MOVEMENTS));
    localStorage.setItem(STORAGE_KEYS.SEEDED, 'true');
  }
}

export const dbEngine = new IndustrialDatabaseEngine();
