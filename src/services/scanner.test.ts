import { describe, expect, it } from 'vitest';
import { classifyBarcode, placeScan } from './scanner';

describe('classifyBarcode', () => {
  it('treats hyphenated instrument tags as tag numbers', () => {
    for (const tag of ['PV-16501', 'FT-10402', 'LT-20415', 'MOV-201']) {
      expect(classifyBarcode(tag).type).toBe('TAG_NUMBER');
    }
  });

  it('treats letter-and-digit manufacturer codes as serial numbers', () => {
    for (const serial of ['F005327261', 'S991048210', '9823471029']) {
      expect(classifyBarcode(serial).type).toBe('SERIAL_NUMBER');
    }
  });

  it('trims the scanned value', () => {
    expect(classifyBarcode('  PV-16501 \n').raw).toBe('PV-16501');
  });

  it('reports an empty scan as unknown', () => {
    expect(classifyBarcode('   ')).toMatchObject({ type: 'UNKNOWN', confidence: 0 });
  });

  it('reports a short code without a hyphen as unknown', () => {
    expect(classifyBarcode('AB1').type).toBe('UNKNOWN');
  });
});

describe('placeScan', () => {
  const empty = { serial: '', tag: '' };

  it('ignores an empty scan', () => {
    expect(placeScan(empty, '   ')).toBeNull();
  });

  it('fills the tag first when the tag is scanned first', () => {
    expect(placeScan(empty, 'PV-16501')).toMatchObject({
      slots: { serial: '', tag: 'PV-16501' },
      changed: true,
      tone: 'info',
    });
  });

  it('fills the serial first when the serial is scanned first', () => {
    expect(placeScan(empty, 'F005327261')?.slots).toEqual({ serial: 'F005327261', tag: '' });
  });

  it('puts an unrecognised first scan in the serial and warns', () => {
    expect(placeScan(empty, 'AB1')).toMatchObject({ slots: { serial: 'AB1', tag: '' }, tone: 'warning' });
  });

  it('completes the pair in either order', () => {
    expect(placeScan({ serial: 'F005327261', tag: '' }, 'PV-16501')).toMatchObject({
      slots: { serial: 'F005327261', tag: 'PV-16501' },
      tone: 'success',
    });
    expect(placeScan({ serial: '', tag: 'PV-16501' }, 'F005327261')).toMatchObject({
      slots: { serial: 'F005327261', tag: 'PV-16501' },
      tone: 'success',
    });
  });

  it('never overwrites a filled serial with another serial', () => {
    const current = { serial: 'F005327261', tag: '' };
    const placement = placeScan(current, 'S991048210');
    expect(placement?.changed).toBe(false);
    expect(placement?.slots).toBe(current);
    expect(placement?.tone).toBe('warning');
  });

  it('never overwrites a filled tag with another tag', () => {
    expect(placeScan({ serial: '', tag: 'PV-16501' }, 'FT-10402')?.changed).toBe(false);
  });

  it('ignores a repeat of the same barcode regardless of case', () => {
    expect(placeScan({ serial: 'F005327261', tag: '' }, 'f005327261')?.message).toContain('Duplicate scan ignored');
    expect(placeScan({ serial: 'A', tag: 'PV-1' }, 'pv-1')?.message).toContain('Duplicate scan ignored');
  });

  it('refuses a third barcode once both fields are filled', () => {
    const placement = placeScan({ serial: 'F005327261', tag: 'PV-16501' }, 'LT-20415');
    expect(placement?.changed).toBe(false);
    expect(placement?.message).toContain('Clear a field');
  });
});
