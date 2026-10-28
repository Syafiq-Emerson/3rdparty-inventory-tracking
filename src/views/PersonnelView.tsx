import React, { useState } from 'react';
import { Plus, Edit2, Power, X, AlertCircle, UserCheck, ShieldAlert } from 'lucide-react';
import { Personnel } from '../types/inventory';
import { errorMessage, inventoryApi } from '../services/api';
import { useAsync } from '../hooks/useAsync';

export const PersonnelView: React.FC = () => {
  const {
    data,
    loading: loadingAll,
    error,
    reload,
  } = useAsync(() => inventoryApi.getPersonnel(false), null, 'Failed to fetch personnel directory.');
  const personnelList = data ?? [];
  // Only the first load replaces the table; later refreshes keep it on screen.
  const loading = loadingAll && data === null;

  // Modal / Form state for Add or Edit
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);
  const [editingPerson, setEditingPerson] = useState<Personnel | null>(null);
  const [formCode, setFormCode] = useState<string>('');
  const [formName, setFormName] = useState<string>('');
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const handleOpenAdd = () => {
    setEditingPerson(null);
    setFormCode('');
    setFormName('');
    setFormError(null);
    setIsModalOpen(true);
  };

  const handleOpenEdit = (person: Personnel) => {
    setEditingPerson(person);
    setFormCode(person.personnelCode);
    setFormName(person.name);
    setFormError(null);
    setIsModalOpen(true);
  };

  const handleToggleStatus = async (person: Personnel) => {
    try {
      await inventoryApi.togglePersonnelStatus(person.id, !person.isActive);
      reload();
    } catch (cause: unknown) {
      alert(errorMessage(cause, 'Failed to toggle personnel status.'));
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formCode.trim() || !formName.trim()) {
      setFormError('Both Personnel ID and Full Name are required.');
      return;
    }

    setIsSaving(true);
    setFormError(null);

    try {
      if (editingPerson) {
        await inventoryApi.updatePersonnel(editingPerson.id, formCode.trim(), formName.trim());
      } else {
        await inventoryApi.createPersonnel(formCode.trim(), formName.trim());
      }
      setIsModalOpen(false);
      reload();
    } catch (cause: unknown) {
      setFormError(errorMessage(cause, 'Failed to save personnel record.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Top Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-200">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-slate-900">
            Settings: Personnel Directory
          </h1>
          <p className="text-xs text-slate-500 mt-0.5">
            Manage authorized plant personnel for "Scan By" attribution. Deactivation preserves historical custody records.
          </p>
        </div>

        <button
          onClick={handleOpenAdd}
          className="flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors shadow-2xs"
        >
          <Plus className="w-4 h-4" />
          <span>Add Personnel Record</span>
        </button>
      </div>

      {/* Info notice about authentication scope */}
      <div className="p-3 bg-slate-50 border border-slate-200 rounded text-xs text-slate-600 flex items-start gap-2">
        <ShieldAlert className="w-4 h-4 text-slate-500 shrink-0 mt-0.5" />
        <div>
          <span className="font-semibold text-slate-800">Operational Notice:</span> "Scan By" selects the accountable technician conducting registration on the floor. In accordance with enterprise specifications, this initial version operates without verified user authentication.
        </div>
      </div>

      {/* Main Table */}
      <div className="bg-white rounded-lg border border-slate-200 shadow-2xs overflow-hidden">
        {loading ? (
          <div className="py-20 text-center text-slate-500 text-xs">
            <div className="animate-spin w-7 h-7 border-2 border-[#004B87] border-t-transparent rounded-full mx-auto mb-2.5" />
            Loading personnel directory from database...
          </div>
        ) : error ? (
          <div className="p-6 text-center text-red-800 text-xs bg-red-50">
            {error}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-slate-50 text-slate-600 border-b border-slate-200 font-semibold">
                  <th className="py-3 px-4">Personnel ID</th>
                  <th className="py-3 px-4">Technician / Employee Name</th>
                  <th className="py-3 px-4">Status</th>
                  <th className="py-3 px-4">Cataloged Units</th>
                  <th className="py-3 px-4">Date Added</th>
                  <th className="py-3 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {personnelList.map((person) => (
                  <tr
                    key={person.id}
                    className={`hover:bg-slate-50/80 transition-colors ${
                      !person.isActive ? 'opacity-60 bg-slate-50/40' : ''
                    }`}
                  >
                    {/* Personnel ID / Code */}
                    <td className="py-3 px-4 font-mono font-bold text-slate-900">
                      {person.personnelCode}
                    </td>

                    {/* Name */}
                    <td className="py-3 px-4 font-medium text-slate-800">
                      {person.name}
                    </td>

                    {/* Status */}
                    <td className="py-3 px-4">
                      {person.isActive ? (
                        <span className="inline-flex items-center gap-1 text-emerald-800 font-medium">
                          <span className="w-1.5 h-1.5 rounded-full bg-emerald-600" />
                          Active
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-slate-500">
                          <span className="w-1.5 h-1.5 rounded-full bg-slate-400" />
                          Inactive
                        </span>
                      )}
                    </td>

                    {/* Cataloged Units */}
                    <td className="py-3 px-4 font-mono tabular-nums text-slate-600">
                      <span className="font-semibold text-slate-800">{person.registeredItemsCount || 0}</span> unit{(person.registeredItemsCount || 0) === 1 ? '' : 's'}
                    </td>

                    {/* Date Added */}
                    <td className="py-3 px-4 font-mono tabular-nums text-slate-400">
                      {new Date(person.createdAt).toLocaleDateString()}
                    </td>

                    {/* Actions */}
                    <td className="py-3 px-4 text-right space-x-2">
                      <button
                        onClick={() => handleOpenEdit(person)}
                        className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-100 border border-slate-300 rounded transition-colors"
                      >
                        <Edit2 className="w-3 h-3 text-slate-500" />
                        <span>Edit</span>
                      </button>

                      <button
                        onClick={() => handleToggleStatus(person)}
                        className={`inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded border transition-colors ${
                          person.isActive
                            ? 'text-amber-800 hover:bg-amber-50 border-amber-300'
                            : 'text-emerald-800 hover:bg-emerald-50 border-emerald-300'
                        }`}
                        title={
                          person.isActive
                            ? 'Deactivate personnel (preserves historical registration logs)'
                            : 'Re-activate personnel'
                        }
                      >
                        <Power className="w-3 h-3" />
                        <span>{person.isActive ? 'Deactivate' : 'Activate'}</span>
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Add / Edit Personnel Modal Dialog */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs">
          <div className="w-full max-w-md bg-white rounded-lg shadow-xl border border-slate-200 overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-center justify-between px-6 py-4 bg-slate-900 text-white">
              <div className="flex items-center gap-2.5">
                <UserCheck className="w-4 h-4 text-blue-400" />
                <h3 className="text-sm font-semibold">
                  {editingPerson ? 'Edit Personnel Record' : 'Add New Personnel'}
                </h3>
              </div>
              <button
                onClick={() => setIsModalOpen(false)}
                className="text-slate-400 hover:text-white"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleSave} className="p-6 space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">
                  Personnel ID / Badge Code <span className="text-red-600">*</span> (Unique)
                </label>
                <input
                  type="text"
                  required
                  value={formCode}
                  onChange={(e) => setFormCode(e.target.value.toUpperCase())}
                  placeholder="Personnel code"
                  className="w-full bg-white border border-slate-300 rounded px-3 py-2 text-xs font-mono uppercase focus:border-[#004B87] outline-hidden"
                />
                <span className="text-[11px] text-slate-400 mt-0.5 block">
                  Employee badge or payroll identification number.
                </span>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">
                  Full Name & Title <span className="text-red-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={formName}
                  onChange={(e) => setFormName(e.target.value)}
                  placeholder="Full name"
                  className="w-full bg-white border border-slate-300 rounded px-3 py-2 text-xs focus:border-[#004B87] outline-hidden"
                />
                <span className="text-[11px] text-slate-400 mt-0.5 block">
                  First and last name along with optional craft specialization.
                </span>
              </div>

              {formError && (
                <div className="p-2.5 bg-red-50 border border-red-200 rounded text-red-800 text-xs flex items-center gap-1.5">
                  <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
                  <span>{formError}</span>
                </div>
              )}

              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-3.5 py-1.5 font-medium text-slate-600 hover:text-slate-900 border border-slate-300 rounded"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isSaving}
                  className="px-4 py-1.5 font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded disabled:opacity-50"
                >
                  {isSaving ? 'Saving...' : editingPerson ? 'Update Personnel' : 'Create Record'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
