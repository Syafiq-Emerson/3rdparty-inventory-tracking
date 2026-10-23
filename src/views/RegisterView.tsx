import React, { lazy, Suspense, useEffect, useEffectEvent, useRef, useState } from 'react';
import {
  Barcode,
  Camera,
  CheckCircle2,
  AlertCircle,
  ArrowLeftRight,
  RefreshCw,
  Building2,
  RotateCcw,
  Radio,
  SlidersHorizontal,
  Info,
} from 'lucide-react';
import { StoreLocation, Personnel, InventoryItem, DuplicateConflictError } from '../types/inventory';
import { errorMessage, inventoryApi, isDuplicateConflictError } from '../services/api';
import { HandheldScannerListener, defaultClassificationRules, placeScan, ScanTone } from '../services/scanner';
import { DuplicateConflictModal } from '../components/DuplicateConflictModal';

// The camera decoder is large; load it only when someone opens the scanner.
const CameraScannerModal = lazy(() => import('../components/CameraScannerModal'));

const NOTIFICATION_MS = 4000;

interface Props {
  onInspectItem: (itemId: string) => void;
}

export const RegisterView: React.FC<Props> = ({ onInspectItem }) => {
  // Form State (4 Mandatory Fields)
  const [serialNumber, setSerialNumber] = useState<string>('');
  const [tagNumber, setTagNumber] = useState<string>('');
  const [storeLocationId, setStoreLocationId] = useState<string>('');
  const [scanByPersonnelId, setScanByPersonnelId] = useState<string>('');

  // Dropdown Master Records (Loaded dynamically)
  const [locations, setLocations] = useState<StoreLocation[]>([]);
  const [personnel, setPersonnel] = useState<Personnel[]>([]);
  const [loadingDropdowns, setLoadingDropdowns] = useState<boolean>(true);

  // Scanner & Modal States
  const [isCameraModalOpen, setIsCameraModalOpen] = useState<boolean>(false);
  const [duplicateConflict, setDuplicateConflict] = useState<DuplicateConflictError | null>(null);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [registeredSuccessItem, setRegisteredSuccessItem] = useState<InventoryItem | null>(null);
  const [scannerNotification, setScannerNotification] = useState<{ text: string; type: ScanTone } | null>(null);
  const [showConfigRules, setShowConfigRules] = useState<boolean>(false);
  const notificationTimer = useRef<number | undefined>(undefined);

  useEffect(() => {
    let cancelled = false;
    Promise.all([inventoryApi.getLocations(true), inventoryApi.getPersonnel(true)])
      .then(([locs, staff]) => {
        if (cancelled) return;
        setLocations(locs);
        setPersonnel(staff);
        // Nothing to choose when there is only one option.
        if (locs.length === 1) setStoreLocationId(locs[0].id);
        if (staff.length === 1) setScanByPersonnelId(staff[0].id);
      })
      .catch((cause: unknown) => {
        if (!cancelled) setSubmitError(errorMessage(cause, 'Could not load the active store locations and personnel.'));
      })
      .finally(() => {
        if (!cancelled) setLoadingDropdowns(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => () => window.clearTimeout(notificationTimer.current), []);

  const notify = (text: string, type: ScanTone = 'info') => {
    window.clearTimeout(notificationTimer.current);
    setScannerNotification({ text, type });
    notificationTimer.current = window.setTimeout(() => setScannerNotification(null), NOTIFICATION_MS);
  };

  const handleScan = useEffectEvent((rawText: string) => {
    const placement = placeScan({ serial: serialNumber, tag: tagNumber }, rawText);
    if (!placement) return;
    if (placement.changed) {
      setSerialNumber(placement.slots.serial);
      setTagNumber(placement.slots.tag);
    }
    notify(placement.message, placement.tone);
  });

  // Keyboard-wedge scanners type the barcode and press Enter; the listener collects those keystrokes.
  useEffect(() => {
    const listener = new HandheldScannerListener((text) => handleScan(text));
    listener.start();
    return () => listener.stop();
  }, []);

  const handleSwapIdentifiers = () => {
    setSerialNumber(tagNumber);
    setTagNumber(serialNumber);
    notify('Swapped Serial Number and Tag Number assignments.', 'info');
  };

  const handleClearForm = () => {
    setSerialNumber('');
    setTagNumber('');
    setRegisteredSuccessItem(null);
    setSubmitError(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!serialNumber.trim() || !tagNumber.trim() || !storeLocationId || !scanByPersonnelId) {
      setSubmitError('All four fields are mandatory before committing to the database.');
      return;
    }

    setIsSubmitting(true);
    setSubmitError(null);
    setDuplicateConflict(null);

    try {
      const newItem = await inventoryApi.registerInventory({
        serialNumber: serialNumber.trim(),
        tagNumber: tagNumber.trim(),
        storeLocationId,
        scanByPersonnelId,
      });

      setRegisteredSuccessItem(newItem);
      notify(`Asset "${newItem.tagNumber}" successfully registered in database.`, 'success');

      // Clear the scanned identifiers for next intake while preserving location/personnel defaults
      setSerialNumber('');
      setTagNumber('');
    } catch (cause: unknown) {
      if (isDuplicateConflictError(cause)) {
        setDuplicateConflict(cause);
      } else {
        setSubmitError(errorMessage(cause, 'An error occurred while registering the inventory item.'));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const isFormComplete = Boolean(
    serialNumber.trim() && tagNumber.trim() && storeLocationId && scanByPersonnelId
  );

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      {/* Top Banner / Breadcrumb */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-200">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-slate-900">
            Register Industrial Inventory
          </h1>
          <p className="text-xs text-slate-500 mt-0.5">
            Ingest equipment by scanning dual physical barcodes (Serial Number & Tag Number).
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => setShowConfigRules((prev) => !prev)}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 transition-colors shadow-2xs"
          >
            <SlidersHorizontal className="w-3.5 h-3.5 text-slate-500" />
            <span>Classification Rules</span>
          </button>

          <button
            onClick={() => setIsCameraModalOpen(true)}
            className="flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-semibold text-white bg-[#004B87] hover:bg-[#003866] rounded transition-colors shadow-2xs"
          >
            <Camera className="w-3.5 h-3.5" />
            <span>Camera Barcode Scanner</span>
          </button>
        </div>
      </div>

      {/* Handheld Scanner Status Bar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between px-4 py-2.5 bg-slate-900 text-white rounded-lg text-xs gap-3">
        <div className="flex items-center gap-2.5">
          <div className="w-2.5 h-2.5 rounded-full bg-emerald-400 animate-pulse" />
          <span className="font-semibold text-slate-200">
            Handheld Scanner Listener Active
          </span>
          <span className="text-slate-400 hidden md:inline">
            (USB & Bluetooth keyboard wedge mode ready)
          </span>
        </div>
      </div>

      {/* Floating Scanner Notification Banner */}
      {scannerNotification && (
        <div
          className={`px-4 py-2.5 rounded-lg border text-xs font-medium flex items-center justify-between animate-in fade-in duration-150 ${
            scannerNotification.type === 'success'
              ? 'bg-emerald-50 border-emerald-200 text-emerald-900'
              : scannerNotification.type === 'warning'
              ? 'bg-amber-50 border-amber-200 text-amber-900'
              : 'bg-blue-50 border-blue-200 text-blue-900'
          }`}
        >
          <div className="flex items-center gap-2">
            <Radio className="w-4 h-4 shrink-0" />
            <span>{scannerNotification.text}</span>
          </div>
          <button
            onClick={() => setScannerNotification(null)}
            className="text-xs opacity-60 hover:opacity-100"
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Configurable Classification Rules Box (Collapsible) */}
      {showConfigRules && (
        <div className="p-4 bg-slate-50 border border-slate-200 rounded-lg text-xs space-y-3">
          <span className="font-semibold text-slate-800 flex items-center gap-1.5">
            <Info className="w-4 h-4 text-slate-500" />
            Barcode Classification Rules
          </span>
          <p className="text-slate-600 leading-relaxed">
            Scanned values without prefixes are classified into Serial Number or Tag Number via regular expression patterns.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 font-mono text-[11px]">
            <div className="p-2.5 bg-white border border-slate-200 rounded">
              <div className="font-semibold text-slate-700 mb-1">Tag Number Pattern (ISA-5.1)</div>
              <div className="text-slate-500">{defaultClassificationRules.tagNumberRegex.toString()}</div>
              <div className="text-[10px] text-slate-400 mt-1">Matches: PV-16501, FT-10402, LT-20415</div>
            </div>
            <div className="p-2.5 bg-white border border-slate-200 rounded">
              <div className="font-semibold text-slate-700 mb-1">Serial Number Pattern</div>
              <div className="text-slate-500">{defaultClassificationRules.serialNumberRegex.toString()}</div>
              <div className="text-[10px] text-slate-400 mt-1">Matches: F005327261, S991048210, 89104021</div>
            </div>
          </div>
        </div>
      )}

      {/* Success Notification Card */}
      {registeredSuccessItem && (
        <div className="p-4 bg-emerald-50 border border-emerald-200 rounded-lg text-emerald-950 text-xs flex flex-col sm:flex-row sm:items-center justify-between gap-3 animate-in fade-in duration-200">
          <div className="flex items-center gap-3">
            <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
            <div>
              <div className="font-bold text-sm text-emerald-900">
                Transaction Committed: {registeredSuccessItem.tagNumber}
              </div>
              <div className="text-emerald-700 mt-0.5">
                Serial <span className="font-mono font-medium">{registeredSuccessItem.serialNumber}</span> stored at{' '}
                <span className="font-semibold">{registeredSuccessItem.locationName}</span> by{' '}
                <span className="font-medium">{registeredSuccessItem.registeredByPersonnelName}</span>.
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <button
              type="button"
              onClick={() => onInspectItem(registeredSuccessItem.id)}
              className="px-3 py-1 bg-emerald-100 hover:bg-emerald-200 text-emerald-900 font-semibold rounded transition-colors"
            >
              Inspect Ledger
            </button>
            <button
              type="button"
              onClick={() => setRegisteredSuccessItem(null)}
              className="px-3 py-1 bg-white hover:bg-emerald-50 border border-emerald-300 text-emerald-800 rounded transition-colors"
            >
              Close
            </button>
          </div>
        </div>
      )}

      {/* Main Registration Form */}
      <form onSubmit={handleSubmit} className="bg-white rounded-lg border border-slate-200 shadow-2xs overflow-hidden">
        <div className="p-6 space-y-6">
          {/* Section 1: Dual Barcode Input Workstation */}
          <div>
            <div className="flex items-center justify-between mb-3">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-600 flex items-center gap-1.5">
                <Barcode className="w-4 h-4 text-[#004B87]" />
                Physical Label Barcode Captures (Mandatory)
              </label>

              <button
                type="button"
                onClick={handleSwapIdentifiers}
                disabled={!serialNumber && !tagNumber}
                className="flex items-center gap-1 text-xs text-[#004B87] hover:underline disabled:opacity-40 font-medium"
              >
                <ArrowLeftRight className="w-3.5 h-3.5" />
                <span>Swap Fields</span>
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Field 1: Serial Number */}
              <div
                className={`p-4 rounded-lg border transition-all ${
                  serialNumber
                    ? 'bg-blue-50/30 border-blue-200'
                    : 'bg-slate-50/50 border-slate-200'
                }`}
              >
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs font-semibold text-slate-700">
                    1. Serial Number
                  </span>
                  {serialNumber ? (
                    <span className="text-[10px] font-semibold text-blue-700 bg-blue-100 px-2 py-0.5 rounded">
                      Captured
                    </span>
                  ) : (
                    <span className="text-[10px] text-slate-600 font-mono">
                      Awaiting Scan
                    </span>
                  )}
                </div>

                <div className="relative">
                  <input
                    type="text"
                    required
                    value={serialNumber}
                    onChange={(e) => setSerialNumber(e.target.value)}
                    placeholder="Scan or enter Serial (e.g. F005327261)"
                    className="w-full bg-white border border-slate-300 text-slate-900 font-mono text-sm rounded px-3 py-2 focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87] outline-hidden placeholder:text-slate-400 placeholder:font-sans"
                  />
                  {serialNumber && (
                    <button
                      type="button"
                      onClick={() => setSerialNumber('')}
                      className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-slate-400 hover:text-slate-600"
                    >
                      Clear
                    </button>
                  )}
                </div>
                <div className="text-[11px] text-slate-600 mt-1.5">
                  Manufacturer physical chassis identifier.
                </div>
              </div>

              {/* Field 2: Tag Number */}
              <div
                className={`p-4 rounded-lg border transition-all ${
                  tagNumber
                    ? 'bg-blue-50/30 border-blue-200'
                    : 'bg-slate-50/50 border-slate-200'
                }`}
              >
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs font-semibold text-slate-700">
                    2. Tag Number
                  </span>
                  {tagNumber ? (
                    <span className="text-[10px] font-semibold text-[#004B87] bg-blue-100 px-2 py-0.5 rounded">
                      Captured
                    </span>
                  ) : (
                    <span className="text-[10px] text-slate-600 font-mono">
                      Awaiting Scan
                    </span>
                  )}
                </div>

                <div className="relative">
                  <input
                    type="text"
                    required
                    value={tagNumber}
                    onChange={(e) => setTagNumber(e.target.value)}
                    placeholder="Scan or enter Tag (e.g. PV-16501)"
                    className="w-full bg-white border border-slate-300 text-slate-900 font-mono text-sm rounded px-3 py-2 focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87] outline-hidden placeholder:text-slate-400 placeholder:font-sans"
                  />
                  {tagNumber && (
                    <button
                      type="button"
                      onClick={() => setTagNumber('')}
                      className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-slate-400 hover:text-slate-600"
                    >
                      Clear
                    </button>
                  )}
                </div>
                <div className="text-[11px] text-slate-600 mt-1.5">
                  Plant loop & instrumentation tag identifier.
                </div>
              </div>
            </div>
          </div>

          {/* Section 2: Relational Assignments (Location & Personnel) */}
          <div className="pt-4 border-t border-slate-100">
            <div className="text-xs font-bold uppercase tracking-wider text-slate-600 mb-3 flex items-center gap-1.5">
              <Building2 className="w-4 h-4 text-[#004B87]" />
              Custody & Storage Assignment (Mandatory)
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Field 3: Store Location */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                  3. Store Location <span className="text-red-600">*</span>
                </label>
                <select
                  required
                  value={storeLocationId}
                  onChange={(e) => setStoreLocationId(e.target.value)}
                  disabled={loadingDropdowns}
                  className="w-full bg-white border border-slate-300 text-slate-900 text-xs rounded px-3 py-2.5 focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87] outline-hidden disabled:bg-slate-50"
                >
                  <option value="">-- Select Active Store Location --</option>
                  {locations.map((loc) => (
                    <option key={loc.id} value={loc.id}>
                      {loc.locationCode} - {loc.locationName}
                    </option>
                  ))}
                </select>
                <div className="text-[11px] text-slate-600 mt-1">
                  Physical bay or rack where unit is stationed.
                </div>
              </div>

              {/* Field 4: Scan By Personnel */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                  4. Scan By (Responsible Personnel) <span className="text-red-600">*</span>
                </label>
                <select
                  required
                  value={scanByPersonnelId}
                  onChange={(e) => setScanByPersonnelId(e.target.value)}
                  disabled={loadingDropdowns}
                  className="w-full bg-white border border-slate-300 text-slate-900 text-xs rounded px-3 py-2.5 focus:border-[#004B87] focus:ring-1 focus:ring-[#004B87] outline-hidden disabled:bg-slate-50"
                >
                  <option value="">-- Select Responsible Personnel --</option>
                  {personnel.map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.name} [{person.personnelCode}]
                    </option>
                  ))}
                </select>
                <div className="text-[11px] text-slate-600 mt-1">
                  Responsible technician conducting verification.
                </div>
              </div>
            </div>
          </div>

          {/* Submission Error Alert */}
          {submitError && (
            <div className="p-3 bg-red-50 border border-red-200 rounded text-red-900 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-red-600 shrink-0" />
              <span>{submitError}</span>
            </div>
          )}
        </div>

        {/* Form Submission Action Strip */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-50 border-t border-slate-200">
          <button
            type="button"
            onClick={handleClearForm}
            className="flex items-center gap-1.5 px-3 py-2 text-xs font-medium text-slate-600 hover:text-slate-900 rounded transition-colors"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            <span>Reset Fields</span>
          </button>

          <div className="flex items-center gap-3">
            {!isFormComplete && (
              <span className="text-[11px] text-slate-600 hidden sm:inline">
                All 4 fields required to enable database commit
              </span>
            )}

            <button
              type="submit"
              disabled={!isFormComplete || isSubmitting}
              className="flex items-center gap-2 px-5 py-2.5 text-xs font-bold text-white bg-[#004B87] hover:bg-[#003866] disabled:opacity-40 disabled:pointer-events-none rounded transition-colors shadow-2xs"
            >
              {isSubmitting ? (
                <>
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  <span>Committing Transaction...</span>
                </>
              ) : (
                <>
                  <CheckCircle2 className="w-4 h-4" />
                  <span>Commit Inventory Registration</span>
                </>
              )}
            </button>
          </div>
        </div>
      </form>

      {/* Camera Barcode Scanner Modal */}
      {isCameraModalOpen && (
        <Suspense fallback={null}>
          <CameraScannerModal
            onClose={() => setIsCameraModalOpen(false)}
            initialSerial={serialNumber}
            initialTag={tagNumber}
            onConfirmScans={(scannedSerial, scannedTag) => {
              if (scannedSerial) setSerialNumber(scannedSerial);
              if (scannedTag) setTagNumber(scannedTag);
              notify('Camera barcodes applied to registration workstation.', 'success');
            }}
          />
        </Suspense>
      )}

      {/* Duplicate Conflict Response Modal */}
      <DuplicateConflictModal
        conflict={duplicateConflict}
        onClose={() => setDuplicateConflict(null)}
        onViewExisting={(itemId) => {
          setDuplicateConflict(null);
          onInspectItem(itemId);
        }}
      />
    </div>
  );
};
