import { ScanClassificationResult, ScanClassificationType } from '../types/inventory';

/**
 * Barcode Classification Engine
 * Evaluates scanned linear barcode strings to classify into Serial Number or Tag Number
 * according to industrial facility conventions.
 */

export interface ClassificationRules {
  // Regex for Tag Number (usually contains hyphens like PV-16501, FT-10402, LT-20415)
  tagNumberRegex: RegExp;
  // Regex for Serial Number (usually alphanumeric like F005327261, S991048210, 10-14 chars)
  serialNumberRegex: RegExp;
}

export const defaultClassificationRules: ClassificationRules = {
  // Industrial Tag Numbers typically follow ISA-5.1 instrumentation tags (e.g., PV-16501, FT-10402, MOV-201, PT-33018, XV-10)
  tagNumberRegex: /^[A-Z]{1,5}-?\d{2,7}[A-Z0-9]*$/i,
  // Serial Numbers are generally pure alphanumeric without hyphens (e.g., F005327261, 9823471029, SN2026-X)
  serialNumberRegex: /^[A-Z0-9]{6,20}$/i,
};

export function classifyBarcode(
  scannedValue: string,
  rules: ClassificationRules = defaultClassificationRules
): ScanClassificationResult {
  const value = scannedValue.trim();

  if (!value) {
    return {
      type: 'UNKNOWN',
      confidence: 0,
      raw: value,
      reason: 'Empty scanned payload',
    };
  }

  const hasHyphen = value.includes('-');
  const matchesTag = rules.tagNumberRegex.test(value);
  const matchesSerial = rules.serialNumberRegex.test(value);

  // Strong Tag indicator: contains hyphen followed by digits (e.g. PV-16501)
  if (hasHyphen && matchesTag) {
    return {
      type: 'TAG_NUMBER',
      confidence: 0.95,
      raw: value,
      reason: 'Matches ISA process instrumentation tag pattern (contains hyphen)',
    };
  }

  // Strong Serial indicator: alphanumeric without hyphen, 6+ chars
  if (!hasHyphen && matchesSerial && !matchesTag) {
    return {
      type: 'SERIAL_NUMBER',
      confidence: 0.95,
      raw: value,
      reason: 'Matches equipment manufacturer serial number alphanumeric pattern',
    };
  }

  // If no hyphen and matches both:
  if (!hasHyphen && matchesSerial && matchesTag) {
    // If it starts with letter followed by mainly numbers, likely serial (e.g., F005327261)
    if (/^[A-Z]\d{6,14}$/i.test(value)) {
      return {
        type: 'SERIAL_NUMBER',
        confidence: 0.85,
        raw: value,
        reason: 'Letter prefix followed by 6+ digits matches equipment serial format',
      };
    }
    return {
      type: 'SERIAL_NUMBER',
      confidence: 0.7,
      raw: value,
      reason: 'Standard alphanumeric equipment identifier',
    };
  }

  // Fallback pattern matching
  if (hasHyphen) {
    return {
      type: 'TAG_NUMBER',
      confidence: 0.75,
      raw: value,
      reason: 'Contains process loop hyphenation delimiter',
    };
  }

  return {
    type: 'UNKNOWN',
    confidence: 0.4,
    raw: value,
    reason: 'Could not definitively distinguish between Serial Number and Tag Number',
  };
}

/**
 * Handheld Scanner Keyboard Buffer Listener
 * USB and Bluetooth linear barcode scanners emulate a rapid sequence of keypress events
 * terminating with an 'Enter' key (key code 13 / 'Enter').
 */
export class HandheldScannerListener {
  private buffer: string = '';
  private lastKeyTime: number = 0;
  private readonly interKeyThresholdMs = 60; // Scanner keystrokes are < 30ms apart; humans type > 80ms apart
  private onScanCallback: (scannedText: string) => void;
  private boundHandler: (event: KeyboardEvent) => void;
  private isActive: boolean = false;

  constructor(onScan: (scannedText: string) => void) {
    this.onScanCallback = onScan;
    this.boundHandler = this.handleKeyDown.bind(this);
  }

  public start() {
    if (this.isActive) return;
    window.addEventListener('keydown', this.boundHandler, true);
    this.isActive = true;
  }

  public stop() {
    if (!this.isActive) return;
    window.removeEventListener('keydown', this.boundHandler, true);
    this.buffer = '';
    this.isActive = false;
  }

  private handleKeyDown(event: KeyboardEvent) {
    // Ignore input if user is explicitly typing into an input or textarea unless it is rapid scanner input
    const target = event.target as HTMLElement | null;
    const isInputElement =
      target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable);

    const currentTime = Date.now();
    const isRapid = currentTime - this.lastKeyTime <= this.interKeyThresholdMs;
    this.lastKeyTime = currentTime;

    if (event.key === 'Enter') {
      // If buffer has sufficient length (barcodes are generally >= 4 characters)
      if (this.buffer.length >= 3) {
        // Prevent form submission if scanner pressed enter
        event.preventDefault();
        event.stopPropagation();
        const scannedText = this.buffer.trim();
        this.buffer = '';
        this.onScanCallback(scannedText);
        return;
      }
      this.buffer = '';
      return;
    }

    // Only collect printable characters
    if (event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey) {
      if (isInputElement && !isRapid && this.buffer.length === 0) {
        // Human typing into a regular input field; don't capture into scanner buffer
        return;
      }

      // Scanner sends keystrokes with < 50ms intervals
      this.buffer += event.key;
    }
  }
}
