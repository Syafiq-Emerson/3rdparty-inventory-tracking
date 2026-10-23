import React from 'react';
import { LayoutGrid, LanConnectIcon } from './icons';

interface Props {
  attempt: number;
  failed: boolean;
  hint: string | null;
  onRetry: () => void;
}

/**
 * Covers the app until the server answers /api/health, so no screen renders
 * against a backend that is not there.
 */
export const BootOverlay: React.FC<Props> = ({ attempt, failed, hint, onRetry }) => (
  <div
    className="fixed inset-0 z-3000 flex flex-col items-center justify-center bg-[#F4F6F9] px-6 text-center"
    style={{ zIndex: 3000 }}
    role="status"
    aria-live="polite"
  >
    {failed ? (
      <>
        <div className="w-14 h-14 rounded-full bg-red-50 text-red-600 flex items-center justify-center mb-4">
          <LanConnectIcon className="w-7 h-7" />
        </div>
        <h1 className="text-lg font-bold text-slate-900">Can&apos;t reach the server</h1>
        <p className="mt-2 max-w-sm text-xs leading-relaxed text-slate-600">
          {hint ?? 'The inventory service did not respond. It may be restarting, or the network may be down.'}
        </p>
        <button
          onClick={onRetry}
          className="mt-6 px-6 py-2.5 text-sm font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors"
        >
          Retry
        </button>
      </>
    ) : (
      <>
        <div className="w-12 h-12 rounded bg-[#004B87] text-white flex items-center justify-center mb-5">
          <LayoutGrid className="w-6 h-6" />
        </div>
        <div className="animate-spin w-8 h-8 border-2 border-[#004B87] border-t-transparent rounded-full" />
        <p className="mt-4 text-xs font-medium text-slate-600">
          {attempt <= 1 ? 'Loading…' : `Connecting to server… (attempt ${attempt})`}
        </p>
      </>
    )}
  </div>
);
