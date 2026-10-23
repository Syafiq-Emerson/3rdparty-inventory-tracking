import React from 'react';
import { X, MapPin, User, Calendar, Hash, Clock, ShieldCheck, ArrowRight } from 'lucide-react';
import { inventoryApi } from '../services/api';
import { useAsync } from '../hooks/useAsync';

interface Props {
  itemId: string | null;
  onClose: () => void;
}

const loadItem = (itemId: string) =>
  Promise.all([inventoryApi.getInventoryById(itemId), inventoryApi.getInventoryHistory(itemId)]).then(
    ([item, history]) => ({ item, history }),
  );

/** Keyed by item, so opening another item always starts from a clean load. */
export const ItemDetailModal: React.FC<Props> = ({ itemId, onClose }) =>
  itemId ? <ItemDetailDialog key={itemId} itemId={itemId} onClose={onClose} /> : null;

const ItemDetailDialog: React.FC<{ itemId: string; onClose: () => void }> = ({ itemId, onClose }) => {
  const { data, loading, error } = useAsync(loadItem, itemId, 'Failed to load item specifications.');
  const item = data?.item ?? null;
  const history = data?.history ?? [];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs">
      <div className="w-full max-w-2xl bg-white rounded-lg shadow-2xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150 max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-900 text-white shrink-0">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-blue-600 rounded">
              <ShieldCheck className="w-5 h-5 text-white" />
            </div>
            <div>
              <div className="text-xs uppercase tracking-wider text-slate-400 font-mono">
                Asset Specification Record
              </div>
              <h3 className="text-lg font-semibold tracking-tight">
                {item ? item.tagNumber : 'Loading Unit...'}
              </h3>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-white rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content Body */}
        <div className="p-6 overflow-y-auto space-y-6">
          {loading ? (
            <div className="py-16 text-center text-slate-500 text-sm">
              <div className="animate-spin w-6 h-6 border-2 border-blue-700 border-t-transparent rounded-full mx-auto mb-3" />
              Retrieving item record and movement ledger...
            </div>
          ) : error ? (
            <div className="p-4 bg-red-50 border border-red-200 rounded text-red-800 text-sm">
              {error}
            </div>
          ) : item ? (
            <>
              {/* Primary Identifiers */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="p-4 bg-slate-50 border border-slate-200 rounded">
                  <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1 flex items-center gap-1.5">
                    <Hash className="w-3.5 h-3.5 text-slate-400" /> Serial Number (Physical Barcode 1)
                  </div>
                  <div className="text-lg font-mono font-bold text-slate-900">
                    {item.serialNumber}
                  </div>
                </div>

                <div className="p-4 bg-blue-50/50 border border-blue-200 rounded">
                  <div className="text-xs font-semibold text-blue-700 uppercase tracking-wider mb-1 flex items-center gap-1.5">
                    <Hash className="w-3.5 h-3.5 text-blue-600" /> Tag Number (Loop Barcode 2)
                  </div>
                  <div className="text-lg font-mono font-bold text-blue-900">
                    {item.tagNumber}
                  </div>
                </div>
              </div>

              {/* Location & Personnel Context */}
              <div className="border border-slate-200 rounded divide-y divide-slate-100 text-sm">
                <div className="flex items-center justify-between px-4 py-3">
                  <span className="text-slate-500 flex items-center gap-2">
                    <MapPin className="w-4 h-4 text-slate-400" /> Current Store Location
                  </span>
                  <span className="font-semibold text-slate-900">
                    {item.locationName} <span className="font-mono text-xs text-slate-500">[{item.locationCode}]</span>
                  </span>
                </div>

                <div className="flex items-center justify-between px-4 py-3">
                  <span className="text-slate-500 flex items-center gap-2">
                    <ShieldCheck className="w-4 h-4 text-slate-400" /> Availability
                  </span>
                  <span className={`rounded px-2 py-1 text-xs font-semibold ${item.isIssued ? 'bg-amber-100 text-amber-900' : 'bg-emerald-100 text-emerald-900'}`}>
                    {item.isIssued ? 'Issued for packing' : 'Available'}
                  </span>
                </div>

                <div className="flex items-center justify-between px-4 py-3">
                  <span className="text-slate-500 flex items-center gap-2">
                    <User className="w-4 h-4 text-slate-400" /> Registered By Technician
                  </span>
                  <span className="font-medium text-slate-900">
                    {item.registeredByPersonnelName} <span className="font-mono text-xs text-slate-500">({item.registeredByPersonnelCode})</span>
                  </span>
                </div>

                <div className="flex items-center justify-between px-4 py-3">
                  <span className="text-slate-500 flex items-center gap-2">
                    <Calendar className="w-4 h-4 text-slate-400" /> Initial Registration Date
                  </span>
                  <span className="font-mono tabular-nums text-slate-700">
                    {new Date(item.createdAt).toLocaleString()}
                  </span>
                </div>
              </div>

              {/* Movement & Custody History (inventory_movements) */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-600 flex items-center gap-1.5">
                    <Clock className="w-4 h-4 text-slate-500" /> Chain of Custody & Location Ledger
                  </h4>
                  <span className="text-xs text-slate-400 font-mono">
                    {history.length} Event{history.length === 1 ? '' : 's'}
                  </span>
                </div>

                <div className="border border-slate-200 rounded divide-y divide-slate-100 overflow-hidden text-xs">
                  {history.length === 0 ? (
                    <div className="p-4 text-center text-slate-400">
                      No movement history recorded.
                    </div>
                  ) : (
                    history.map((movement) => (
                      <div key={movement.id} className="p-3 bg-white hover:bg-slate-50 transition-colors">
                        <div className="flex items-center justify-between mb-1.5">
                          <span className="font-semibold text-slate-900 flex items-center gap-1.5">
                            {movement.eventType === 'INITIAL_REGISTRATION'
                              ? <span className="text-blue-800 font-medium">Initial Registration & Intake</span>
                              : movement.eventType === 'ISSUE_FOR_PACKING'
                                ? <span className="text-amber-800 font-medium">Issued for Packing</span>
                                : <span>Location Relocation</span>}
                          </span>
                          <span className="text-slate-500 font-mono tabular-nums">
                            {new Date(movement.movedAt).toLocaleString()}
                          </span>
                        </div>

                        <div className="flex items-center gap-2 text-slate-600">
                          {movement.eventType === 'ISSUE_FOR_PACKING' ? (
                            <>
                              <span>{movement.fromLocationName ?? 'Warehouse location'}</span>
                              <ArrowRight className="w-3.5 h-3.5 text-amber-600 shrink-0" />
                              <span className="font-medium text-amber-900">Packing (issued)</span>
                            </>
                          ) : movement.fromLocationName ? (
                            <>
                              <span>{movement.fromLocationName}</span>
                              <ArrowRight className="w-3.5 h-3.5 text-slate-400 shrink-0" />
                              <span className="font-medium text-slate-900">{movement.toLocationName ?? 'Unknown destination'}</span>
                            </>
                          ) : (
                            <span>
                              Assigned to <span className="font-medium text-slate-900">{movement.toLocationName}</span>
                            </span>
                          )}
                          <span className="text-slate-300">·</span>
                          <span className="text-slate-500">
                            Technician: <span className="text-slate-700">{movement.performedByPersonnelName}</span>
                          </span>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </>
          ) : null}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end px-6 py-3.5 bg-slate-50 border-t border-slate-200 shrink-0">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
