import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import type { DeviceAlert, DeviceLogEntry, DeviceSummary, RuntimeModeResponse } from '../models/device';

const MAX_LOG_EVENT_BATCH = 20;
const LOG_STREAM_RECONNECT_MS = 1000;

export type DeviceLogStreamState = 'connecting' | 'live' | 'reconnecting';

export interface DeviceLogStreamUpdate {
  state: DeviceLogStreamState;
  entries: DeviceLogEntry[];
}

const normalizeProtocol = (value: unknown): string => {
  if (typeof value === 'string') {
    return value;
  }

  switch (value) {
    case 0:
      return 'ROS2';
    case 1:
      return 'CANOpen';
    case 2:
      return 'Ethernet';
    case 3:
      return 'WebSocket';
    default:
      return 'Ethernet';
  }
};

const normalizeProtocolValue = (value: unknown): number => {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  switch (String(value ?? 'Ethernet').trim().toLowerCase()) {
    case 'ros2':
    case 'ros-2':
      return 0;
    case 'canopen':
    case 'can-open':
      return 1;
    case 'ethernet':
      return 2;
    case 'websocket':
    case 'ws':
      return 3;
    default:
      return 2;
  }
};

const toStringRecord = (value: Record<string, unknown>): Record<string, string> => {
  const entries = Object.entries(value)
    .filter(([, entryValue]) => entryValue !== undefined && entryValue !== null)
    .map(([key, entryValue]) => [key, String(entryValue)] as const);

  return Object.fromEntries(entries);
};

const normalizeDevice = (item: Partial<DeviceSummary> & { deviceId?: string }): DeviceSummary => ({
  id: item.id ?? item.deviceId ?? 'unknown-device',
  deviceId: item.deviceId ?? item.id ?? 'unknown-device',
  name: item.name ?? 'Unnamed device',
  deviceType: item.deviceType ?? 'Telemetry device',
  protocol: normalizeProtocol(item.protocol),
  status: item.status ?? 'online',
  health: item.health ?? 90,
  address: item.address ?? '127.0.0.1',
  port: item.port ?? 9000,
  enabled: item.enabled ?? true,
  lastSeen: item.lastSeen ?? 'Just now',
  lastValue: item.lastValue ?? 'No recent value',
  alertCount: item.alertCount ?? 0
});

const MOCK_DEVICES: DeviceSummary[] = [
  {
    id: 'vehicle-101',
    name: 'Vehicle 101',
    deviceType: 'CAN Gateway',
    protocol: 'CANOpen',
    status: 'online',
    health: 97,
    address: '10.12.0.21',
    port: 5001,
    enabled: true,
    lastSeen: 'Just now',
    lastValue: 'RPM 1420',
    alertCount: 1
  },
  {
    id: 'vehicle-204',
    name: 'Vehicle 204',
    deviceType: 'ROS Bridge',
    protocol: 'ROS2',
    status: 'warning',
    health: 81,
    address: '10.12.0.42',
    port: 5010,
    enabled: true,
    lastSeen: '3 min ago',
    lastValue: 'Temperature 82C',
    alertCount: 3
  },
  {
    id: 'sensor-rack-7',
    name: 'Sensor Rack 7',
    deviceType: 'Ethernet Sensor',
    protocol: 'Ethernet',
    status: 'offline',
    health: 52,
    address: '10.12.0.77',
    port: 8080,
    enabled: false,
    lastSeen: '12 min ago',
    lastValue: 'No heartbeat',
    alertCount: 2
  }
];

const MOCK_LOGS: Record<string, DeviceLogEntry[]> = {
  'vehicle-101': [
    { id: '101', deviceId: 'vehicle-101', source: 'mock-can', dataType: 'telemetry', timestamp: '2026-09-17T00:08:00Z', level: 'info', payloadText: 'Bus load 68%; throttle stable; RPM 1420.' },
    { id: '102', deviceId: 'vehicle-101', source: 'mock-can', dataType: 'alert', timestamp: '2026-09-17T00:07:54Z', level: 'warn', payloadText: 'Temperature drift near threshold for 36 seconds.' },
    { id: '103', deviceId: 'vehicle-101', source: 'mock-ros', dataType: 'telemetry', timestamp: '2026-09-17T00:07:40Z', level: 'info', payloadText: 'Wheel speed stable at 88 km/h.' }
  ],
  'vehicle-204': [
    { id: '201', deviceId: 'vehicle-204', source: 'mock-ros', dataType: 'telemetry', timestamp: '2026-09-17T00:09:00Z', level: 'warn', payloadText: 'Thermal envelope crossed: 82C on node 3.' },
    { id: '202', deviceId: 'vehicle-204', source: 'mock-ros', dataType: 'event', timestamp: '2026-09-17T00:08:10Z', level: 'info', payloadText: 'Bridge resubscription completed after reconnection.' },
    { id: '203', deviceId: 'vehicle-204', source: 'mock-can', dataType: 'telemetry', timestamp: '2026-09-17T00:08:01Z', level: 'error', payloadText: 'Packet loss increased to 4.9% on channel B.' }
  ],
  'sensor-rack-7': [
    { id: '301', deviceId: 'sensor-rack-7', source: 'mock-ethernet', dataType: 'telemetry', timestamp: '2026-09-17T00:05:00Z', level: 'error', payloadText: 'Endpoint heartbeat absent for 12 minutes.' },
    { id: '302', deviceId: 'sensor-rack-7', source: 'mock-ethernet', dataType: 'event', timestamp: '2026-09-17T00:04:20Z', level: 'warn', payloadText: 'Link status downgraded to degraded mode.' }
  ]
};

@Injectable({ providedIn: 'root' })
export class TelemetryApiService {

  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5147';
  private readonly liveDeviceStream$ = this.createLiveDeviceStream();

  getRuntimeMode(): Observable<RuntimeModeResponse> {
    return this.http.get<RuntimeModeResponse>(`${this.baseUrl}/api/runtime/mode`).pipe(
      catchError(() => of({ useMockData: true, dataMode: 'mock' as const }))
    );
  }

  getDevices(): Observable<DeviceSummary[]> {
    return this.http.get<Array<Partial<DeviceSummary> & { deviceId?: string }>>(`${this.baseUrl}/api/devices`).pipe(
      map((devices) => devices.map((device) => normalizeDevice(device))),
      catchError(() => of(MOCK_DEVICES))
    );
  }

  streamDevices(): Observable<DeviceSummary[]> {
    return this.liveDeviceStream$;
  }

  private createLiveDeviceStream(): Observable<DeviceSummary[]> {
    return new Observable<DeviceSummary[]>((subscriber) => {
      const source = new EventSource(`${this.baseUrl}/api/devices/stream`);
      let fallbackLoaded = false;
      let initialLoadSent = false;

      const emitCurrentDevices = () => {
        if (initialLoadSent) {
          return;
        }

        initialLoadSent = true;
        this.getDevices().subscribe((devices) => subscriber.next(devices));
      };

      const handleSnapshot = (event: Event) => {
        const message = event as MessageEvent<string>;
        const payload = this.parseStreamPayload(message.data);
        if (!payload) {
          return;
        }

        initialLoadSent = true;
        subscriber.next(payload);
      };

      source.addEventListener('connected', emitCurrentDevices as EventListener);
      source.addEventListener('snapshot', handleSnapshot as EventListener);
      source.onerror = () => {
        if (fallbackLoaded) {
          return;
        }

        fallbackLoaded = true;
        this.getDevices().subscribe((devices) => subscriber.next(devices));
      };

      return () => {
        source.removeEventListener('snapshot', handleSnapshot as EventListener);
        source.removeEventListener('connected', emitCurrentDevices as EventListener);
        source.close();
      };
    }).pipe(
      catchError(() => this.getDevices()),
      shareReplay({ bufferSize: 1, refCount: true })
    );
  }

  getDeviceById(deviceId: string): Observable<DeviceSummary | undefined> {
    return this.streamDevices().pipe(
      map((devices) => devices.find((device) => device.id === deviceId || device.deviceId === deviceId))
    );
  }

  getLogs(deviceId: string, limit = 20): Observable<DeviceLogEntry[]> {
    const fallback = MOCK_LOGS[deviceId] ?? [];

    return this.http.get<any[]>(`${this.baseUrl}/api/logs?deviceId=${encodeURIComponent(deviceId)}&limit=${limit}`).pipe(
      map((rows) =>
        rows.map((row, index) => this.toDeviceLogEntry(row, deviceId, index)).filter((entry) => !!entry) as DeviceLogEntry[]
      ),
      catchError(() => of(fallback))
    );
  }

  streamLogs(deviceId: string, limit = MAX_LOG_EVENT_BATCH): Observable<DeviceLogStreamUpdate> {
    return new Observable<DeviceLogStreamUpdate>((subscriber) => {
      let source: EventSource | null = null;
      let disposed = false;
      let reconnectHandle: ReturnType<typeof setTimeout> | null = null;

      const connect = () => {
        if (disposed) {
          return;
        }

        subscriber.next({ state: 'connecting', entries: [] });
        source = new EventSource(
          `${this.baseUrl}/api/logs/stream?deviceId=${encodeURIComponent(deviceId)}&batchSize=${limit}&flushIntervalMs=250`
        );

        const handleConnected = () => {
          subscriber.next({ state: 'live', entries: [] });
        };

        const handleLogBatch = (event: Event) => {
          const payload = this.parseLiveLogEventBatch((event as MessageEvent<string>).data, deviceId);
          if (payload.length === 0) {
            return;
          }

          subscriber.next({ state: 'live', entries: payload.slice(0, limit) });
        };

        const handleError = () => {
          if (disposed) {
            return;
          }

          subscriber.next({ state: 'reconnecting', entries: [] });

          source?.removeEventListener('connected', handleConnected as EventListener);
          source?.removeEventListener('logs', handleLogBatch as EventListener);
          source?.close();
          source = null;

          if (reconnectHandle !== null) {
            clearTimeout(reconnectHandle);
          }

          reconnectHandle = setTimeout(() => {
            reconnectHandle = null;
            connect();
          }, LOG_STREAM_RECONNECT_MS);
        };

        source.addEventListener('connected', handleConnected as EventListener);
        source.addEventListener('logs', handleLogBatch as EventListener);
        source.onerror = handleError;
      };

      connect();

      return () => {
        disposed = true;

        if (reconnectHandle !== null) {
          clearTimeout(reconnectHandle);
          reconnectHandle = null;
        }

        source?.close();
        source = null;
      };
    });
  }

  getAlerts(): Observable<DeviceAlert[]> {
    return this.getDevices().pipe(
      map((devices) =>
        devices.flatMap((device) => {
          const items: DeviceAlert[] = [];

          if (device.status === 'warning') {
            items.push({
              title: device.name,
              message: 'Telemetry drift detected on the primary bus.',
              severity: 'warning',
              deviceId: device.id
            });
          }

          if (device.status === 'offline') {
            items.push({
              title: device.name,
              message: 'Endpoint heartbeat missed for an extended interval.',
              severity: 'critical',
              deviceId: device.id
            });
          }

          if (device.alertCount > 0) {
            items.push({
              title: device.name,
              message: `${device.alertCount} active issue${device.alertCount > 1 ? 's' : ''} require review.`,
              severity: device.status === 'offline' ? 'critical' : 'info',
              deviceId: device.id
            });
          }

          return items;
        })
      )
    );
  }

  private toDeviceLogEntry(row: any, deviceId: string, index: number): DeviceLogEntry | null {
    const metadata = row?.metadata ?? row ?? {};
    const payloadBase64 = row?.payloadBase64 ?? '';
    const payloadText = row?.payloadText ?? (
      payloadBase64 ? this.decodeBase64(payloadBase64) : 'No payload preview available.'
    );

    const priority = metadata.Priority ?? metadata.priority ?? 'Normal';
    const level: 'info' | 'warn' | 'error' = priority === 'High' || priority === 'high'
      ? 'error'
      : priority === 'Medium' || priority === 'medium'
        ? 'warn'
        : 'info';

    const timestamp =
      metadata.RecordedTimestamp
      ?? metadata.recordedTimestamp
      ?? row?.RecordedTimestamp
      ?? row?.timestamp
      ?? new Date().toISOString();

    return {
      id: metadata.RecordId ?? metadata.recordId ?? row?.RecordId ?? row?.id ?? `${deviceId}-${index}`,
      deviceId: metadata.DeviceId ?? metadata.deviceId ?? row?.DeviceId ?? row?.deviceId ?? deviceId,
      source: metadata.Source ?? metadata.source ?? row?.Source ?? row?.source ?? 'unknown',
      dataType: metadata.DataType ?? metadata.dataType ?? row?.DataType ?? row?.dataType ?? 'telemetry',
      timestamp,
      level,
      payloadText
    } satisfies DeviceLogEntry;
  }

  private parseLiveLogEventBatch(value: string, deviceId: string): DeviceLogEntry[] {
    try {
      const parsed = JSON.parse(value) as {
        items?: any[];
      };

      if (!parsed || !Array.isArray(parsed.items)) {
        return [];
      }

      return parsed.items
        .map((item, index) => this.toDeviceLogEntry(item, deviceId, index))
        .filter((entry) => !!entry) as DeviceLogEntry[];
    } catch {
      return [];
    }
  }

  private decodeBase64(value: string): string {
    try {
      return atob(value);
    } catch {
      return 'No payload preview available.';
    }
  }

  private parseStreamPayload(value: string): DeviceSummary[] | null {
    try {
      const parsed = JSON.parse(value) as { devices?: Array<Partial<DeviceSummary> & { deviceId?: string }> };
      if (!parsed.devices) {
        return null;
      }

      return parsed.devices.map((device) => normalizeDevice(device));
    } catch {
      return null;
    }
  }

  registerDevice(deviceId: string, payload: Partial<DeviceSummary>) {
    const port = String(payload.port ?? 9000);
    const requestBody = {
      name: payload.name ?? deviceId,
      deviceType: payload.deviceType ?? 'Custom',
      protocol: normalizeProtocolValue(payload.protocol ?? 'Ethernet'),
      address: payload.address ?? '127.0.0.1',
      port,
      enabled: payload.enabled ?? true,
      properties: toStringRecord({
        name: payload.name ?? deviceId,
        deviceType: payload.deviceType ?? 'Custom',
        protocol: normalizeProtocol(payload.protocol ?? 'Ethernet'),
        address: payload.address ?? '127.0.0.1',
        port,
        enabled: payload.enabled ?? true
      })
    };

    return this.http.put(`${this.baseUrl}/api/devices/${encodeURIComponent(deviceId)}`, requestBody).pipe(
      catchError(() => of(true))
    );
  }

  updateDevice(deviceId: string, payload: Partial<DeviceSummary>) {
    return this.registerDevice(deviceId, payload);
  }

  removeDevice(deviceId: string): Observable<boolean> {
    return this.http.delete(`${this.baseUrl}/api/devices/${encodeURIComponent(deviceId)}`).pipe(
      map(() => true),
      catchError(() => of(false))
    );
  }
}
