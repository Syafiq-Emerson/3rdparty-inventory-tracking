import { describe, expect, it } from 'vitest';
import { CSV_BOM, csvCell, toCsv } from './csv';

describe('csvCell', () => {
  it('quotes every value and doubles embedded quotes', () => {
    expect(csvCell('Warehouse A')).toBe('"Warehouse A"');
    expect(csvCell('6" valve')).toBe('"6"" valve"');
  });

  it('keeps commas and line breaks inside the quotes', () => {
    expect(csvCell('Bay 1, Rack 2')).toBe('"Bay 1, Rack 2"');
    expect(csvCell('line\nbreak')).toBe('"line\nbreak"');
  });

  it('neutralises values a spreadsheet would run as a formula', () => {
    for (const value of ['=SUM(A1)', '+1', '-2', '@cmd', '\tx']) {
      expect(csvCell(value).startsWith(`"'`)).toBe(true);
    }
  });

  it('leaves a hyphen inside a value alone', () => {
    expect(csvCell('PV-16501')).toBe('"PV-16501"');
  });

  it('writes empty cells for null and undefined', () => {
    expect(csvCell(null)).toBe('""');
    expect(csvCell(undefined)).toBe('""');
  });
});

describe('toCsv', () => {
  it('starts with a byte-order mark and ends rows with CRLF', () => {
    const csv = toCsv(['Serial', 'Tag'], [['F1', 'PV-1']]);
    expect(csv.startsWith(CSV_BOM)).toBe(true);
    expect(csv.slice(CSV_BOM.length)).toBe('"Serial","Tag"\r\n"F1","PV-1"\r\n');
  });
});
