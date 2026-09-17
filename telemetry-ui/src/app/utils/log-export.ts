import type { DeviceLogEntry } from '../models/device';
import type { ExportColumn, ExportColumnKey, ExportFormat } from '../components/device-detail/device-logs.models';

export interface LogExportOptions<T extends object> {
  expandParsedJsonColumns?: boolean;
  getColumnValue: (entry: T, column: ExportColumnKey) => string;
  getParsedJsonSource?: (entry: T) => string | undefined;
}

interface ExportColumnDescriptor<T extends object> {
  label: string;
  resolve: (entry: T) => string;
}

export function buildDelimitedLogExport<T extends object>(
  entries: T[],
  columns: ExportColumn[],
  format: ExportFormat,
  options: LogExportOptions<T>
): string {
  const delimiter = format === 'csv' ? ',' : '\t';
  const descriptors: ExportColumnDescriptor<T>[] = columns.map((column) => ({
    label: column.label,
    resolve: (entry) => options.getColumnValue(entry, column.key)
  }));

  if (format === 'csv' && options.expandParsedJsonColumns) {
    const parsedColumns = collectParsedJsonColumns(entries, options.getParsedJsonSource);
    for (const parsedColumn of parsedColumns) {
      descriptors.push({
        label: parsedColumn.label,
        resolve: (entry) => parsedColumn.resolve(entry)
      });
    }
  }

  const header = descriptors.map((column) => escapeCell(column.label, delimiter)).join(delimiter);
  const body = entries
    .map((entry) =>
      descriptors
        .map((column) => escapeCell(column.resolve(entry), delimiter))
        .join(delimiter)
    )
    .join('\n');

  return `${header}\n${body}`;
}

interface ParsedJsonColumn<T extends object> {
  label: string;
  resolve: (entry: T) => string;
}

function collectParsedJsonColumns<T extends object>(
  entries: T[],
  getParsedJsonSource?: (entry: T) => string | undefined
): ParsedJsonColumn<T>[] {
  const parsedByEntry = new WeakMap<T, Map<string, string>>();
  const orderedKeys: string[] = [];
  const labels = new Map<string, string>();

  for (const entry of entries) {
    const source = getParsedJsonSource?.(entry) ?? '';
    const parsed = parseJsonObject(source);
    if (!parsed) {
      continue;
    }

    parsedByEntry.set(entry, parsed);
    for (const key of parsed.keys()) {
      if (labels.has(key)) {
        continue;
      }

      orderedKeys.push(key);
      labels.set(key, `JSON ${key}`);
    }
  }

  return orderedKeys.map((key) => ({
    label: labels.get(key) ?? `JSON ${key}`,
    resolve: (entry) => parsedByEntry.get(entry)?.get(key) ?? ''
  }));
}

function parseJsonObject(source: string): Map<string, string> | null {
  const trimmed = source.trim();
  if (!trimmed || (!trimmed.startsWith('{') && !trimmed.startsWith('['))) {
    return null;
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    const flattened = new Map<string, string>();
    flattenJsonValue(parsed, '', flattened);
    return flattened.size > 0 ? flattened : null;
  } catch {
    return null;
  }
}

function flattenJsonValue(value: unknown, prefix: string, output: Map<string, string>) {
  if (value === null || value === undefined) {
    if (prefix) {
      output.set(prefix, '');
    }
    return;
  }

  if (Array.isArray(value)) {
    if (prefix) {
      output.set(prefix, JSON.stringify(value));
    }
    return;
  }

  if (typeof value === 'object') {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) {
      if (prefix) {
        output.set(prefix, '{}');
      }
      return;
    }

    for (const [key, nestedValue] of entries) {
      const nextPrefix = prefix ? `${prefix}.${key}` : key;
      flattenJsonValue(nestedValue, nextPrefix, output);
    }

    return;
  }

  output.set(prefix || 'value', String(value));
}

function escapeCell(value: string, delimiter: string): string {
  const text = value ?? '';
  const needsQuotes = text.includes('"') || text.includes('\n') || text.includes('\r') || text.includes(delimiter);
  if (!needsQuotes) {
    return text;
  }

  return `"${text.replaceAll('"', '""')}"`;
}

export function getLogParsedJsonSource(entry: Pick<DeviceLogEntry, 'rawJson' | 'payloadText'>): string | undefined {
  return entry.rawJson ?? entry.payloadText;
}
