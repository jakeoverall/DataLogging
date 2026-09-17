import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import type { DeviceAlert, DeviceLogEntry, DeviceSummary, RuntimeModeResponse } from '../models/device';

const normalizeProtocol = (value: unknown) => {
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
    default:
      return 'Ethernet';
  }
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
    return new Observable<DeviceSummary[]>((subscriber) => {
      const source = new EventSource(`${this.baseUrl}/api/devices/stream`);
      let fallbackLoaded = false;

      const handleSnapshot = (event: Event) => {
        const message = event as MessageEvent<string>;
        const payload = this.parseStreamPayload(message.data);
        if (!payload) {
          return;
        }

        subscriber.next(payload);
      };

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
        source.close();
      };
    }).pipe(
      catchError(() => this.getDevices())
    );
  }

  getDeviceById(deviceId: string): Observable<DeviceSummary | undefined> {
    return this.getDevices().pipe(
      map((devices) => devices.find((device) => device.id === deviceId || device.deviceId === deviceId))
    );
  }

  getLogs(deviceId: string, limit = 20): Observable<DeviceLogEntry[]> {
    const fallback = MOCK_LOGS[deviceId] ?? [];

    return this.http.get<any[]>(`${this.baseUrl}/api/logs?deviceId=${encodeURIComponent(deviceId)}&limit=${limit}`).pipe(
      map((rows) =>
        rows.map((row, index) => {
          const metadata = row.metadata ?? {};
          const payloadBase64 = row.payloadBase64 ?? '';
          const payloadText = row.payloadText ?? (
            payloadBase64 ? this.decodeBase64(payloadBase64) : 'No payload preview available.'
          );

          const level: 'info' | 'warn' | 'error' = metadata.Priority === 'High' || metadata.Priority === 'high'
            ? 'error'
            : metadata.Priority === 'Medium' || metadata.Priority === 'medium'
              ? 'warn'
              : 'info';

          return {
            id: metadata.RecordId ?? `${deviceId}-${index}`,
            deviceId: metadata.DeviceId ?? deviceId,
            source: metadata.Source ?? 'unknown',
            dataType: metadata.DataType ?? 'telemetry',
            timestamp: metadata.RecordedTimestamp ?? new Date().toISOString(),
            level,
            payloadText
          } satisfies DeviceLogEntry;
        }) as DeviceLogEntry[]
      ),
      catchError(() => of(fallback))
    );
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

  private decodeBase64(value: string): string {
    try {
      return atob(value);
    } catch {
      return 'No payload preview available.';
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
  }

  registerDevice(deviceId: string, payload: Partial<DeviceSummary>) {
    return this.http.put(`${this.baseUrl}/api/devices/${encodeURIComponent(deviceId)}`, {
      name: payload.name ?? deviceId,
      deviceType: payload.deviceType ?? 'Custom',
      protocol: payload.protocol ?? 'Ethernet',
      address: payload.address ?? '127.0.0.1',
      port: payload.port ?? 9000,
      enabled: payload.enabled ?? true,
      properties: {}
    }).pipe(catchError(() => of(true)));
  }
}
