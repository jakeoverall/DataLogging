export type DeviceStatus = 'online' | 'warning' | 'offline';

export interface DeviceSummary {
  id: string;
  deviceId?: string;
  name: string;
  deviceType: string;
  protocol: string;
  status: DeviceStatus;
  health: number;
  address: string;
  port: number;
  enabled: boolean;
  lastSeen: string;
  lastValue?: string;
  alertCount: number;
}

export interface DeviceAlert {
  title: string;
  message: string;
  severity: 'info' | 'warning' | 'critical';
  deviceId: string;
}

export interface DeviceLogEntry {
  id: string;
  deviceId: string;
  source: string;
  dataType: string;
  timestamp: string;
  level: 'info' | 'warn' | 'error';
  payloadText: string;
  rawJson?: string;
}

export interface RuntimeModeResponse {
  useMockData: boolean;
  dataMode: 'mock' | 'live';
}
