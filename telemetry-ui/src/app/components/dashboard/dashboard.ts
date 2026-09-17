import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import { DeviceFormComponent, type DeviceFormValue } from '../device-form/device-form';
import type { DeviceAlert, DeviceStatus, DeviceSummary } from '../../models/device';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, DeviceFormComponent],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent implements OnDestroy {
  private readonly api = inject(TelemetryApiService);
  private readonly subscriptions = new Subscription();
  private readonly updatingDeviceIds = signal<Set<string>>(new Set());

  protected readonly devices = signal<DeviceSummary[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly alerts = signal<DeviceAlert[]>([]);
  protected readonly isRegisterDialogOpen = signal(false);
  protected readonly searchText = signal('');
  protected readonly statusFilter = signal<'all' | DeviceStatus>('all');
  protected readonly enabledFilter = signal<'all' | 'enabled' | 'disabled'>('enabled');
  protected readonly protocolOptions = ['Ethernet', 'ROS2', 'CANOpen', 'WebSocket'];
  protected readonly statusFilterOptions: Array<'all' | DeviceStatus> = ['all', 'online', 'warning', 'offline'];
  protected readonly enabledFilterOptions: Array<'all' | 'enabled' | 'disabled'> = ['all', 'enabled', 'disabled'];
  protected form = this.createDefaultForm();

  protected readonly filteredDevices = computed(() => {
    const query = this.searchText().trim().toLowerCase();
    const status = this.statusFilter();
    const enabledState = this.enabledFilter();

    return this.devices().filter((device) => {
      if (status !== 'all' && device.status !== status) {
        return false;
      }

      if (enabledState === 'enabled' && !device.enabled) {
        return false;
      }

      if (enabledState === 'disabled' && device.enabled) {
        return false;
      }

      if (!query) {
        return true;
      }

      const searchable = `${device.name} ${device.deviceType} ${device.protocol} ${device.id} ${device.address}`.toLowerCase();
      return searchable.includes(query);
    });
  });

  protected readonly onlineCount = computed(() =>
    this.devices().filter((device) => device.enabled && device.status === 'online').length
  );
  protected readonly warningCount = computed(() =>
    this.devices().filter((device) => device.enabled && device.status === 'warning').length
  );
  protected readonly offlineCount = computed(() =>
    this.devices().filter((device) => device.enabled && device.status === 'offline').length
  );
  protected readonly disabledCount = computed(() =>
    this.devices().filter((device) => !device.enabled).length
  );
  protected readonly alertCount = computed(() => this.alerts().length);

  constructor() {
    this.loadDevices();
    this.connectDeviceStream();
  }

  protected refreshDevices() {
    this.loadDevices();
  }

  protected openRegisterDialog() {
    this.form = this.createDefaultForm();
    this.isRegisterDialogOpen.set(true);
  }

  protected closeRegisterDialog() {
    this.isRegisterDialogOpen.set(false);
  }

  protected registerDevice(formValue: DeviceFormValue) {
    const { deviceId, name, deviceType, protocol, address, port } = formValue;
    const routeDeviceId = this.toDeviceId(deviceId || name);

    if (!routeDeviceId) {
      return;
    }

    this.api.registerDevice(routeDeviceId, { name, deviceType, protocol, address, port, enabled: true }).subscribe(() => {
      this.loadDevices();
      this.isRegisterDialogOpen.set(false);
      this.form = this.createDefaultForm();
    });
  }

  protected onSearchInput(event: Event) {
    const input = event.target as HTMLInputElement;
    this.searchText.set(input.value ?? '');
  }

  protected onStatusFilterChange(event: Event) {
    const select = event.target as HTMLSelectElement;
    const value = select.value as 'all' | DeviceStatus;
    this.statusFilter.set(value);
  }

  protected onEnabledFilterChange(event: Event) {
    const select = event.target as HTMLSelectElement;
    const value = select.value as 'all' | 'enabled' | 'disabled';
    this.enabledFilter.set(value);
  }

  protected isUpdating(deviceId: string): boolean {
    return this.updatingDeviceIds().has(deviceId);
  }

  protected toggleDeviceEnabled(event: Event, device: DeviceSummary) {
    event.preventDefault();
    event.stopPropagation();

    if (this.isUpdating(device.id)) {
      return;
    }

    const nextEnabled = !device.enabled;
    this.updatingDeviceIds.update((current) => new Set(current).add(device.id));

    this.api.updateDevice(device.id, {
      name: device.name,
      deviceType: device.deviceType,
      protocol: device.protocol,
      address: device.address,
      port: device.port,
      enabled: nextEnabled
    }).subscribe({
      next: () => {
        this.devices.update((items) =>
          items.map((item) => item.id === device.id ? { ...item, enabled: nextEnabled } : item)
        );
        this.alerts.set(this.buildAlerts(this.devices()));
        this.loadDevices(false);
        this.updatingDeviceIds.update((current) => {
          const next = new Set(current);
          next.delete(device.id);
          return next;
        });
      },
      error: () => {
        // Keep the current UI state unchanged when the update fails.
        this.updatingDeviceIds.update((current) => {
          const next = new Set(current);
          next.delete(device.id);
          return next;
        });
      }
    });
  }

  private createDefaultForm(): DeviceFormValue {
    return {
      deviceId: '',
      name: '',
      deviceType: 'Custom gateway',
      protocol: 'Ethernet',
      address: '10.12.0.90',
      port: 9000
    };
  }

  private toDeviceId(value: string): string {
    return value.trim().toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '');
  }

  private loadDevices(showLoading = true) {
    if (showLoading) {
      this.isLoading.set(true);
    }

    this.api.getDevices().subscribe((devices) => {
      this.devices.set(devices);
      this.alerts.set(this.buildAlerts(devices));

      if (showLoading) {
        this.isLoading.set(false);
      }
    });
  }

  private connectDeviceStream() {
    const streamSubscription = this.api.streamDevices().subscribe((devices) => {
      this.devices.set(devices);
      this.alerts.set(this.buildAlerts(devices));
      this.isLoading.set(false);
    });

    this.subscriptions.add(streamSubscription);
  }

  private buildAlerts(devices: DeviceSummary[]): DeviceAlert[] {
    return devices.flatMap((device) => {
      const items: DeviceAlert[] = [];

      if (device.status === 'warning') {
        if (!device.enabled) {
          return items;
        }

        items.push({
          title: device.name,
          message: 'Telemetry drift detected on the primary bus.',
          severity: 'warning',
          deviceId: device.id
        });
      }

      if (device.status === 'offline') {
        if (!device.enabled) {
          return items;
        }

        items.push({
          title: device.name,
          message: 'Endpoint heartbeat missed for an extended interval.',
          severity: 'critical',
          deviceId: device.id
        });
      }

      if (device.enabled && device.alertCount > 0) {
        items.push({
          title: device.name,
          message: `${device.alertCount} active issue${device.alertCount > 1 ? 's' : ''} require review.`,
          severity: device.status === 'offline' ? 'critical' : 'info',
          deviceId: device.id
        });
      }

      return items;
    });
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
  }
}
