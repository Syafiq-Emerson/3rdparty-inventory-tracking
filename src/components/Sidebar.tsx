import React from 'react';
import {
  LayoutDashboard,
  Barcode,
  Search,
  MapPin,
  Users,
  Server,
  Layers,
  ChevronRight,
  X,
  FileCode,
} from 'lucide-react';

interface Props {
  activeView: string;
  setActiveView: (view: string) => void;
  isMobileOpen: boolean;
  setIsMobileOpen: (open: boolean) => void;
}

export const Sidebar: React.FC<Props> = ({
  activeView,
  setActiveView,
  isMobileOpen,
  setIsMobileOpen,
}) => {
  const navItems = [
    {
      id: 'dashboard',
      label: 'Dashboard',
      icon: LayoutDashboard,
      badge: null,
    },
    {
      id: 'register',
      label: 'Register Inventory',
      icon: Barcode,
      badge: 'Dual Scan',
    },
    {
      id: 'inventory',
      label: 'Inventory List / Search',
      icon: Search,
      badge: null,
    },
  ];

  const settingsItems = [
    {
      id: 'locations',
      label: 'Store Locations',
      icon: MapPin,
    },
    {
      id: 'personnel',
      label: 'Personnel',
      icon: Users,
    },
  ];

  const technicalItems = [
    {
      id: 'architecture',
      label: 'ASP.NET & MySQL Spec',
      icon: Server,
    },
  ];

  const handleSelect = (viewId: string) => {
    setActiveView(viewId);
    setIsMobileOpen(false);
  };

  const content = (
    <div className="flex flex-col h-full bg-[#0B1E36] text-slate-300 select-none">
      {/* Brand Header */}
      <div className="p-5 border-b border-slate-800/80 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded bg-[#004B87] text-white flex items-center justify-center font-bold text-sm tracking-wide shadow-xs">
            <Layers className="w-4 h-4 text-white" />
          </div>
          <div>
            <div className="text-xs uppercase tracking-wider text-slate-400 font-semibold font-mono">
              Enterprise AMS
            </div>
            <div className="text-sm font-bold text-white tracking-tight">
              Inventory System
            </div>
          </div>
        </div>

        {/* Mobile Close Button */}
        <button
          onClick={() => setIsMobileOpen(false)}
          className="md:hidden p-1 text-slate-400 hover:text-white rounded"
        >
          <X className="w-5 h-5" />
        </button>
      </div>

      {/* Nav Menu */}
      <div className="flex-1 py-5 px-3 space-y-6 overflow-y-auto">
        {/* Main Operations */}
        <div>
          <div className="px-3 mb-2 text-[10px] uppercase font-mono tracking-wider text-slate-400 font-semibold">
            Operations
          </div>
          <nav className="space-y-1">
            {navItems.map((item) => {
              const Icon = item.icon;
              const isActive = activeView === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => handleSelect(item.id)}
                  className={`w-full flex items-center justify-between px-3 py-2.5 rounded text-xs font-medium transition-all ${
                    isActive
                      ? 'bg-[#004B87] text-white shadow-xs font-semibold'
                      : 'text-slate-300 hover:bg-slate-800/70 hover:text-white'
                  }`}
                >
                  <div className="flex items-center gap-2.5">
                    <Icon
                      className={`w-4 h-4 shrink-0 ${
                        isActive ? 'text-white' : 'text-slate-400'
                      }`}
                    />
                    <span className="truncate">{item.label}</span>
                  </div>
                  {item.badge && (
                    <span
                      className={`text-[10px] font-mono px-1.5 py-0.5 rounded ${
                        isActive
                          ? 'bg-blue-900/80 text-blue-100'
                          : 'bg-slate-800 text-slate-300'
                      }`}
                    >
                      {item.badge}
                    </span>
                  )}
                </button>
              );
            })}
          </nav>
        </div>

        {/* Master Settings */}
        <div>
          <div className="px-3 mb-2 text-[10px] uppercase font-mono tracking-wider text-slate-400 font-semibold">
            Settings & Master Data
          </div>
          <nav className="space-y-1">
            {settingsItems.map((item) => {
              const Icon = item.icon;
              const isActive = activeView === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => handleSelect(item.id)}
                  className={`w-full flex items-center justify-between px-3 py-2.5 rounded text-xs font-medium transition-all ${
                    isActive
                      ? 'bg-[#004B87] text-white shadow-xs font-semibold'
                      : 'text-slate-300 hover:bg-slate-800/70 hover:text-white'
                  }`}
                >
                  <div className="flex items-center gap-2.5">
                    <Icon
                      className={`w-4 h-4 shrink-0 ${
                        isActive ? 'text-white' : 'text-slate-400'
                      }`}
                    />
                    <span className="truncate">{item.label}</span>
                  </div>
                  <ChevronRight className="w-3.5 h-3.5 text-slate-500" />
                </button>
              );
            })}
          </nav>
        </div>

        {/* System & Architecture */}
        <div>
          <div className="px-3 mb-2 text-[10px] uppercase font-mono tracking-wider text-slate-400 font-semibold">
            System & Deployment
          </div>
          <nav className="space-y-1">
            {technicalItems.map((item) => {
              const Icon = item.icon;
              const isActive = activeView === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => handleSelect(item.id)}
                  className={`w-full flex items-center justify-between px-3 py-2.5 rounded text-xs font-medium transition-all ${
                    isActive
                      ? 'bg-[#004B87] text-white shadow-xs font-semibold'
                      : 'text-slate-300 hover:bg-slate-800/70 hover:text-white'
                  }`}
                >
                  <div className="flex items-center gap-2.5">
                    <Icon
                      className={`w-4 h-4 shrink-0 ${
                        isActive ? 'text-white' : 'text-slate-400'
                      }`}
                    />
                    <span className="truncate">{item.label}</span>
                  </div>
                  <ChevronRight className="w-3.5 h-3.5 text-slate-500" />
                </button>
              );
            })}
          </nav>
        </div>
      </div>

      {/* Footer Info */}
      <div className="p-4 border-t border-slate-800/80 text-[11px] text-slate-400 bg-slate-950/40">
        <div className="flex items-center gap-2 text-slate-300 font-medium mb-1">
          <FileCode className="w-3.5 h-3.5 text-blue-400" />
          <span>Industrial Compliance</span>
        </div>
        <div className="text-slate-500 leading-tight">
          ISA-5.1 Instrumentation Tags & ASME Physical Serials
        </div>
      </div>
    </div>
  );

  return (
    <>
      {/* Desktop Persistent Sidebar */}
      <aside className="hidden md:block w-64 shrink-0 border-r border-slate-200 h-[calc(100vh-3.5rem)] sticky top-14">
        {content}
      </aside>

      {/* Mobile Drawer */}
      {isMobileOpen && (
        <div className="fixed inset-0 z-40 md:hidden flex">
          <div
            className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs"
            onClick={() => setIsMobileOpen(false)}
          />
          <div className="relative w-72 max-w-[80vw] h-full z-50">
            {content}
          </div>
        </div>
      )}
    </>
  );
};
