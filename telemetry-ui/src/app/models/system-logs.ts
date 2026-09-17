export interface SystemLogFileDescriptor {
  name: string;
  path: string;
  sizeBytes: number;
  lastModifiedUtc: string;
  isCurrent: boolean;
}
