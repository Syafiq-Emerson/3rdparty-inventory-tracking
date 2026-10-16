import React, { useEffect, useState } from 'react';
import {
  MapPin,
  Plus,
  Edit2,
  Power,
  Check,
  X,
  AlertCircle,
  Building,
  RefreshCw,
  Boxes,
} from 'lucide-react';
import { StoreLocation } from '../types/inventory';
import { inventoryApi } from '../services/api';

export const StoreLocationsView: React.FC = () => {
  const [locations, setLocations] = useState<StoreLocation[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Modal / Form state for Add or Edit
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);
  const [editingLocation, setEditingLocation] = useState<StoreLocation | null>(null);
  const [formCode, setFormCode] = useState<string>('');
  const [formName, setFormName] = useState<string>('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const loadLocations = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await inventoryApi.getLocations(false); // get both active & inactive
      setLocations(data);
    } catch (err: any) {
      setError(err.message || 'Failed to fetch store locations.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadLocations();
  }, []);

  const handleOpenAdd = () => {
    setEditingLocation(null);
    setFormCode('');
    setFormName('');
    setFormError(null);
    setIsModalOpen(true);
  };

  const handleOpenEdit = (loc: StoreLocation) => {
    setEditingLocation(loc);
    setFormCode(loc.locationCode);
    setFormName(loc.locationName);
    setFormError(null);
    setIsModalOpen(true);
  };

  const handleToggleStatus = async (loc: StoreLocation) => {
    try {
      await inventoryApi.toggleLocationStatus(loc.id, !loc.isActive);
      await loadLocations();
    } catch (err: any) {
      alert(err.message || 'Failed to toggle location status.');
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formCode.trim() || !formName.trim()) {
      setFormError('Both Location Code and Location Name are required.');
      return;
    }

    setIsSaving(true);
    setFormError(null);

    try {
      if (editingLocation) {
        await inventoryApi.updateLocation(editingLocation.id, formCode.trim(), formName.trim());
      } else {
        await inventoryApi.createLocation(formCode.trim(), formName.trim());
      }
      setIsModalOpen(false);
      await loadLocations();
    } catch (err: any) {
      setFormError(err.message || 'Failed to save location.');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Top Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-200">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-slate-900">
            Settings: Store Locations
          </h1>
          <p className="text-xs text-slate-500 mt-0.5">
            Configure storage bays, clean rooms, and racks. Deactivating a location preserves historical references.
          </p>
        </div>

        <button
          onClick={handleOpenAdd}
          className="flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors shadow-2xs"
        >
          <Plus className="w-4 h-4" />
          <span>Add Store Location</span>
        </button>
      </div>

      {/* Main Table */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-2xs overflow-hidden">
        {loading ? (
          <div className="py-20 text-center text-slate-500 text-xs">
            <div className="animate-spin w-7 h-7 border-2 border-[#004B87] border-t-transparent rounded-full mx-auto mb-2.5" />
            Loading store locations from database...
          </div>
        ) : error ? (
          <div className="p-6 text-center text-red-800 text-xs bg-red-50">
            {error}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-slate-50 text-slate-600 border-b border-slate-200 font-semibold">
                  <th className="py-3 px-4">Location Code</th>
                  <th className="py-3 px-4">Location Name</th>
                  <th className="py-3 px-4">Status</th>
                  <th className="py-3 px-4">Assigned Units</th>
                  <th className="py-3 px-4">Created Date</th>
                  <th className="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {locations.map((loc) => (
                  <tr
                    key={loc.id}
                    className={`hover:bg-slate-50/80 transition-colors ${
                      !loc.isActive ? 'opacity-60 bg-slate-50/40' : ''
                    }`}
                  >
                    {/* Location Code */}
                    <td className="py-3 px-4 font-mono font-bold text-slate-900">
                      {loc.locationCode}
                    </td>

                    {/* Location Name */}
                    <td className="py-3 px-4 font-medium text-slate-800">
                      {loc.locationName}
                    </td>

                    {/* Status */}
                    <td className="py-3 px-4">
                      {loc.isActive ? (
                        <span className="inline-flex items-center gap-1 text-emerald-800 font-medium">
                          <span className="w-1.5 h-1.5 rounded-full bg-emerald-600" />
                          Active
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-slate-500">
                          <span className="w-1.5 h-1.5 rounded-full bg-slate-400" />
                          Inactive
                        </span>
                      )}
                    </td>

                    {/* Assigned Units */}
                    <td className="py-3 px-4 font-mono tabular-nums text-slate-600">
                      <span className="font-semibold text-slate-800">{loc.itemCount || 0}</span> unit{(loc.itemCount || 0) === 1 ? '' : 's'}
                    </td>

                    {/* Created Date */}
                    <td className="py-3 px-4 font-mono tabular-nums text-slate-400">
                      {new Date(loc.createdAt).toLocaleDateString()}
                    </td>

                    {/* Actions */}
                    <td className="py-3 px-4 text-right space-x-2">
                      <button
                        onClick={() => handleOpenEdit(loc)}
                        className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-100 border border-slate-300 rounded transition-colors"
                      >
                        <Edit2 className="w-3 h-3 text-slate-500" />
                        <span>Edit</span>
                      </button>

                      <button
                        onClick={() => handleToggleStatus(loc)}
                        className={`inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded border transition-colors ${
                          loc.isActive
                            ? 'text-amber-800 hover:bg-amber-50 border-amber-300'
                            : 'text-emerald-800 hover:bg-emerald-50 border-emerald-300'
                        }`}
                        title={
                          loc.isActive
                            ? 'Deactivate location (preserves existing assignments)'
                            : 'Re-activate location'
                        }
                      >
                        <Power className="w-3 h-3" />
                        <span>{loc.isActive ? 'Deactivate' : 'Activate'}</span>
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Add / Edit Location Modal Dialog */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs">
          <div className="w-full max-w-md bg-white rounded-lg shadow-xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center justify-between px-6 py-4 bg-slate-900 text-white">
              <div className="flex items-center gap-2.5">
                <Building className="w-4 h-4 text-blue-400" />
                <h3 className="text-sm font-semibold">
                  {editingLocation ? 'Edit Store Location' : 'Add New Store Location'}
                </h3>
              </div>
              <button
                onClick={() => setIsModalOpen(false)}
                className="text-slate-400 hover:text-white"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleSave} className="p-6 space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">
                  Location Code <span className="text-red-600">*</span> (Unique)
                </label>
                <input
                  type="text"
                  required
                  value={formCode}
                  onChange={(e) => setFormCode(e.target.value.toUpperCase())}
                  placeholder="e.g. WH-C-02"
                  className="w-full bg-white border border-slate-300 rounded px-3 py-2 text-xs font-mono uppercase focus:border-[#004B87] outline-hidden"
                />
                <span className="text-[11px] text-slate-400 mt-0.5 block">
                  Identifier tag assigned to bay/rack.
                </span>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">
                  Location Name <span className="text-red-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={formName}
                  onChange={(e) => setFormName(e.target.value)}
                  placeholder="e.g. Warehouse C - Hazardous Storage Room"
                  className="w-full bg-white border border-slate-300 rounded px-3 py-2 text-xs focus:border-[#004B87] outline-hidden"
                />
                <span className="text-[11px] text-slate-400 mt-0.5 block">
                  Full descriptive facility department title.
                </span>
              </div>

              {formError && (
                <div className="p-2.5 bg-red-50 border border-red-200 rounded text-red-800 text-xs flex items-center gap-1.5">
                  <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
                  <span>{formError}</span>
                </div>
              )}

              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-3.5 py-1.5 font-medium text-slate-600 hover:text-slate-900 border border-slate-300 rounded"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isSaving}
                  className="px-4 py-1.5 font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded disabled:opacity-50"
                >
                  {isSaving ? 'Saving...' : editingLocation ? 'Update Location' : 'Create Location'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
