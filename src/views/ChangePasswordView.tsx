import React, { useState } from 'react';
import { Lock } from '../components/icons';
import { authApi, errorMessage } from '../services/api';
import { SignedInUser } from '../types/inventory';

const MINIMUM_LENGTH = 12;

interface Props {
  onChanged: (user: SignedInUser) => void;
}

/** Shown when the account still carries MustChangePassword, before any other screen. */
export const ChangePasswordView: React.FC<Props> = ({ onChanged }) => {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy) return;

    if (newPassword !== confirmPassword) {
      setError('The two new passwords do not match.');
      return;
    }

    setBusy(true);
    setError(null);
    try {
      onChanged(await authApi.changePassword(currentPassword, newPassword));
    } catch (cause) {
      setError(errorMessage(cause, 'Could not change the password. Try again.'));
    } finally {
      setBusy(false);
    }
  };

  const field = (
    id: string,
    label: string,
    value: string,
    setter: (next: string) => void,
    autoComplete: string
  ) => (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-xs font-semibold text-slate-700">
        {label}
      </label>
      <div className="relative">
        <Lock className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
        <input
          id={id}
          type="password"
          autoComplete={autoComplete}
          value={value}
          onChange={(e) => setter(e.target.value)}
          className="w-full pl-9 pr-3 py-2 bg-slate-50/50 border border-slate-300 rounded text-sm text-slate-900 focus:bg-white focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87] outline-hidden transition-colors"
        />
      </div>
    </div>
  );

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#F4F6F9] px-6 font-sans">
      <div className="w-full max-w-sm">
        <div className="text-center mb-6">
          <h1 className="text-lg font-bold tracking-tight text-slate-900">Choose a new password</h1>
          <p className="text-xs text-slate-500 mt-1">
            This account still uses its setup password. Pick a new one of at least {MINIMUM_LENGTH} characters.
          </p>
        </div>

        <form
          onSubmit={handleSubmit}
          className="bg-white rounded-lg border border-slate-200 shadow-2xs p-6 space-y-4"
        >
          {field('currentPassword', 'Current password', currentPassword, setCurrentPassword, 'current-password')}
          {field('newPassword', 'New password', newPassword, setNewPassword, 'new-password')}
          {field('confirmPassword', 'Confirm new password', confirmPassword, setConfirmPassword, 'new-password')}

          {error && (
            <div className="p-3 bg-red-50 border border-red-200 rounded text-xs text-red-800" role="alert">
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={busy || newPassword.length < MINIMUM_LENGTH || !currentPassword}
            className="w-full py-2.5 text-sm font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors disabled:opacity-40 disabled:pointer-events-none"
          >
            {busy ? 'Saving…' : 'Save password'}
          </button>
        </form>
      </div>
    </div>
  );
};
