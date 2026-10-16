import React, { useEffect, useState } from 'react';
import {
  Boxes,
  MapPin,
  Users,
  Clock,
  ArrowUpRight,
  Barcode,
  Search,
  RefreshCw,
  TrendingUp,
  Layers,
  ChevronRight,
} from 'lucide-react';
import { DashboardSummary } from '../types/inventory';
import { inventoryApi } from '../services/api';

interface Props {
  onNavigate: (view: string) => void;
  onInspectItem: (itemId: string) => void;
}

export const DashboardView: React.FC<Props> = ({ onNavigate, onInspectItem }) => {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [refreshing, setRefreshing] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const fetchSummary = async (isManual = false) => {
    if (isManual) setRefreshing(true);
    else setLoading(true);
    setError(null);

    try {
      const data = await inventoryApi.getDashboardSummary();
      setSummary(data);
    } catch (err: any) {
      setError(err.message || 'Failed to fetch dashboard statistics.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchSummary();
  }, []);

  const totalCapacity = summary?.inventoryByStoreLocation.reduce((acc, curr) => acc + curr.itemCount, 0) || 1;

  return (
    <div className="space-y-6">
      {/* Page Title & Actions Strip */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-200">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-slate-900">
            Plant Inventory Telemetry & Dashboard
          </h1>
          <p className="text-xs text-slate-500 mt-0.5">
            Real-time asset tallies and distribution across physical store locations.
          </p>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            onClick={() => fetchSummary(true)}
            disabled={refreshing}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition-colors shadow-2xs disabled:opacity-50"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${refreshing ? 'animate-spin' : ''}`} />
            <span>Refresh Ledger</span>
          </button>

          <button
            onClick={() => onNavigate('register')}
            className="flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors shadow-2xs"
          >
            <Barcode className="w-3.5 h-3.5" />
            <span>Register Asset</span>
          </button>
        </div>
      </div>

      {loading && !summary ? (
        <div className="py-24 text-center text-slate-500 text-sm">
          <div className="animate-spin w-8 h-8 border-2 border-[#004B87] border-t-transparent rounded-full mx-auto mb-3" />
          Querying relational database records...
        </div>
      ) : error ? (
        <div className="p-4 bg-red-50 border border-red-200 rounded text-red-800 text-xs">
          {error}
        </div>
      ) : summary ? (
        <>
          {/* Key Performance Indicators Strip */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {/* KPI 1: Total Units */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-2xs">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Total Tracked Units
                </span>
                <div className="p-2 bg-blue-50 text-[#004B87] rounded">
                  <Boxes className="w-4 h-4" />
                </div>
              </div>
              <div className="text-2xl font-bold font-mono tabular-nums text-slate-900">
                {summary.totalRegisteredUnits}
              </div>
              <div className="text-[11px] text-slate-500 mt-1 flex items-center gap-1">
                <span>Unique dual-barcode pairs</span>
              </div>
            </div>

            {/* KPI 2: Active Store Locations */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-2xs">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Active Store Locations
                </span>
                <div className="p-2 bg-slate-100 text-slate-700 rounded">
                  <MapPin className="w-4 h-4" />
                </div>
              </div>
              <div className="text-2xl font-bold font-mono tabular-nums text-slate-900">
                {summary.activeStoreLocationsCount}
              </div>
              <div className="text-[11px] text-slate-500 mt-1 flex items-center gap-1">
                <span>Storage bays, racks & labs</span>
              </div>
            </div>

            {/* KPI 3: Active Personnel */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-2xs">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Active Personnel
                </span>
                <div className="p-2 bg-slate-100 text-slate-700 rounded">
                  <Users className="w-4 h-4" />
                </div>
              </div>
              <div className="text-2xl font-bold font-mono tabular-nums text-slate-900">
                {summary.activePersonnelCount}
              </div>
              <div className="text-[11px] text-slate-500 mt-1 flex items-center gap-1">
                <span>Technicians & supervisors</span>
              </div>
            </div>

            {/* KPI 4: Recent Intake Activity */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-2xs">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Intake Activity
                </span>
                <div className="p-2 bg-emerald-50 text-emerald-700 rounded">
                  <Clock className="w-4 h-4" />
                </div>
              </div>
              <div className="text-2xl font-bold font-mono tabular-nums text-slate-900">
                {summary.recentlyRegisteredInventory.length}
              </div>
              <div className="text-[11px] text-slate-500 mt-1 flex items-center gap-1">
                <span>Recent cataloged assets</span>
              </div>
            </div>
          </div>

          {/* Section 2: Distribution Chart & Quick Actions */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Location Distribution Breakdown */}
            <div className="lg:col-span-2 bg-white p-5 rounded-lg border border-slate-200 shadow-2xs flex flex-col">
              <div className="flex items-center justify-between mb-4 pb-3 border-b border-slate-100">
                <div className="flex items-center gap-2">
                  <TrendingUp className="w-4 h-4 text-[#004B87]" />
                  <h2 className="text-sm font-bold text-slate-900">
                    Inventory Distribution by Store Location
                  </h2>
                </div>
                <button
                  onClick={() => onNavigate('locations')}
                  className="text-xs text-[#004B87] hover:underline font-medium flex items-center gap-0.5"
                >
                  Manage Locations <ChevronRight className="w-3 h-3" />
                </button>
              </div>

              <div className="space-y-3.5 flex-1 justify-center flex flex-col">
                {summary.inventoryByStoreLocation.length === 0 ? (
                  <div className="text-xs text-slate-400 text-center py-6">
                    No store locations configured.
                  </div>
                ) : (
                  summary.inventoryByStoreLocation.map((loc) => {
                    const percentage = totalCapacity > 0 ? Math.round((loc.itemCount / totalCapacity) * 100) : 0;
                    return (
                      <div key={loc.locationId} className="space-y-1">
                        <div className="flex items-center justify-between text-xs">
                          <div className="flex items-center gap-2 truncate pr-2">
                            <span className="font-mono font-semibold text-slate-900">
                              {loc.locationCode}
                            </span>
                            <span className="text-slate-500 truncate">
                              {loc.locationName}
                            </span>
                            {!loc.isActive && (
                              <span className="text-[10px] text-amber-700 bg-amber-50 px-1 rounded">
                                Inactive
                              </span>
                            )}
                          </div>
                          <div className="flex items-center gap-3 shrink-0">
                            <span className="font-mono font-medium text-slate-700 tabular-nums">
                              {loc.itemCount} unit{loc.itemCount === 1 ? '' : 's'}
                            </span>
                            <span className="text-[11px] text-slate-400 font-mono w-9 text-right tabular-nums">
                              {percentage}%
                            </span>
                          </div>
                        </div>

                        {/* Bar Chart Track */}
                        <div className="w-full h-2 bg-slate-100 rounded-full overflow-hidden">
                          <div
                            className={`h-full rounded-full transition-all duration-300 ${
                              loc.isActive ? 'bg-[#004B87]' : 'bg-slate-400'
                            }`}
                            style={{ width: `${Math.max(percentage, 2)}%` }}
                          />
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </div>

            {/* Quick Workstation Actions Card */}
            <div className="bg-white p-5 rounded-lg border border-slate-200 shadow-2xs flex flex-col justify-between">
              <div>
                <div className="flex items-center gap-2 mb-3 pb-3 border-b border-slate-100">
                  <Layers className="w-4 h-4 text-slate-700" />
                  <h2 className="text-sm font-bold text-slate-900">
                    Workstation Actions
                  </h2>
                </div>
                <p className="text-xs text-slate-500 mb-4 leading-relaxed">
                  Perform high-speed dual barcode ingestion or audit physical inventory items across bays.
                </p>

                <div className="space-y-2.5">
                  <button
                    onClick={() => onNavigate('register')}
                    className="w-full flex items-center justify-between p-3 rounded border border-slate-200 hover:border-blue-300 hover:bg-blue-50/40 text-left transition-colors group"
                  >
                    <div className="flex items-center gap-3">
                      <div className="p-2 bg-blue-100/70 text-[#004B87] rounded">
                        <Barcode className="w-4 h-4" />
                      </div>
                      <div>
                        <div className="text-xs font-semibold text-slate-900 group-hover:text-[#004B87]">
                          Register Dual Barcode Unit
                        </div>
                        <div className="text-[11px] text-slate-500">
                          Supports handheld scanner & camera
                        </div>
                      </div>
                    </div>
                    <ArrowUpRight className="w-4 h-4 text-slate-400 group-hover:text-[#004B87]" />
                  </button>

                  <button
                    onClick={() => onNavigate('inventory')}
                    className="w-full flex items-center justify-between p-3 rounded border border-slate-200 hover:border-slate-300 hover:bg-slate-50 text-left transition-colors group"
                  >
                    <div className="flex items-center gap-3">
                      <div className="p-2 bg-slate-100 text-slate-700 rounded">
                        <Search className="w-4 h-4" />
                      </div>
                      <div>
                        <div className="text-xs font-semibold text-slate-900">
                          Search Existing Inventory
                        </div>
                        <div className="text-[11px] text-slate-500">
                          Query by serial number or ISA tag
                        </div>
                      </div>
                    </div>
                    <ArrowUpRight className="w-4 h-4 text-slate-400" />
                  </button>
                </div>
              </div>

              <div className="mt-6 p-3 bg-slate-50 border border-slate-200 rounded text-[11px] text-slate-500">
                <span className="font-semibold text-slate-700">Database Safeguard:</span> Simultaneous unique constraints active on Serial Number and Tag Number.
              </div>
            </div>
          </div>

          {/* Section 3: Recently Registered Inventory Table */}
          <div className="bg-white rounded-lg border border-slate-200 shadow-2xs overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-slate-200">
              <div className="flex items-center gap-2">
                <Clock className="w-4 h-4 text-slate-700" />
                <h2 className="text-sm font-bold text-slate-900">
                  Recently Registered Inventory
                </h2>
              </div>
              <button
                onClick={() => onNavigate('inventory')}
                className="text-xs text-[#004B87] hover:underline font-medium flex items-center gap-1"
              >
                View Full Inventory Ledger <ChevronRight className="w-3 h-3" />
              </button>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="bg-slate-50 text-slate-600 border-b border-slate-200 font-semibold">
                    <th className="py-2.5 px-4">Serial Number</th>
                    <th className="py-2.5 px-4">Tag Number</th>
                    <th className="py-2.5 px-4">Store Location</th>
                    <th className="py-2.5 px-4">Registered By</th>
                    <th className="py-2.5 px-4">Registration Date</th>
                    <th className="py-2.5 px-4 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {summary.recentlyRegisteredInventory.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="py-8 text-center text-slate-400">
                        No registered inventory items yet.
                      </td>
                    </tr>
                  ) : (
                    summary.recentlyRegisteredInventory.map((item) => (
                      <tr
                        key={item.id}
                        className="hover:bg-slate-50/80 transition-colors"
                      >
                        <td className="py-2.5 px-4 font-mono font-medium text-slate-900">
                          {item.serialNumber}
                        </td>
                        <td className="py-2.5 px-4 font-mono font-semibold text-[#004B87]">
                          {item.tagNumber}
                        </td>
                        <td className="py-2.5 px-4 text-slate-700">
                          <span className="font-semibold">{item.locationName}</span>{' '}
                          <span className="text-slate-400 font-mono">[{item.locationCode}]</span>
                        </td>
                        <td className="py-2.5 px-4 text-slate-600">
                          {item.registeredByPersonnelName}
                        </td>
                        <td className="py-2.5 px-4 text-slate-500 font-mono tabular-nums">
                          {new Date(item.createdAt).toLocaleString()}
                        </td>
                        <td className="py-2.5 px-4 text-right">
                          <button
                            onClick={() => onInspectItem(item.id)}
                            className="text-xs text-[#004B87] hover:underline font-semibold"
                          >
                            Inspect
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </>
      ) : null}
    </div>
  );
};
