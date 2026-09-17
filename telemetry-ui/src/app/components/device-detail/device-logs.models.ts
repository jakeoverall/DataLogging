export type ExportFormat = 'csv' | 'tsv';

export type ExportColumnKey =
  | 'timestamp'
  | 'level'
  | 'source'
  | 'dataType'
  | 'deviceId'
  | 'payloadText'
  | 'rawJson';

export interface ExportColumn {
  key: ExportColumnKey;
  label: string;
}

export const EXPORT_COLUMNS: ExportColumn[] = [
  { key: 'timestamp', label: 'Timestamp' },
  { key: 'level', label: 'Level' },
  { key: 'source', label: 'Source' },
  { key: 'dataType', label: 'Data Type' },
  { key: 'deviceId', label: 'Device ID' },
  { key: 'payloadText', label: 'Payload Text' },
  { key: 'rawJson', label: 'Raw JSON' }
];
