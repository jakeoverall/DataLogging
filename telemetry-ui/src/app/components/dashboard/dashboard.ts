import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import type { DeviceAlert, DeviceSummary } from '../../models/device';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent {
  private readonly api = inject(TelemetryApiService);

  protected readonly devices = signal<DeviceSummary[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly alerts = signal<DeviceAlert[]>([]);
  protected form = {
    name: '',
    deviceType: 'Custom gateway',
    protocol: 'Ethernet',
    address: '10.12.0.90',
    port: 9000
  };

  protected readonly onlineCount = computed(() => this.devices().filter((device) => device.status === 'online').length);
  protected readonly warningCount = computed(() => this.devices().filter((device) => device.status === 'warning').length);
  protected readonly offlineCount = computed(() => this.devices().filter((device) => device.status === 'offline').length);
  protected readonly alertCount = computed(() => this.alerts().length);

  constructor() {
    this.loadDevices();
  }

  protected refreshDevices() {
    this.loadDevices();
  }

  protected registerDevice() {
    const { name, deviceType, protocol, address, port } = this.form;
    const deviceId = name.trim().toLowerCase().replace(/\s+/g, '-');

    if (!deviceId) {
      return;
    }

    this.api.registerDevice(deviceId, { name, deviceType, protocol, address, port, enabled: true }).subscribe(() => {
      this.loadDevices();
      this.form = {
        name: '',
        deviceType: 'Custom gateway',
        protocol: 'Ethernet',
        address: '10.12.0.90',
        port: 9000
      };
    });
  }

  private loadDevices() {
    this.api.getDevices().subscribe((devices) => {
      this.devices.set(devices);
      this.isLoading.set(false);
    });

    this.api.getAlerts().subscribe((alerts) => this.alerts.set(alerts));
  }
}
