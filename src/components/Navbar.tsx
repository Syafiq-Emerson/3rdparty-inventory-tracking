import React, { useEffect, useState } from 'react';
import { Menu, Shield, Database, Radio, Clock } from 'lucide-react';

interface Props {
  onToggleMobileSidebar: () => void;
  activeView: string;
}

export const Navbar: React.FC<Props> = ({ onToggleMobileSidebar, activeView }) => {
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
    <header className="h-14 bg-white border-b border-slate-200 px-4 sm:px-6 flex items-center justify-between gap-4 sticky top-0 z-30">
      {/* Left: Mobile Toggle & Brand Header */}
      <div className="flex items-center gap-3">
        <button
          onClick={onToggleMobileSidebar}
          className="md:hidden p-1.5 text-slate-600 hover:text-slate-900 hover:bg-slate-100 rounded transition-colors"
          aria-label="Toggle navigation menu"
        >
          <Menu className="w-5 h-5" />
        </button>

        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded bg-[#004B87] flex items-center justify-center text-white font-bold text-xs tracking-wider shadow-xs">
            IMS
          </div>
          <div className="flex flex-col">
            <span className="text-sm font-bold tracking-tight text-slate-900 leading-tight">
              Industrial Asset Inventory
            </span>
            <span className="text-[11px] text-slate-500 hidden sm:inline">
              Precision Asset Custody & Dual-Barcode Ledger
            </span>
          </div>
        </div>
      </div>

      {/* Center/Right: Facility Context & Status Indicators */}
      <div className="flex items-center gap-4 text-xs">
        {/* Facility Telemetry Context */}
        <div className="hidden lg:flex items-center gap-3 text-slate-600 border-r border-slate-200 pr-4">
          <span className="flex items-center gap-1.5 font-medium text-slate-700">
            <Database className="w-3.5 h-3.5 text-[#004B87]" />
            MySQL 8.0 Engine
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
      </div>
    </header>
  );
};
