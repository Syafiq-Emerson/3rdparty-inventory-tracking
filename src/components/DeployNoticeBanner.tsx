import React from 'react';
import { RefreshCw } from 'lucide-react';
import { useDeployNotice } from '../hooks/useDeployNotice';

/** Warns open pages before an IIS publish, then covers the app while the files are copied. */
export const DeployNoticeBanner: React.FC = () => {
  const { pending, updating, secondsLeft, message } = useDeployNotice();

  if (updating) {
    return (
      <div
        className="fixed inset-0 flex items-center justify-center bg-[#F4F6F9]/95 px-6 text-center"
        style={{ zIndex: 3100 }}
        role="alert"
        aria-live="assertive"
      >
        <div className="max-w-sm">
          <div className="mx-auto mb-4 h-10 w-10 animate-spin motion-reduce:animate-none rounded-full border-4 border-[#004B87] border-t-transparent" />
          <h1 className="text-lg font-bold text-slate-900">Industrial Inventory is being updated</h1>
          <p className="mt-2 text-xs leading-relaxed text-slate-600">
            This usually takes a minute or two. The page reloads by itself when the update is finished.
          </p>
        </div>
      </div>
    );
  }

  if (!pending) return null;

  return (
    <div
      className="fixed inset-x-0 top-3 flex justify-center px-4"
      style={{ zIndex: 3100 }}
      role="status"
      aria-live="polite"
    >
      <div className="flex w-full max-w-lg items-start gap-3 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-amber-900 shadow-lg">
        <RefreshCw className="mt-0.5 h-4 w-4 shrink-0" />
        <div className="text-xs leading-relaxed">
          <div className="text-sm font-semibold">Update starts in {secondsLeft} s</div>
          <div>{message}</div>
        </div>
      </div>
    </div>
  );
};
