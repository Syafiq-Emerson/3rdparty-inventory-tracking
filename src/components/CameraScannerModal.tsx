import React, { useEffect, useRef, useState } from 'react';
import { Camera, X, Check, RefreshCw, Zap, AlertCircle, Scan, ArrowRight } from 'lucide-react';
import { BrowserMultiFormatReader } from '@zxing/library';
import { classifyBarcode } from '../services/scanner';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onConfirmScans: (serial: string, tag: string) => void;
  initialSerial?: string;
  initialTag?: string;
}

export const CameraScannerModal: React.FC<Props> = ({
  isOpen,
  onClose,
  onConfirmScans,
  initialSerial = '',
  initialTag = '',
}) => {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const codeReaderRef = useRef<BrowserMultiFormatReader | null>(null);
  const streamRef = useRef<MediaStream | null>(null);

  const [capturedSerial, setCapturedSerial] = useState<string>(initialSerial);
  const [capturedTag, setCapturedTag] = useState<string>(initialTag);
  const [cameraError, setCameraError] = useState<string | null>(null);
  const [isInitializing, setIsInitializing] = useState<boolean>(true);
  const [torchActive, setTorchActive] = useState<boolean>(false);
  const [hasTorch, setHasTorch] = useState<boolean>(false);
  const [facingMode, setFacingMode] = useState<'environment' | 'user'>('environment');
  const [lastScannedFeedback, setLastScannedFeedback] = useState<string | null>(null);

  // Manual fallback inputs
  const [showManualFallback, setShowManualFallback] = useState<boolean>(false);
  const [manualSerialInput, setManualSerialInput] = useState<string>(initialSerial);
  const [manualTagInput, setManualTagInput] = useState<string>(initialTag);

  useEffect(() => {
    if (isOpen) {
      setCapturedSerial(initialSerial);
      setCapturedTag(initialTag);
      setManualSerialInput(initialSerial);
      setManualTagInput(initialTag);
      startCamera();
    } else {
      stopCamera();
    }

    return () => {
      stopCamera();
    };
  }, [isOpen, facingMode]);

  const startCamera = async () => {
    setIsInitializing(true);
    setCameraError(null);

    try {
      // Release existing stream if any
      stopCamera();

      // Check mediaDevices support
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        throw new Error('Camera access is not supported by this browser environment or requires HTTPS.');
      }

      const constraints: MediaStreamConstraints = {
        video: {
          facingMode: { ideal: facingMode },
          width: { ideal: 1280 },
          height: { ideal: 720 },
        },
        audio: false,
      };

      const stream = await navigator.mediaDevices.getUserMedia(constraints);
      streamRef.current = stream;

      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();

        // Check torch capability
        const track = stream.getVideoTracks()[0];
        const capabilities = (track.getCapabilities ? track.getCapabilities() : {}) as any;
        setHasTorch(Boolean(capabilities?.torch));

        // Start ZXing Barcode Scanning loop
        codeReaderRef.current = new BrowserMultiFormatReader();
        (codeReaderRef.current as any).decodeFromVideoElementContinuously(
          videoRef.current,
          (result: any) => {
            if (result) {
              handleBarcodeDetected(result.getText());
            }
          }
        );
      }

      setIsInitializing(false);
    } catch (err: any) {
      console.warn('Camera initiation failed:', err);
      setCameraError(
        err.name === 'NotAllowedError'
          ? 'Camera permission denied. Please allow camera access in browser permissions or use manual entry.'
          : err.message || 'Unable to access video device.'
      );
      setIsInitializing(false);
      setShowManualFallback(true);
    }
  };

  const stopCamera = () => {
    if (codeReaderRef.current) {
      try {
        codeReaderRef.current.reset();
      } catch (e) {
        // ignore reset errors
      }
      codeReaderRef.current = null;
    }

    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    }
  };

  const toggleTorch = async () => {
    if (!streamRef.current) return;
    const track = streamRef.current.getVideoTracks()[0];
    if (track && hasTorch) {
      try {
        const nextState = !torchActive;
        await (track as any).applyConstraints({
          advanced: [{ torch: nextState }],
        });
        setTorchActive(nextState);
      } catch (e) {
        console.warn('Torch constraint error:', e);
      }
    }
  };

  const toggleCamera = () => {
    setFacingMode((prev) => (prev === 'environment' ? 'user' : 'environment'));
  };

  const handleBarcodeDetected = (rawText: string) => {
    const text = rawText.trim();
    if (!text) return;

    // Classify identifier
    const classification = classifyBarcode(text);

    // Provide visual toast feedback
    setLastScannedFeedback(`Scanned "${text}" (${classification.type === 'SERIAL_NUMBER' ? 'Serial Number' : 'Tag Number'})`);
    setTimeout(() => setLastScannedFeedback(null), 3000);

    if (classification.type === 'SERIAL_NUMBER') {
      setCapturedSerial((prev) => {
        if (!prev) return text;
        return prev;
      });
      setManualSerialInput((prev) => (!prev ? text : prev));
    } else if (classification.type === 'TAG_NUMBER') {
      setCapturedTag((prev) => {
        if (!prev) return text;
        return prev;
      });
      setManualTagInput((prev) => (!prev ? text : prev));
    } else {
      // Ambiguous: Assign to whichever field is currently empty
      if (!capturedSerial) {
        setCapturedSerial(text);
        setManualSerialInput(text);
      } else if (!capturedTag) {
        setCapturedTag(text);
        setManualTagInput(text);
      }
    }
  };

  const handleApply = () => {
    const finalSerial = showManualFallback ? manualSerialInput.trim() : capturedSerial.trim();
    const finalTag = showManualFallback ? manualTagInput.trim() : capturedTag.trim();

    onConfirmScans(finalSerial, finalTag);
    onClose();
  };

  if (!isOpen) return null;

  const bothCaptured = Boolean(capturedSerial && capturedTag);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-950/80 backdrop-blur-xs">
      <div className="w-full max-w-lg bg-slate-900 border border-slate-800 rounded-lg shadow-2xl overflow-hidden flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-3.5 bg-slate-900 text-white border-b border-slate-800 shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="p-1.5 bg-blue-600 rounded">
              <Camera className="w-4 h-4 text-white" />
            </div>
            <div>
              <h3 className="text-sm font-semibold tracking-tight text-white">
                Dual Barcode Camera Scanner
              </h3>
              <p className="text-xs text-slate-400">
                Align equipment label with linear barcodes in frame
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1 text-slate-400 hover:text-white rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Dual Barcode Slots HUD */}
        <div className="px-5 py-3 bg-slate-950 border-b border-slate-800 grid grid-cols-2 gap-3 shrink-0">
          {/* Serial Number Slot */}
          <div
            className={`p-2.5 rounded border transition-colors ${
              capturedSerial
                ? 'bg-blue-950/40 border-blue-600/60 text-blue-200'
                : 'bg-slate-900 border-slate-800 text-slate-400'
            }`}
          >
            <div className="flex items-center justify-between text-xs font-semibold mb-1">
              <span>1. Serial Number</span>
              {capturedSerial ? (
                <Check className="w-3.5 h-3.5 text-blue-400" />
              ) : (
                <span className="text-[10px] uppercase font-mono text-slate-500">Waiting</span>
              )}
            </div>
            <div className="font-mono text-xs font-bold truncate">
              {capturedSerial || '---'}
            </div>
          </div>

          {/* Tag Number Slot */}
          <div
            className={`p-2.5 rounded border transition-colors ${
              capturedTag
                ? 'bg-emerald-950/40 border-emerald-600/60 text-emerald-200'
                : 'bg-slate-900 border-slate-800 text-slate-400'
            }`}
          >
            <div className="flex items-center justify-between text-xs font-semibold mb-1">
              <span>2. Tag Number</span>
              {capturedTag ? (
                <Check className="w-3.5 h-3.5 text-emerald-400" />
              ) : (
                <span className="text-[10px] uppercase font-mono text-slate-500">Waiting</span>
              )}
            </div>
            <div className="font-mono text-xs font-bold truncate">
              {capturedTag || '---'}
            </div>
          </div>
        </div>

        {/* Viewfinder Viewport */}
        <div className="relative flex-1 bg-black min-h-[280px] max-h-[380px] flex items-center justify-center overflow-hidden">
          {cameraError ? (
            <div className="p-6 text-center text-slate-300 max-w-sm space-y-3">
              <AlertCircle className="w-8 h-8 text-amber-500 mx-auto" />
              <div className="text-sm font-medium text-white">Camera Unavailable</div>
              <p className="text-xs text-slate-400 leading-relaxed">{cameraError}</p>
              <button
                onClick={() => setShowManualFallback(true)}
                className="px-3.5 py-1.5 text-xs font-medium text-white bg-blue-700 rounded hover:bg-blue-800"
              >
                Use Manual Input
              </button>
            </div>
          ) : (
            <>
              <video
                ref={videoRef}
                playsInline
                muted
                className="w-full h-full object-cover"
              />

              {/* Viewfinder Reticle Overlay */}
              <div className="absolute inset-0 pointer-events-none flex flex-col items-center justify-center p-6">
                <div className="w-64 sm:w-80 h-32 border-2 border-dashed border-blue-400/70 rounded-lg relative flex items-center justify-center shadow-[0_0_0_9999px_rgba(0,0,0,0.45)]">
                  <div className="w-full h-0.5 bg-red-500/80 animate-pulse shadow-[0_0_8px_#ef4444]" />
                  <div className="absolute top-2 left-2 text-[10px] uppercase font-mono text-blue-300 bg-slate-900/80 px-1.5 py-0.5 rounded">
                    Industrial Laser Reticle
                  </div>
                </div>
                <div className="mt-4 text-xs font-medium text-slate-300 bg-slate-900/90 px-3 py-1 rounded shadow-sm">
                  {bothCaptured
                    ? '✓ Both barcodes captured! Tap Apply to populate form.'
                    : capturedSerial || capturedTag
                    ? '1 of 2 captured. Align the second barcode...'
                    : 'Align Serial Number or Tag Number barcode within the box'}
                </div>
              </div>

              {/* Toast Feedback for Scanned Value */}
              {lastScannedFeedback && (
                <div className="absolute bottom-3 left-4 right-4 bg-blue-900/95 text-white px-3 py-1.5 rounded text-xs font-medium text-center border border-blue-500/40 animate-in fade-in duration-150">
                  {lastScannedFeedback}
                </div>
              )}
            </>
          )}

          {/* Camera Controls Overlay */}
          <div className="absolute top-3 right-3 flex items-center gap-2">
            {hasTorch && (
              <button
                onClick={toggleTorch}
                className={`p-2 rounded-full transition-colors ${
                  torchActive ? 'bg-amber-400 text-slate-950' : 'bg-slate-900/80 text-white hover:bg-slate-800'
                }`}
                title="Toggle Torch/Flashlight"
              >
                <Zap className="w-4 h-4" />
              </button>
            )}
            <button
              onClick={toggleCamera}
              className="p-2 bg-slate-900/80 text-white rounded-full hover:bg-slate-800 transition-colors"
              title="Switch Camera"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Manual Fallback Collapsible */}
        {showManualFallback && (
          <div className="p-4 bg-slate-900 border-t border-slate-800 space-y-3">
            <div className="text-xs font-semibold uppercase text-slate-400 tracking-wider">
              Manual Override / Keyboard Entry
            </div>
            <div className="grid grid-cols-2 gap-3 text-xs">
              <div>
                <label className="block text-slate-400 mb-1">Serial Number</label>
                <input
                  type="text"
                  value={manualSerialInput}
                  onChange={(e) => {
                    setManualSerialInput(e.target.value);
                    setCapturedSerial(e.target.value);
                  }}
                  placeholder="e.g. F005327261"
                  className="w-full bg-slate-950 border border-slate-700 text-white rounded px-2.5 py-1.5 font-mono focus:border-blue-500 outline-hidden"
                />
              </div>
              <div>
                <label className="block text-slate-400 mb-1">Tag Number</label>
                <input
                  type="text"
                  value={manualTagInput}
                  onChange={(e) => {
                    setManualTagInput(e.target.value);
                    setCapturedTag(e.target.value);
                  }}
                  placeholder="e.g. PV-16501"
                  className="w-full bg-slate-950 border border-slate-700 text-white rounded px-2.5 py-1.5 font-mono focus:border-blue-500 outline-hidden"
                />
              </div>
            </div>
          </div>
        )}

        {/* Footer */}
        <div className="flex items-center justify-between px-5 py-3 bg-slate-950 border-t border-slate-800 shrink-0">
          <button
            onClick={() => setShowManualFallback((prev) => !prev)}
            className="text-xs text-slate-400 hover:text-white transition-colors underline underline-offset-2"
          >
            {showManualFallback ? 'Hide Manual Inputs' : 'Manual Entry Fallback'}
          </button>

          <div className="flex items-center gap-2">
            <button
              onClick={onClose}
              className="px-3.5 py-1.5 text-xs font-medium text-slate-400 hover:text-white bg-slate-900 border border-slate-700 rounded transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={handleApply}
              disabled={!capturedSerial && !capturedTag}
              className="flex items-center gap-1.5 px-4 py-1.5 text-xs font-semibold text-white bg-blue-700 hover:bg-blue-600 disabled:opacity-40 disabled:pointer-events-none rounded transition-colors shadow-xs"
            >
              Apply Captured Barcodes <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
