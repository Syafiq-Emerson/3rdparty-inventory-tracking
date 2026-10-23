import { ScanClassificationResult } from '../types/inventory';

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

export interface ScanSlots {
  serial: string;
  tag: string;
}

export type ScanTone = 'success' | 'warning' | 'info';

export interface ScanPlacement {
  slots: ScanSlots;
  changed: boolean;
  message: string;
  tone: ScanTone;
}

const sameValue = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

/**
 * Decides where one scanned barcode goes, in either scan order. A filled field is never overwritten:
 * the operator has to clear or swap it first. Returns null for an empty scan.
 */
export function placeScan(
  current: ScanSlots,
  rawText: string,
  rules: ClassificationRules = defaultClassificationRules
): ScanPlacement | null {
  const text = rawText.trim();
  if (!text) return null;

  const serial = current.serial.trim();
  const tag = current.tag.trim();
  const { type } = classifyBarcode(text, rules);
  const keep = (message: string): ScanPlacement => ({ slots: current, changed: false, message, tone: 'warning' });

  if (!serial && !tag) {
    if (type === 'TAG_NUMBER') {
      return {
        slots: { serial: '', tag: text },
        changed: true,
        message: `Captured Tag Number "${text}". Waiting for Serial Number.`,
        tone: 'info',
      };
    }
    return {
      slots: { serial: text, tag: '' },
      changed: true,
      message:
        type === 'UNKNOWN'
          ? `Captured "${text}" as Serial Number (format not recognised). Swap if it is the tag.`
          : `Captured Serial Number "${text}". Waiting for Tag Number.`,
      tone: type === 'UNKNOWN' ? 'warning' : 'info',
    };
  }

  if (serial && !tag) {
    if (sameValue(text, serial)) return keep(`Duplicate scan ignored: "${text}" is already the Serial Number.`);
    if (type === 'SERIAL_NUMBER') {
      return keep(`Serial Number is already filled. Clear or swap it before replacing it with "${text}".`);
    }
    return {
      slots: { serial, tag: text },
      changed: true,
      message: `Captured Tag Number "${text}". Both identifiers are ready.`,
      tone: 'success',
    };
  }

  if (!serial && tag) {
    if (sameValue(text, tag)) return keep(`Duplicate scan ignored: "${text}" is already the Tag Number.`);
    if (type === 'TAG_NUMBER') {
      return keep(`Tag Number is already filled. Clear or swap it before replacing it with "${text}".`);
    }
    return {
      slots: { serial: text, tag },
      changed: true,
      message: `Captured Serial Number "${text}". Both identifiers are ready.`,
      tone: 'success',
    };
  }

  if (sameValue(text, serial) || sameValue(text, tag)) {
    return keep(`Duplicate scan ignored: "${text}" is already captured.`);
  }
  return keep(`Both fields are already filled (Serial ${serial}, Tag ${tag}). Clear a field before scanning again.`);
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
