/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

import React, { useState } from 'react';
import { Navbar } from './components/Navbar';
import { Sidebar } from './components/Sidebar';
import { DashboardView } from './views/DashboardView';
import { RegisterView } from './views/RegisterView';
import { InventoryListView } from './views/InventoryListView';
import { StoreLocationsView } from './views/StoreLocationsView';
import { PersonnelView } from './views/PersonnelView';
import { SystemArchitectureView } from './views/SystemArchitectureView';
import { ItemDetailModal } from './components/ItemDetailModal';

export default function App() {
  const [activeView, setActiveView] = useState<string>('dashboard');
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState<boolean>(false);
  const [inspectedItemId, setInspectedItemId] = useState<string | null>(null);

  const handleInspectItem = (id: string) => {
    setInspectedItemId(id);
  };

  const renderActiveView = () => {
    switch (activeView) {
      case 'dashboard':
        return (
          <DashboardView
            onNavigate={(view) => setActiveView(view)}
            onInspectItem={handleInspectItem}
          />
        );
      case 'register':
        return (
          <RegisterView
            onSuccess={() => {
              // Stay on register for subsequent scans or allow navigation
            }}
            onInspectItem={handleInspectItem}
          />
        );
      case 'inventory':
        return (
          <InventoryListView
            onInspectItem={handleInspectItem}
            onNavigateToRegister={() => setActiveView('register')}
          />
        );
      case 'locations':
        return <StoreLocationsView />;
      case 'personnel':
        return <PersonnelView />;
      case 'architecture':
        return <SystemArchitectureView />;
      default:
        return (
          <DashboardView
            onNavigate={(view) => setActiveView(view)}
            onInspectItem={handleInspectItem}
          />
        );
    }
  };

  return (
    <div className="min-h-screen bg-[#F4F6F9] text-slate-800 flex flex-col font-sans antialiased selection:bg-[#004B87] selection:text-white">
      {/* Top Header Navigation */}
      <Navbar
        onToggleMobileSidebar={() => setIsMobileSidebarOpen((prev) => !prev)}
        activeView={activeView}
      />

      {/* Main Layout Container */}
      <div className="flex-1 flex w-full">
        {/* Navigation Sidebar */}
        <Sidebar
          activeView={activeView}
          setActiveView={setActiveView}
          isMobileOpen={isMobileSidebarOpen}
          setIsMobileOpen={setIsMobileSidebarOpen}
        />

        {/* Main Workspace Content Area */}
        <main className="flex-1 p-4 sm:p-6 lg:p-8 max-w-7xl mx-auto w-full overflow-x-hidden">
          {renderActiveView()}
        </main>
      </div>

      {/* Global Asset Detail Modal */}
      <ItemDetailModal
        itemId={inspectedItemId}
        onClose={() => setInspectedItemId(null)}
      />
    </div>
  );
}
