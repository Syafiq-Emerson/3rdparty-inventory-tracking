import {
  DashboardSummary,
  InventoryItem,
  InventoryMovement,
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

export const inventoryApi = {
  // Dashboard Endpoints
  async getDashboardSummary(): Promise<DashboardSummary> {
    await delay();
    return dbEngine.getDashboardSummary();
  },

  // Inventory Endpoints
  async getInventory(locationId?: string): Promise<InventoryItem[]> {
    await delay();
    return dbEngine.getItems(locationId);
  },

  async getInventoryById(id: string): Promise<InventoryItem> {
    await delay();
    const item = dbEngine.getItemById(id);
    if (!item) {
      throw new Error(`Inventory item '${id}' not found.`);
    }
    return item;
  },

  async searchInventory(query: string, locationId?: string): Promise<InventoryItem[]> {
    await delay();
    return dbEngine.searchItems(query, locationId);
  },

  async registerInventory(payload: RegisterInventoryPayload): Promise<InventoryItem> {
    await delay(120); // Network & database transaction simulation
    return dbEngine.registerItem(payload);
  },

  async getInventoryHistory(itemId: string): Promise<InventoryMovement[]> {
    await delay();
    return dbEngine.getItemHistory(itemId);
  },

  // Store Location Endpoints
  async getLocations(activeOnly = false): Promise<StoreLocation[]> {
    await delay();
    return dbEngine.getLocations(activeOnly);
  },

  async createLocation(code: string, name: string): Promise<StoreLocation> {
    await delay();
    return dbEngine.createLocation(code, name);
  },

  async updateLocation(id: string, code: string, name: string): Promise<StoreLocation> {
    await delay();
    return dbEngine.updateLocation(id, code, name);
  },

  async toggleLocationStatus(id: string, isActive: boolean): Promise<StoreLocation> {
    await delay();
    return dbEngine.toggleLocationStatus(id, isActive);
  },

  // Personnel Endpoints
  async getPersonnel(activeOnly = false): Promise<Personnel[]> {
    await delay();
    return dbEngine.getPersonnel(activeOnly);
  },

  async createPersonnel(code: string, name: string): Promise<Personnel> {
    await delay();
    return dbEngine.createPersonnel(code, name);
  },

  async updatePersonnel(id: string, code: string, name: string): Promise<Personnel> {
    await delay();
    return dbEngine.updatePersonnel(id, code, name);
  },

  async togglePersonnelStatus(id: string, isActive: boolean): Promise<Personnel> {
    await delay();
    return dbEngine.togglePersonnelStatus(id, isActive);
  },

  // Factory Database Reset
  async resetDatabase(): Promise<void> {
    await delay(150);
    dbEngine.resetDatabase();
  },
};
