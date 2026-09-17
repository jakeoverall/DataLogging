import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import type { DeviceAlert, DeviceSummary } from '../../models/device';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent implements OnDestroy {
  private readonly api = inject(TelemetryApiService);
  private readonly subscriptions = new Subscription();

  protected readonly devices = signal<DeviceSummary[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly alerts = signal<DeviceAlert[]>([]);
  protected readonly protocolOptions = ['Ethernet', 'ROS2', 'CANOpen', 'WebSocket'];
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
    this.connectDeviceStream();
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
    this.isLoading.set(true);
    this.api.getDevices().subscribe((devices) => {
      this.devices.set(devices);
      this.alerts.set(this.buildAlerts(devices));
      this.isLoading.set(false);
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
    });
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
  }
}
