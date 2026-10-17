import React, { useEffect, useRef, useState } from 'react';
import {
  AlertCircle,
  ArrowLeftRight,
  ArrowRight,
  Barcode,
  Camera,
  Check,
  CheckCircle2,
  LoaderCircle,
  MapPin,
  PackageCheck,
  RefreshCw,
  Radio,
  ScanLine,
  ShieldCheck,
  UserRound,
} from 'lucide-react';
import { ApiRequestError, inventoryApi } from '../services/api';
import { CameraScannerModal } from '../components/CameraScannerModal';
import {
  classifyBarcode,
  HandheldScannerListener,
} from '../services/scanner';
import {
  InventoryItemIdentification,
  IssueInventoryResponse,
  Personnel,
} from '../types/inventory';

interface Props {
  onViewInventory: () => void;
}

export const IssueForPackingView: React.FC<Props> = ({ onViewInventory }) => {
  const [serialNumber, setSerialNumber] = useState('');
  const [tagNumber, setTagNumber] = useState('');
  const [personnelId, setPersonnelId] = useState('');
  const [personnel, setPersonnel] = useState<Personnel[]>([]);
  const [personnelLoading, setPersonnelLoading] = useState(true);
  const [personnelError, setPersonnelError] = useState<string | null>(null);
  const [personnelReload, setPersonnelReload] = useState(0);
  const [identifiedItem, setIdentifiedItem] = useState<InventoryItemIdentification | null>(null);
  const [identifyError, setIdentifyError] = useState<string | null>(null);
  const [issueError, setIssueError] = useState<string | null>(null);
  const [issuedResult, setIssuedResult] = useState<IssueInventoryResponse | null>(null);
  const [isIdentifying, setIsIdentifying] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [isCameraModalOpen, setIsCameraModalOpen] = useState(false);
  const [scannerNotification, setScannerNotification] = useState<{
    text: string;
    type: 'success' | 'warning' | 'info';
  } | null>(null);
  const scannerHandlerRef = useRef<(value: string) => void>(() => undefined);
  const submissionLockRef = useRef(false);
  const identificationRequestRef = useRef(0);

  useEffect(() => {
    let isMounted = true;
    setPersonnelLoading(true);
    setPersonnelError(null);

    inventoryApi.getPersonnel(true)
      .then((activePersonnel) => {
        if (isMounted) setPersonnel(activePersonnel.filter((person) => person.isActive));
      })
      .catch((error: unknown) => {
        if (isMounted) {
          setPersonnelError(
            error instanceof Error
              ? error.message
              : 'Unable to load active personnel. Check the connection and try again.'
          );
          setPersonnel([]);
        }
      })
      .finally(() => {
        if (isMounted) setPersonnelLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [personnelReload]);

  const notifyScanner = (text: string, type: 'success' | 'warning' | 'info' = 'info') => {
    setScannerNotification({ text, type });
  };

  const clearIdentification = () => {
    identificationRequestRef.current += 1;
    setIdentifiedItem(null);
    setConfirmed(false);
    setIdentifyError(null);
    setIssueError(null);
  };

  const processIncomingScan = (rawText: string) => {
    if (submissionLockRef.current) return;
    const text = rawText.trim();
    if (!text) return;

    const classification = classifyBarcode(text);
    const currentSerial = serialNumber.trim();
    const currentTag = tagNumber.trim();

    if (!currentSerial && !currentTag) {
      if (classification.type === 'TAG_NUMBER') {
        setTagNumber(text);
        notifyScanner(`Captured Tag Number: "${text}". Waiting for Serial Number.`, 'info');
      } else {
        setSerialNumber(text);
        notifyScanner(
          classification.type === 'UNKNOWN'
            ? `Captured identifier "${text}" as Serial Number (format unverified).`
            : `Captured Serial Number: "${text}". Waiting for Tag Number.`,
          classification.type === 'UNKNOWN' ? 'warning' : 'info'
        );
      }
      clearIdentification();
      setIssuedResult(null);
      return;
    }

    if (currentSerial && !currentTag) {
      if (text.toLowerCase() === currentSerial.toLowerCase()) {
        notifyScanner(`Duplicate scan ignored: "${text}" is already in Serial Number.`, 'warning');
      } else if (classification.type === 'SERIAL_NUMBER') {
        notifyScanner(`Serial Number is already filled. Clear or swap it before replacing it with "${text}".`, 'warning');
      } else {
        setTagNumber(text);
        clearIdentification();
        setIssuedResult(null);
        notifyScanner(`Captured Tag Number: "${text}". Both identifiers are ready.`, 'success');
      }
      return;
    }

    if (!currentSerial && currentTag) {
      if (text.toLowerCase() === currentTag.toLowerCase()) {
        notifyScanner(`Duplicate scan ignored: "${text}" is already in Tag Number.`, 'warning');
      } else if (classification.type === 'TAG_NUMBER') {
        notifyScanner(`Tag Number is already filled. Clear or swap it before replacing it with "${text}".`, 'warning');
      } else {
        setSerialNumber(text);
        clearIdentification();
        setIssuedResult(null);
        notifyScanner(`Captured Serial Number: "${text}". Both identifiers are ready.`, 'success');
      }
      return;
    }

    if (text.toLowerCase() === currentSerial.toLowerCase() ||
        text.toLowerCase() === currentTag.toLowerCase()) {
      notifyScanner(`Duplicate scan ignored: "${text}" is already captured.`, 'warning');
      return;
    }

    notifyScanner(
      `Both fields are already populated (Serial: ${currentSerial}, Tag: ${currentTag}). Clear a field before rescanning.`,
      'warning'
    );
  };

  scannerHandlerRef.current = processIncomingScan;

  useEffect(() => {
    const listener = new HandheldScannerListener((value) => scannerHandlerRef.current(value));
    listener.start();
    return () => listener.stop();
  }, []);

  useEffect(() => {
    if (!scannerNotification) return;
    const timer = window.setTimeout(() => setScannerNotification(null), 4500);
    return () => window.clearTimeout(timer);
  }, [scannerNotification]);

  const handleIdentify = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const serial = serialNumber.trim();
    const tag = tagNumber.trim();

    clearIdentification();
    const requestId = identificationRequestRef.current;
    setIssuedResult(null);
    if (!serial || !tag) {
      setIdentifyError('Enter both the serial number and tag number to identify an asset.');
      return;
    }

    setIsIdentifying(true);
    try {
      const item = await inventoryApi.identifyInventory(serial, tag);
      if (requestId !== identificationRequestRef.current) return;
      setIdentifiedItem(item);
      if (item.isIssued) {
        setIdentifyError('This asset has already been issued for packing and cannot be issued again.');
      }
    } catch (error: unknown) {
      if (requestId !== identificationRequestRef.current) return;
      if (error instanceof ApiRequestError && error.status === 404) {
        setIdentifyError('No registered asset matches both identifiers. Check the labels and try again.');
      } else if (error instanceof ApiRequestError && error.status === 400) {
        setIdentifyError(error.message);
      } else {
        setIdentifyError(
          error instanceof Error
            ? error.message
            : 'Asset identification failed. Check the connection and try again.'
        );
      }
    } finally {
      setIsIdentifying(false);
    }
  };

  const handleIssue = async () => {
    if (!identifiedItem || identifiedItem.isIssued || !personnelId || !confirmed || submissionLockRef.current) return;

    submissionLockRef.current = true;
    setIsSubmitting(true);
    setIssueError(null);
    try {
      const result = await inventoryApi.issueInventory({
        serialNumber: identifiedItem.serialNumber,
        tagNumber: identifiedItem.tagNumber,
        performedByPersonnelId: personnelId,
      });
      setIssuedResult(result);
      setSerialNumber('');
      setTagNumber('');
      setPersonnelId('');
      setIdentifiedItem(null);
      setConfirmed(false);
    } catch (error: unknown) {
      if (error instanceof ApiRequestError && error.status === 409) {
        setIdentifiedItem(null);
        setConfirmed(false);
        setIssueError('This asset was already issued by another request. Identify it again to verify its current status.');
      } else if (error instanceof ApiRequestError && error.status === 400) {
        setIssueError(error.message);
      } else if (error instanceof ApiRequestError && error.status === 404) {
        setIdentifiedItem(null);
        setConfirmed(false);
        setIssueError('The asset could not be found with that identifier pair. Identify it again before retrying.');
      } else {
        setIssueError(
          error instanceof Error
            ? error.message
            : 'The issue request failed. Check the connection and try again.'
        );
      }
    } finally {
      submissionLockRef.current = false;
      setIsSubmitting(false);
    }
  };

  const handleSwapIdentifiers = () => {
    setSerialNumber(tagNumber);
    setTagNumber(serialNumber);
    clearIdentification();
    setIssuedResult(null);
    notifyScanner('Swapped Serial Number and Tag Number assignments.', 'info');
  };

  const handleCameraScans = (scannedSerial: string, scannedTag: string) => {
    if (scannedSerial) setSerialNumber(scannedSerial);
    if (scannedTag) setTagNumber(scannedTag);
    clearIdentification();
    setIssuedResult(null);
    notifyScanner('Camera barcode values applied. Identify the asset to verify the pair.', 'success');
  };

  const selectedPersonnel = personnel.find((person) => person.id === personnelId);
  const canIssue = Boolean(
    identifiedItem &&
    !identifiedItem.isIssued &&
    selectedPersonnel &&
    confirmed &&
    !personnelLoading &&
    !personnelError
  );

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 border-b border-slate-200 pb-5 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <div className="mb-2 flex items-center gap-2 text-[10px] font-mono font-semibold uppercase tracking-[0.2em] text-[#004B87]">
            <span className="h-px w-7 bg-[#004B87]" />
            Warehouse transaction / 02
          </div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-950">Issue for Packing</h1>
          <p className="mt-1 max-w-2xl text-sm text-slate-500">
            Verify both asset identifiers, record the responsible operator, then release the asset from available stock.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2 self-start sm:self-auto">
          <button
            type="button"
            onClick={() => setIsCameraModalOpen(true)}
            disabled={isIdentifying || isSubmitting}
            className="inline-flex items-center gap-2 rounded bg-[#004B87] px-3.5 py-2.5 text-xs font-semibold text-white shadow-2xs transition hover:bg-[#003866] disabled:opacity-50"
          >
            <Camera className="h-4 w-4" />
            Dual Barcode Camera Scanner
          </button>
          <div className="flex items-center gap-2 rounded border border-amber-200 bg-amber-50 px-3 py-2.5 text-[11px] font-semibold uppercase tracking-wide text-amber-800">
            <ShieldCheck className="h-4 w-4" />
            Confirmation required
          </div>
        </div>
      </header>

      {issuedResult && (
        <section role="status" className="flex flex-col gap-4 rounded-lg border border-emerald-200 bg-emerald-50 p-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-start gap-3">
            <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-emerald-600 text-white">
              <Check className="h-4 w-4" />
            </span>
            <div>
              <h2 className="text-sm font-bold text-emerald-950">Issue recorded successfully</h2>
              <p className="mt-1 text-sm text-emerald-900">
                Tag <span className="font-mono font-bold">{issuedResult.item.tagNumber}</span> was issued for packing.
                The event was recorded at {new Date(issuedResult.movement.movedAt).toLocaleString()}.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onViewInventory}
            className="shrink-0 rounded bg-emerald-800 px-4 py-2.5 text-xs font-semibold text-white transition hover:bg-emerald-900"
          >
            View available inventory
          </button>
        </section>
      )}

      <div className="flex flex-col gap-3 rounded-lg bg-slate-900 px-4 py-3 text-xs text-white sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-2.5">
          <Radio className="h-4 w-4 shrink-0 text-emerald-400" />
          <span className="font-semibold text-slate-100">Manual scanning input ready</span>
          <span className="hidden text-slate-400 md:inline">
            Type values or use a USB / Bluetooth keyboard-wedge scanner.
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-2 text-[11px]">
          <span className="text-slate-400">Simulate scan:</span>
          <button
            type="button"
            onClick={() => processIncomingScan('F005327261')}
            className="rounded bg-slate-800 px-2 py-1 font-mono text-slate-200 transition hover:bg-slate-700"
          >
            F005327261
          </button>
          <button
            type="button"
            onClick={() => processIncomingScan('PV-16501')}
            className="rounded bg-slate-800 px-2 py-1 font-mono text-slate-200 transition hover:bg-slate-700"
          >
            PV-16501
          </button>
        </div>
      </div>

      {scannerNotification && (
        <div
          role="status"
          className={`flex items-center justify-between gap-3 rounded border px-4 py-2.5 text-xs font-medium ${
            scannerNotification.type === 'success'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-900'
              : scannerNotification.type === 'warning'
                ? 'border-amber-200 bg-amber-50 text-amber-900'
                : 'border-blue-200 bg-blue-50 text-blue-900'
          }`}
        >
          <span className="flex items-center gap-2"><Barcode className="h-4 w-4 shrink-0" />{scannerNotification.text}</span>
          <button type="button" onClick={() => setScannerNotification(null)} className="shrink-0 opacity-70 hover:opacity-100">
            Dismiss
          </button>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1.05fr)_minmax(320px,0.95fr)]">
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-2xs">
          <div className="flex items-center gap-3 border-b border-slate-200 bg-slate-50/80 px-5 py-4">
            <span className="flex h-8 w-8 items-center justify-center rounded bg-[#0B1E36] text-white">
              <ScanLine className="h-4 w-4" />
            </span>
            <div>
              <h2 className="text-sm font-bold text-slate-900">Identify registered asset</h2>
              <p className="text-[11px] text-slate-500">Both values must match one inventory record.</p>
            </div>
            <span className="ml-auto font-mono text-[10px] text-slate-400">STEP 01</span>
          </div>

          <form onSubmit={handleIdentify} className="space-y-5 p-5">
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block">
                <span className="mb-1.5 block text-xs font-semibold text-slate-700">Serial Number</span>
                <input
                  required
                  maxLength={100}
                  autoComplete="off"
                  disabled={isIdentifying || isSubmitting}
                  value={serialNumber}
                  onChange={(event) => {
                    setSerialNumber(event.target.value);
                    clearIdentification();
                    setIssuedResult(null);
                  }}
                  placeholder="Scan or enter serial"
                  className="w-full rounded border border-slate-300 bg-slate-50 px-3 py-2.5 font-mono text-sm text-slate-900 outline-none transition placeholder:font-sans placeholder:text-slate-400 focus:border-[#004B87] focus:bg-white focus:ring-2 focus:ring-[#004B87]/10"
                />
              </label>
              <label className="block">
                <span className="mb-1.5 block text-xs font-semibold text-slate-700">Tag Number</span>
                <input
                  required
                  maxLength={100}
                  autoComplete="off"
                  disabled={isIdentifying || isSubmitting}
                  value={tagNumber}
                  onChange={(event) => {
                    setTagNumber(event.target.value);
                    clearIdentification();
                    setIssuedResult(null);
                  }}
                  placeholder="Scan or enter tag"
                  className="w-full rounded border border-slate-300 bg-slate-50 px-3 py-2.5 font-mono text-sm text-slate-900 outline-none transition placeholder:font-sans placeholder:text-slate-400 focus:border-[#004B87] focus:bg-white focus:ring-2 focus:ring-[#004B87]/10"
                />
              </label>
            </div>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-[11px] text-slate-500">
                Enter manually or scan the physical labels in either order. Repeated scans will not overwrite captured values.
              </p>
              <button
                type="button"
                onClick={handleSwapIdentifiers}
                disabled={!serialNumber && !tagNumber || isIdentifying || isSubmitting}
                className="inline-flex items-center gap-1.5 text-xs font-medium text-[#004B87] hover:underline disabled:opacity-40"
              >
                <ArrowLeftRight className="h-3.5 w-3.5" />
                Swap Fields
              </button>
            </div>

            {identifyError && (
              <div role="alert" className="flex items-start gap-2 rounded border border-rose-200 bg-rose-50 p-3 text-xs text-rose-800">
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>{identifyError}</span>
              </div>
            )}

            <button
              type="submit"
              disabled={isIdentifying || isSubmitting || !serialNumber.trim() || !tagNumber.trim()}
              className="flex w-full items-center justify-center gap-2 rounded bg-[#004B87] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[#003866] disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isIdentifying ? <LoaderCircle className="h-4 w-4 animate-spin" /> : <ScanLine className="h-4 w-4" />}
              {isIdentifying ? 'Checking identifier pair…' : 'Identify asset'}
            </button>
          </form>
        </section>

        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-2xs">
          <div className="flex items-center gap-3 border-b border-slate-200 bg-slate-50/80 px-5 py-4">
            <span className="flex h-8 w-8 items-center justify-center rounded bg-[#0B1E36] text-white">
              <UserRound className="h-4 w-4" />
            </span>
            <div>
              <h2 className="text-sm font-bold text-slate-900">Responsible operator</h2>
              <p className="text-[11px] text-slate-500">Select the active staff member performing this issue.</p>
            </div>
            <span className="ml-auto font-mono text-[10px] text-slate-400">STEP 02</span>
          </div>

          <div className="space-y-4 p-5">
            <label className="block">
              <span className="mb-1.5 block text-xs font-semibold text-slate-700">Personnel</span>
              <select
                value={personnelId}
                disabled={personnelLoading || isSubmitting || Boolean(personnelError) || personnel.length === 0}
                onChange={(event) => {
                  setPersonnelId(event.target.value);
                  setConfirmed(false);
                  setIssueError(null);
                }}
                className="w-full rounded border border-slate-300 bg-slate-50 px-3 py-2.5 text-sm text-slate-900 outline-none transition focus:border-[#004B87] focus:bg-white disabled:cursor-not-allowed disabled:opacity-60"
              >
                <option value="">
                  {personnelLoading ? 'Loading active personnel…' : 'Select responsible personnel'}
                </option>
                {personnel.map((person) => (
                  <option key={person.id} value={person.id}>
                    {person.personnelCode} — {person.name}
                  </option>
                ))}
              </select>
            </label>

            {personnelError ? (
              <div role="alert" className="rounded border border-rose-200 bg-rose-50 p-3 text-xs text-rose-800">
                <p>{personnelError}</p>
                <button
                  type="button"
                  onClick={() => setPersonnelReload((attempt) => attempt + 1)}
                  className="mt-2 inline-flex items-center gap-1.5 font-semibold text-rose-900 underline"
                >
                  <RefreshCw className="h-3 w-3" /> Retry personnel load
                </button>
              </div>
            ) : !personnelLoading && personnel.length === 0 ? (
              <p className="rounded border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
                No active personnel are available. Ask an administrator to review the personnel records.
              </p>
            ) : null}

            {identifiedItem && (
              <div className={`rounded border p-4 ${identifiedItem.isIssued ? 'border-amber-200 bg-amber-50' : 'border-emerald-200 bg-emerald-50/70'}`}>
                <div className="mb-3 flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <CheckCircle2 className={`h-4 w-4 ${identifiedItem.isIssued ? 'text-amber-700' : 'text-emerald-700'}`} />
                    <span className={`text-xs font-bold uppercase tracking-wide ${identifiedItem.isIssued ? 'text-amber-900' : 'text-emerald-900'}`}>
                      {identifiedItem.isIssued ? 'Previously issued' : 'Verified asset'}
                    </span>
                  </div>
                  <span className="font-mono text-[10px] text-slate-400">RECORD MATCH</span>
                </div>
                <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-xs">
                  <div>
                    <dt className="text-slate-500">Serial Number</dt>
                    <dd className="mt-0.5 break-all font-mono font-semibold text-slate-900">{identifiedItem.serialNumber}</dd>
                  </div>
                  <div>
                    <dt className="text-slate-500">Tag Number</dt>
                    <dd className="mt-0.5 break-all font-mono font-semibold text-slate-900">{identifiedItem.tagNumber}</dd>
                  </div>
                  <div className="col-span-2 border-t border-slate-200/80 pt-2">
                    <dt className="flex items-center gap-1 text-slate-500"><MapPin className="h-3 w-3" /> Current store location</dt>
                    <dd className="mt-0.5 font-semibold text-slate-900">
                      {identifiedItem.locationName} <span className="font-mono text-slate-500">[{identifiedItem.locationCode}]</span>
                    </dd>
                  </div>
                </dl>
              </div>
            )}
          </div>
        </section>
      </div>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-2xs">
        <div className="flex items-center gap-3 border-b border-slate-200 px-5 py-4">
          <span className="flex h-8 w-8 items-center justify-center rounded bg-amber-100 text-amber-800">
            <PackageCheck className="h-4 w-4" />
          </span>
          <div>
            <h2 className="text-sm font-bold text-slate-900">Review and confirm issue</h2>
            <p className="text-[11px] text-slate-500">This records an issue event; the inventory record and its audit history are retained.</p>
          </div>
          <span className="ml-auto font-mono text-[10px] text-slate-400">STEP 03</span>
        </div>

        <div className="flex flex-col gap-5 p-5 lg:flex-row lg:items-center lg:justify-between">
          <div className="max-w-2xl">
            {issueError && (
              <div role="alert" className="mb-3 flex items-start gap-2 rounded border border-rose-200 bg-rose-50 p-3 text-xs text-rose-800">
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>{issueError}</span>
              </div>
            )}
            {identifiedItem && !identifiedItem.isIssued && selectedPersonnel ? (
              <label className="flex cursor-pointer items-start gap-3 text-sm text-slate-700">
                <input
                  type="checkbox"
                  checked={confirmed}
                  onChange={(event) => setConfirmed(event.target.checked)}
                  className="mt-0.5 h-4 w-4 accent-[#004B87]"
                />
                <span>
                  I verified serial <strong className="font-mono">{identifiedItem.serialNumber}</strong> and tag{' '}
                  <strong className="font-mono">{identifiedItem.tagNumber}</strong>. I confirm that{' '}
                  <strong>{selectedPersonnel.name}</strong> is issuing this asset for packing.
                </span>
              </label>
            ) : (
              <p className="text-sm text-slate-500">
                Identify an available asset and select active personnel to enable the confirmation step.
              </p>
            )}
          </div>

          <button
            type="button"
            onClick={handleIssue}
            disabled={!canIssue || isSubmitting}
            className="inline-flex shrink-0 items-center justify-center gap-2 rounded bg-amber-700 px-5 py-3 text-sm font-bold text-white transition hover:bg-amber-800 disabled:cursor-not-allowed disabled:bg-slate-300"
          >
            {isSubmitting ? <LoaderCircle className="h-4 w-4 animate-spin" /> : <ArrowRight className="h-4 w-4" />}
            {isSubmitting ? 'Recording issue…' : 'Issue for Packing'}
          </button>
        </div>
      </section>

      <CameraScannerModal
        isOpen={isCameraModalOpen}
        onClose={() => setIsCameraModalOpen(false)}
        initialSerial={serialNumber}
        initialTag={tagNumber}
        onConfirmScans={handleCameraScans}
      />
    </div>
  );
};
