import React, { useEffect, useState } from 'react';
import { Menu, Database, Radio, Clock, MapPin, LogOut } from 'lucide-react';
import emersonLogo from '../assets/emerson_logo.svg';

interface Props {
  onToggleMobileSidebar: () => void;
  activeView: string;
  userName: string;
  onSignOut: () => void;
}

export const Navbar: React.FC<Props> = ({ onToggleMobileSidebar, userName, onSignOut }) => {
  const [timeString, setTimeString] = useState<string>('');

  useEffect(() => {
    const updateTime = () => {
      const now = new Date();
      setTimeString(
        now.toLocaleTimeString(undefined, {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
          hour12: false,
        })
      );
    };
    updateTime();
    const interval = setInterval(updateTime, 1000);
    return () => clearInterval(interval);
  }, []);

  return (
    <header className="min-h-14 bg-white border-b border-slate-200 px-4 py-2 sm:px-6 sm:py-0 flex flex-wrap items-center justify-between gap-4 sticky top-0 z-30">
      {/* Left: Mobile Toggle & Brand Header */}
      <div className="flex min-w-[120px] flex-1 items-center gap-3">
        <button
          onClick={onToggleMobileSidebar}
          className="md:hidden p-1.5 text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded transition-colors"
          aria-label="Toggle navigation menu"
        >
          <Menu className="w-5 h-5" />
        </button>

        <div className="flex min-w-0 items-center gap-3">
          <img
            src={emersonLogo}
            alt="Emerson Logo"
            className="h-8 w-auto object-contain shrink-0"
          />
          <div className="h-6 w-px bg-slate-200 hidden sm:block" />
          <div className="flex min-w-0 flex-col">
            <span className="truncate text-sm font-bold tracking-tight text-slate-900 leading-tight">
              Department of Third Party Inspection - Industrial Asset Inventory
            </span>
            <span className="text-[11px] text-slate-500 hidden sm:inline">
              Precision Asset Custody & Dual-Barcode Ledger
            </span>
          </div>
        </div>
      </div>

      {/* Center/Right: Facility Context & Status Indicators */}
      <div className="ml-auto flex shrink-0 items-center gap-2 text-xs sm:gap-4">
        {/* Facility Telemetry Context */}
        <div className="hidden lg:flex items-center gap-3 text-slate-600 border-r border-slate-200 pr-4">
          <span className="flex items-center gap-1.5 font-medium text-slate-700">
            <Database className="w-3.5 h-3.5 text-[#004B87]" />
            SQL Server
          </span>
          <span className="text-slate-300">·</span>
          <span className="text-slate-600">Plant Unit 04 / Facility North</span>
        </div>

        {/* Live Clock */}
        <div className="flex items-center gap-1.5 text-slate-500 font-mono tabular-nums text-xs">
          <Clock className="w-3.5 h-3.5 text-slate-400" />
          <span>{timeString}</span>
        </div>

        {/* Scanner Readiness Marker */}
        <div className="flex items-center gap-1.5 text-slate-700 font-medium">
          <Radio className="w-3.5 h-3.5 text-emerald-600" />
          <span className="hidden sm:inline text-[11px]">Scanner Listener Active</span>
        </div>

        <span className="inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded bg-teal-700 px-3 py-1 font-semibold text-white">
          <MapPin className="w-3.5 h-3.5" aria-hidden="true" />
          Nilai, Malaysia Plant
        </span>

        <div className="flex shrink-0 items-center gap-2 border-l border-slate-200 pl-2 sm:pl-4">
          <span className="hidden sm:inline text-[11px] font-semibold text-slate-700">{userName}</span>
          <button
            onClick={onSignOut}
            className="flex items-center gap-1.5 px-2.5 py-1 text-[11px] font-medium text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded transition-colors"
          >
            <LogOut className="w-3.5 h-3.5" />
            <span className="hidden sm:inline">Sign out</span>
          </button>
        </div>
      </div>
    </header>
  );
};
