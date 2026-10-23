/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

import React, { lazy, Suspense, useEffect, useEffectEvent, useState } from 'react';
import { Navbar } from './components/Navbar';
import { Sidebar } from './components/Sidebar';
import { BootOverlay } from './components/BootOverlay';
import { LoginView } from './views/LoginView';
import { ChangePasswordView } from './views/ChangePasswordView';
import { ItemDetailModal } from './components/ItemDetailModal';
import { DeployNoticeBanner } from './components/DeployNoticeBanner';
import { authApi, errorMessage, onUnauthorized, systemApi } from './services/api';
import { AppVersion, SignedInUser } from './types/inventory';

// Each screen is its own chunk, so the first load only fetches the shell and the dashboard.
const DashboardView = lazy(() => import('./views/DashboardView').then((m) => ({ default: m.DashboardView })));
const RegisterView = lazy(() => import('./views/RegisterView').then((m) => ({ default: m.RegisterView })));
const IssueForPackingView = lazy(() =>
  import('./views/IssueForPackingView').then((m) => ({ default: m.IssueForPackingView })),
);
const InventoryListView = lazy(() =>
  import('./views/InventoryListView').then((m) => ({ default: m.InventoryListView })),
);
const StoreLocationsView = lazy(() =>
  import('./views/StoreLocationsView').then((m) => ({ default: m.StoreLocationsView })),
);
const PersonnelView = lazy(() => import('./views/PersonnelView').then((m) => ({ default: m.PersonnelView })));

/** Backoff between health probes, in milliseconds. One entry per retry. */
const RETRY_DELAYS = [500, 1000, 2000, 3000, 4000, 5000, 5000];

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/** Probes /api/health with backoff until the server answers, then reads the current session. */
async function connect(onRetry: (attempt: number) => void): Promise<SignedInUser | null> {
  for (let index = 0; ; index += 1) {
    try {
      await systemApi.health();
      return await authApi.me();
    } catch (cause) {
      if (index === RETRY_DELAYS.length) throw cause;
      await wait(RETRY_DELAYS[index]);
      onRetry(index + 2);
    }
  }
}

export default function App() {
  const [activeView, setActiveView] = useState<string>('dashboard');
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState<boolean>(false);
  const [inspectedItemId, setInspectedItemId] = useState<string | null>(null);

  const [ready, setReady] = useState(false);
  const [attempt, setAttempt] = useState(1);
  const [bootFailed, setBootFailed] = useState(false);
  const [bootHint, setBootHint] = useState<string | null>(null);
  const [user, setUser] = useState<SignedInUser | null>(null);
  const [appVersion, setAppVersion] = useState<AppVersion | null>(null);

  const finishBoot = (connecting: Promise<SignedInUser | null>) =>
    connecting.then(
      (signedIn) => {
        setUser(signedIn);
        setReady(true);
      },
      (cause: unknown) => {
        setBootHint(errorMessage(cause, 'The inventory service did not respond.'));
        setBootFailed(true);
      },
    );

  const startBoot = useEffectEvent(() => void finishBoot(connect(setAttempt)));

  useEffect(() => {
    startBoot();
  }, []);

  const retryBoot = () => {
    setBootFailed(false);
    setBootHint(null);
    setAttempt(1);
    void finishBoot(connect(setAttempt));
  };

  // Footer version only; not worth blocking the app for.
  useEffect(() => {
    if (!ready) return;
    systemApi
      .version()
      .then(setAppVersion)
      .catch(() => undefined);
  }, [ready]);

  // A 401 from any call means the session ended; drop back to the sign-in screen.
  useEffect(() => {
    onUnauthorized(() => setUser(null));
    return () => onUnauthorized(null);
  }, []);

  const handleSignOut = async () => {
    try {
      await authApi.logout();
    } finally {
      setUser(null);
      setActiveView('dashboard');
    }
  };

  if (!ready) {
    return <BootOverlay attempt={attempt} failed={bootFailed} hint={bootHint} onRetry={retryBoot} />;
  }

  if (!user) {
    return (
      <>
        <LoginView onSignedIn={setUser} />
        <DeployNoticeBanner />
      </>
    );
  }

  if (user.mustChangePassword) {
    return (
      <>
        <ChangePasswordView onChanged={setUser} />
        <DeployNoticeBanner />
      </>
    );
  }

  const handleInspectItem = (id: string) => setInspectedItemId(id);

  const renderActiveView = () => {
    switch (activeView) {
      case 'register':
        return <RegisterView onInspectItem={handleInspectItem} />;
      case 'issue':
        return <IssueForPackingView onViewInventory={() => setActiveView('inventory')} />;
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
      case 'dashboard':
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
      <Navbar
        onToggleMobileSidebar={() => setIsMobileSidebarOpen((prev) => !prev)}
        activeView={activeView}
        userName={user.userName}
        onSignOut={handleSignOut}
      />

      <div className="flex-1 flex w-full">
        <Sidebar
          activeView={activeView}
          setActiveView={setActiveView}
          isMobileOpen={isMobileSidebarOpen}
          setIsMobileOpen={setIsMobileSidebarOpen}
          appVersion={appVersion}
        />

        <main className="flex-1 p-4 sm:p-6 lg:p-8 max-w-7xl mx-auto w-full overflow-x-hidden">
          <Suspense fallback={<div className="py-12 text-center text-xs text-slate-500">Loading…</div>}>
            {renderActiveView()}
          </Suspense>
        </main>
      </div>

      <ItemDetailModal itemId={inspectedItemId} onClose={() => setInspectedItemId(null)} />
      <DeployNoticeBanner />
    </div>
  );
}
