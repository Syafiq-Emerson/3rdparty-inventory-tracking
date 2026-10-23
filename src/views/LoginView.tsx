import React, { useState } from 'react';
import { LayoutGrid, Lock, UserIcon } from '../components/icons';
import { authApi, errorMessage } from '../services/api';
import { SignedInUser } from '../types/inventory';

interface Props {
  onSignedIn: (user: SignedInUser) => void;
}

export const LoginView: React.FC<Props> = ({ onSignedIn }) => {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy) return;

    setBusy(true);
    setError(null);
    try {
      onSignedIn(await authApi.login(userName.trim(), password));
    } catch (cause) {
      setError(errorMessage(cause, 'Could not sign in. Try again.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#F4F6F9] px-6 font-sans">
      <div className="w-full max-w-sm">
        <div className="flex flex-col items-center mb-7">
          <div className="w-12 h-12 rounded bg-[#004B87] text-white flex items-center justify-center mb-3">
            <LayoutGrid className="w-6 h-6" />
          </div>
          <h1 className="text-lg font-bold tracking-tight text-slate-900">Industrial Inventory</h1>
          <p className="text-xs text-slate-500 mt-1">Sign in to continue.</p>
        </div>

        <form
          onSubmit={handleSubmit}
          className="bg-white rounded-lg border border-slate-200 shadow-2xs p-6 space-y-4"
        >
          <div className="space-y-1.5">
            <label htmlFor="userName" className="block text-xs font-semibold text-slate-700">
              User name
            </label>
            <div className="relative">
              <UserIcon className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
              <input
                id="userName"
                name="username"
                autoComplete="username"
                autoFocus
                value={userName}
                onChange={(e) => setUserName(e.target.value)}
                className="w-full pl-9 pr-3 py-2 bg-slate-50/50 border border-slate-300 rounded text-sm text-slate-900 focus:bg-white focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87] outline-hidden transition-colors"
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <label htmlFor="password" className="block text-xs font-semibold text-slate-700">
              Password
            </label>
            <div className="relative">
              <Lock className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
              <input
                id="password"
                name="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full pl-9 pr-3 py-2 bg-slate-50/50 border border-slate-300 rounded text-sm text-slate-900 focus:bg-white focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87] outline-hidden transition-colors"
              />
            </div>
          </div>

          {error && (
            <div className="p-3 bg-red-50 border border-red-200 rounded text-xs text-red-800" role="alert">
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={busy || !userName.trim() || !password}
            className="w-full py-2.5 text-sm font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors disabled:opacity-40 disabled:pointer-events-none"
          >
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  );
};
