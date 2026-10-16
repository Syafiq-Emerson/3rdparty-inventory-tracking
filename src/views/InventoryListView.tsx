import React, { useEffect, useState } from 'react';
import {
  Search,
  Filter,
  Download,
  Eye,
  RefreshCw,
  Building2,
  Calendar,
  User,
  Hash,
  ChevronLeft,
  ChevronRight,
  SlidersHorizontal,
} from 'lucide-react';
import { InventoryItem, StoreLocation } from '../types/inventory';
import { inventoryApi } from '../services/api';

interface Props {
  onInspectItem: (itemId: string) => void;
  onNavigateToRegister: () => void;
}

export const InventoryListView: React.FC<Props> = ({ onInspectItem, onNavigateToRegister }) => {
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [locations, setLocations] = useState<StoreLocation[]>([]);
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedLocationId, setSelectedLocationId] = useState<string>('');

  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Pagination
  const [currentPage, setCurrentPage] = useState<number>(1);
  const pageSize = 15;

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [locationsData, itemsData] = await Promise.all([
        inventoryApi.getLocations(),
        searchQuery.trim()
          ? inventoryApi.searchInventory(searchQuery.trim(), selectedLocationId || undefined)
          : inventoryApi.getInventory(selectedLocationId || undefined),
      ]);
      setLocations(locationsData);
      setItems(itemsData);
    } catch (err: any) {
      setError(err.message || 'Failed to query inventory records.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [selectedLocationId]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCurrentPage(1);
    loadData();
  };

  const handleResetFilters = () => {
    setSearchQuery('');
    setSelectedLocationId('');
    setCurrentPage(1);
  };

  const handleExportCsv = () => {
    if (items.length === 0) return;

    const headers = ['Serial Number', 'Tag Number', 'Location Code', 'Location Name', 'Registered By', 'Date Created'];
    const rows = items.map((i) => [
      `"${i.serialNumber}"`,
      `"${i.tagNumber}"`,
      `"${i.locationCode}"`,
      `"${i.locationName}"`,
      `"${i.registeredByPersonnelName}"`,
      `"${new Date(i.createdAt).toISOString()}"`,
    ]);

    const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `inventory_export_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  // Client-side pagination slicing of retrieved query page
  const totalPages = Math.ceil(items.length / pageSize) || 1;
  const paginatedItems = items.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  return (
    <div className="space-y-6">
      {/* Top Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-200">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-slate-900">
            Inventory Catalog & Search
          </h1>
          <p className="text-xs text-slate-500 mt-0.5">
            Query and filter plant equipment records by Serial Number or Tag Number.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handleExportCsv}
            disabled={items.length === 0}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition-colors shadow-2xs disabled:opacity-40"
          >
            <Download className="w-3.5 h-3.5 text-slate-500" />
            <span>Export CSV</span>
          </button>

          <button
            onClick={onNavigateToRegister}
            className="flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors shadow-2xs"
          >
            <span>+ Ingest Asset</span>
          </button>
        </div>
      </div>

      {/* Search & Location Filter Toolbar */}
      <div className="p-4 bg-white rounded-lg border border-slate-200 shadow-2xs">
        <form onSubmit={handleSearchSubmit} className="flex flex-col md:flex-row items-center gap-3">
          {/* Search Input */}
          <div className="relative flex-1 w-full">
            <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by Serial Number (e.g. F005327261) or Tag Number (e.g. PV-16501)..."
              className="w-full pl-9 pr-3 py-2 bg-slate-50/50 border border-slate-300 rounded text-xs text-slate-900 placeholder:text-slate-400 focus:bg-white focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87] outline-hidden transition-colors"
            />
          </div>

          {/* Location Filter Dropdown */}
          <div className="w-full md:w-64">
            <select
              value={selectedLocationId}
              onChange={(e) => {
                setSelectedLocationId(e.target.value);
                setCurrentPage(1);
              }}
              className="w-full py-2 px-3 bg-slate-50/50 border border-slate-300 rounded text-xs text-slate-900 focus:bg-white focus:border-[#004B87] outline-hidden transition-colors"
            >
              <option value="">All Store Locations</option>
              {locations.map((loc) => (
                <option key={loc.id} value={loc.id}>
                  {loc.locationCode} - {loc.locationName}
                </option>
              ))}
            </select>
          </div>

          {/* Buttons */}
          <div className="flex items-center gap-2 w-full md:w-auto">
            <button
              type="submit"
              className="flex-1 md:flex-none px-4 py-2 text-xs font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors"
            >
              Search
            </button>
            {(searchQuery || selectedLocationId) && (
              <button
                type="button"
                onClick={handleResetFilters}
                className="px-3 py-2 text-xs font-medium text-slate-600 hover:text-slate-900 bg-slate-100 rounded transition-colors"
              >
                Clear
              </button>
            )}
          </div>
        </form>
      </div>

      {/* Main Table */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-2xs overflow-hidden">
        {loading ? (
          <div className="py-20 text-center text-slate-500 text-xs">
            <div className="animate-spin w-7 h-7 border-2 border-[#004B87] border-t-transparent rounded-full mx-auto mb-2.5" />
            Executing backend inventory query...
          </div>
        ) : error ? (
          <div className="p-6 text-center text-red-800 text-xs bg-red-50">
            {error}
          </div>
        ) : items.length === 0 ? (
          <div className="py-20 text-center text-slate-500 space-y-3">
            <div className="w-12 h-12 rounded-full bg-slate-100 text-slate-400 flex items-center justify-center mx-auto">
              <Search className="w-6 h-6" />
            </div>
            <div className="text-sm font-semibold text-slate-800">No Inventory Records Found</div>
            <p className="text-xs text-slate-400 max-w-sm mx-auto">
              {searchQuery || selectedLocationId
                ? 'No assets matched your search filter criteria. Try adjusting your search term.'
                : 'No equipment has been registered in the database yet.'}
            </p>
            <button
              onClick={onNavigateToRegister}
              className="px-4 py-2 text-xs font-semibold text-white bg-[#004B87] rounded hover:bg-[#003866] transition-colors"
            >
              Register First Asset
            </button>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="bg-slate-50 text-slate-600 border-b border-slate-200 font-semibold">
                    <th className="py-3 px-4">Serial Number</th>
                    <th className="py-3 px-4">Tag Number</th>
                    <th className="py-3 px-4">Store Location</th>
                    <th className="py-3 px-4">Responsible Personnel</th>
                    <th className="py-3 px-4">Registration Date</th>
                    <th className="py-3 px-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {paginatedItems.map((item) => (
                    <tr
                      key={item.id}
                      className="hover:bg-slate-50/80 transition-colors"
                    >
                      {/* Serial Number */}
                      <td className="py-3 px-4 font-mono font-medium text-slate-900">
                        {item.serialNumber}
                      </td>

                      {/* Tag Number */}
                      <td className="py-3 px-4 font-mono font-bold text-[#004B87]">
                        {item.tagNumber}
                      </td>

                      {/* Store Location */}
                      <td className="py-3 px-4">
                        <div className="font-medium text-slate-900 truncate max-w-[200px]">
                          {item.locationName}
                        </div>
                        <div className="font-mono text-[11px] text-slate-400">
                          {item.locationCode}
                        </div>
                      </td>

                      {/* Registered By Personnel */}
                      <td className="py-3 px-4 text-slate-700">
                        <div className="font-medium truncate max-w-[180px]">
                          {item.registeredByPersonnelName}
                        </div>
                        <div className="font-mono text-[11px] text-slate-400">
                          {item.registeredByPersonnelCode}
                        </div>
                      </td>

                      {/* Registration Date */}
                      <td className="py-3 px-4 font-mono tabular-nums text-slate-500">
                        {new Date(item.createdAt).toLocaleDateString()}{' '}
                        <span className="text-[11px] text-slate-400">
                          {new Date(item.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                        </span>
                      </td>

                      {/* Action */}
                      <td className="py-3 px-4 text-right">
                        <button
                          onClick={() => onInspectItem(item.id)}
                          className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold text-[#004B87] hover:bg-blue-50 rounded transition-colors"
                        >
                          <Eye className="w-3.5 h-3.5" />
                          <span>Details</span>
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination Controls */}
            <div className="flex items-center justify-between px-5 py-3 bg-slate-50 border-t border-slate-200 text-xs text-slate-600">
              <div>
                Showing <span className="font-mono font-semibold">{(currentPage - 1) * pageSize + 1}</span> to{' '}
                <span className="font-mono font-semibold">{Math.min(currentPage * pageSize, items.length)}</span> of{' '}
                <span className="font-mono font-semibold">{items.length}</span> units
              </div>

              <div className="flex items-center gap-2">
                <button
                  onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                  disabled={currentPage === 1}
                  className="p-1 border border-slate-300 rounded bg-white hover:bg-slate-50 disabled:opacity-40 disabled:pointer-events-none transition-colors"
                  aria-label="Previous page"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>
                <span className="font-mono font-medium px-2">
                  Page {currentPage} of {totalPages}
                </span>
                <button
                  onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
                  disabled={currentPage === totalPages}
                  className="p-1 border border-slate-300 rounded bg-white hover:bg-slate-50 disabled:opacity-40 disabled:pointer-events-none transition-colors"
                  aria-label="Next page"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
};
