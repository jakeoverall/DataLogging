import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { filter, map, switchMap } from 'rxjs';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import { DeviceFormComponent, type DeviceFormValue } from '../device-form/device-form';
import type { DeviceSummary } from '../../models/device';

const PROTOCOL_OPTIONS = ['Ethernet', 'ROS2', 'CANOpen', 'WebSocket'];

@Component({
  selector: 'app-device-settings',
  standalone: true,
  imports: [CommonModule, DeviceFormComponent],
  template: `
    <article class="panel settings-panel">
      @if (device(); as currentDevice) {
        <h2>Device settings</h2>
        <app-device-form
          title="Edit device"
          submitLabel="Save changes"
          [showCancel]="false"
          [showDeviceId]="false"
          [protocolOptions]="protocolOptions"
          [initialValue]="form"
          (formSubmit)="saveChanges($event)"
        />
        <button type="button" class="danger-button" (click)="removeDevice()">Remove device</button>
      } @else {
        <p class="muted">Loading settings…</p>
      }
    </article>
  `,
  styles: [
    `:host { display: block; }
     .panel { background: rgba(15, 23, 42, 0.75); border: 1px solid rgba(148, 163, 184, 0.2); border-radius: 16px; padding: 1rem 1.25rem; }
     h2 { margin: 0 0 1rem; color: white; }
     .danger-button {
       margin-top: 0.9rem;
       background: rgba(239, 68, 68, 0.22);
       color: #fecaca;
       border: 1px solid rgba(248, 113, 113, 0.35);
       border-radius: 10px;
       cursor: pointer;
       font-weight: 600;
       padding: 0.8rem 1rem;
     }
     .muted { color: #b9c3d5; }
    `
  ]
})
export class DeviceSettingsComponent {
  private readonly api = inject(TelemetryApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly device = signal<DeviceSummary | undefined>(undefined);
  protected readonly protocolOptions = PROTOCOL_OPTIONS;
  protected form: DeviceFormValue = {
    deviceId: '',
    name: '',
    deviceType: 'Custom gateway',
    protocol: 'Ethernet',
    address: '127.0.0.1',
    port: 9000
  };

  constructor() {
    const destroyRef = inject(DestroyRef);

    this.route.parent?.paramMap
      .pipe(
        map((params) => params.get('id')),
        filter((deviceId): deviceId is string => Boolean(deviceId)),
        switchMap((deviceId) => this.api.streamDevices().pipe(
          map((devices) => devices.find((entry) => entry.id === deviceId || entry.deviceId === deviceId))
        )),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((device) => {
        this.device.set(device);
        if (device) {
          this.form = {
            deviceId: device.id,
            name: device.name,
            deviceType: device.deviceType,
            protocol: device.protocol,
            address: device.address,
            port: device.port
          };
        }
      });
  }

  protected saveChanges(formValue: DeviceFormValue) {
    const currentDevice = this.device();
    if (!currentDevice) {
      return;
    }

    this.api.updateDevice(currentDevice.id, {
      ...currentDevice,
      name: formValue.name,
      deviceType: formValue.deviceType,
      protocol: formValue.protocol,
      address: formValue.address,
      port: Number(formValue.port) || currentDevice.port
    }).subscribe(() => {
      this.device.set({
        ...currentDevice,
        name: formValue.name,
        deviceType: formValue.deviceType,
        protocol: formValue.protocol,
        address: formValue.address,
        port: Number(formValue.port) || currentDevice.port
      });
      this.form = {
        ...formValue,
        deviceId: currentDevice.id
      };
    });
  }

  protected removeDevice() {
    const currentDevice = this.device();
    if (!currentDevice) {
      return;
    }

    const confirmed = confirm(`Remove device "${currentDevice.name}" (${currentDevice.id})?`);
    if (!confirmed) {
      return;
    }

    this.api.removeDevice(currentDevice.id).subscribe((removed) => {
      if (removed) {
        this.router.navigateByUrl('/dashboard');
      }
    });
  }
}
