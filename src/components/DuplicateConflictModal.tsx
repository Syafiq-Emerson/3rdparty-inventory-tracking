import React from 'react';
import { AlertTriangle, ArrowRight, X, MapPin, User, Calendar, Hash } from 'lucide-react';
import { DuplicateConflictError } from '../types/inventory';

interface Props {
  conflict: DuplicateConflictError | null;
  onClose: () => void;
  onViewExisting: (itemId: string) => void;
}

export const DuplicateConflictModal: React.FC<Props> = ({ conflict, onClose, onViewExisting }) => {
  if (!conflict) return null;

  const { conflictingField, conflictingValue, existingItem } = conflict;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs">
      <div className="w-full max-w-lg bg-white rounded-lg shadow-xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 bg-amber-50 border-b border-amber-200">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-amber-100 rounded text-amber-800">
              <AlertTriangle className="w-5 h-5 text-amber-700" />
            </div>
            <div>
              <h3 className="text-base font-semibold text-amber-950">
                Registration Blocked: Duplicate Identifier
              </h3>
              <p className="text-xs text-amber-800">
                A matching record already exists in the industrial database.
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1 text-slate-400 hover:text-slate-600 rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-4 text-sm">
          <div className="p-3 bg-slate-50 border border-slate-200 rounded text-slate-700">
            <span className="font-semibold text-slate-900">
              {conflictingField === 'SerialNumber' ? 'Serial Number' : 'Tag Number'} Conflict:
            </span>{' '}
            <code className="font-mono font-bold text-amber-800 bg-amber-100/60 px-1.5 py-0.5 rounded">
              {conflictingValue}
            </code>
            <p className="text-xs text-slate-500 mt-1">
              Database integrity constraints prevent duplicate physical unit entries. Registration has been rolled back.
            </p>
          </div>

          <div>
            <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-500 mb-2">
              Existing Registered Item Details
            </h4>
            <div className="divide-y divide-slate-100 border border-slate-200 rounded bg-white overflow-hidden text-xs">
              <div className="flex justify-between items-center px-4 py-2.5">
                <span className="text-slate-500 flex items-center gap-1.5">
                  <Hash className="w-3.5 h-3.5" /> Serial Number
                </span>
                <span className="font-mono font-medium text-slate-900">
                  {existingItem.serialNumber}
                </span>
              </div>
              <div className="flex justify-between items-center px-4 py-2.5">
                <span className="text-slate-500 flex items-center gap-1.5">
                  <Hash className="w-3.5 h-3.5" /> Tag Number
                </span>
                <span className="font-mono font-medium text-slate-900">
                  {existingItem.tagNumber}
                </span>
              </div>
              <div className="flex justify-between items-center px-4 py-2.5 bg-blue-50/40">
                <span className="text-slate-700 font-medium flex items-center gap-1.5">
                  <MapPin className="w-3.5 h-3.5 text-blue-700" /> Current Store Location
                </span>
                <span className="font-semibold text-blue-900">
                  {existingItem.locationName} ({existingItem.locationCode})
                </span>
              </div>
              <div className="flex justify-between items-center px-4 py-2.5">
                <span className="text-slate-500 flex items-center gap-1.5">
                  <User className="w-3.5 h-3.5" /> Registered By
                </span>
                <span className="text-slate-800 font-medium">
                  {existingItem.registeredByPersonnelName}
                </span>
              </div>
              <div className="flex justify-between items-center px-4 py-2.5">
                <span className="text-slate-500 flex items-center gap-1.5">
                  <Calendar className="w-3.5 h-3.5" /> Registration Timestamp
                </span>
                <span className="text-slate-700 tabular-nums">
                  {new Date(existingItem.createdAt).toLocaleString()}
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 px-6 py-3.5 bg-slate-50 border-t border-slate-200">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition-colors"
          >
            Dismiss & Rescan
          </button>
          <button
            onClick={() => {
              onClose();
              onViewExisting(existingItem.id);
            }}
            className="flex items-center gap-1.5 px-4 py-2 text-xs font-semibold text-white bg-blue-800 rounded hover:bg-blue-900 transition-colors shadow-xs"
          >
            View Existing Record <ArrowRight className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </div>
  );
};
