/** Byte-order mark, so Excel reads the file as UTF-8 instead of the local code page. */
export const CSV_BOM = '﻿';

// A cell that starts with one of these is run as a formula by Excel and LibreOffice.
const FORMULA_START = /^[=+\-@\t\r]/;

/** One CSV cell: formula-like text is prefixed with ' and every cell is quoted with quotes doubled. */
export function csvCell(value: string | number | boolean | null | undefined): string {
  let text = value === null || value === undefined ? '' : String(value);
  if (FORMULA_START.test(text)) text = `'${text}`;
  return `"${text.replace(/"/g, '""')}"`;
}

/** Header row plus data rows, CRLF line endings, with a BOM for Excel. */
export function toCsv(headers: string[], rows: (string | number | boolean | null | undefined)[][]): string {
  const lines = [headers, ...rows].map((row) => row.map(csvCell).join(','));
  return CSV_BOM + lines.join('\r\n') + '\r\n';
}
