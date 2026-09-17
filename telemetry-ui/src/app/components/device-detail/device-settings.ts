import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import type { DeviceSummary } from '../../models/device';

@Component({
  selector: 'app-device-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <article class="panel settings-panel">
      @if (device(); as currentDevice) {
        <h2>Device settings</h2>
        <div class="settings-grid">
          <label>
            <span>Name</span>
            <input type="text" [(ngModel)]="form.name" name="name" />
          </label>
          <label>
            <span>Protocol</span>
            <input type="text" [(ngModel)]="form.protocol" name="protocol" />
          </label>
          <label>
            <span>Address</span>
            <input type="text" [(ngModel)]="form.address" name="address" />
          </label>
          <label>
            <span>Port</span>
            <input type="number" [(ngModel)]="form.port" name="port" />
          </label>
        </div>
        <button type="button" class="primary-button" (click)="saveChanges()">Save changes</button>
      } @else {
        <p class="muted">Loading settings…</p>
      }
    </article>
  `,
  styles: [
    `:host { display: block; }
     .panel { background: rgba(15, 23, 42, 0.75); border: 1px solid rgba(148, 163, 184, 0.2); border-radius: 16px; padding: 1rem 1.25rem; }
     h2 { margin: 0 0 1rem; color: white; }
     .settings-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
     label { display: flex; flex-direction: column; gap: 0.45rem; color: #e2e8f0; }
     input { background: rgba(15, 23, 42, 0.4); border: 1px solid rgba(148, 163, 184, 0.18); border-radius: 10px; color: white; padding: 0.8rem 0.9rem; }
     .primary-button { background: linear-gradient(135deg, #38bdf8, #8b5cf6); color: white; border: none; border-radius: 10px; cursor: pointer; font-weight: 600; padding: 0.8rem 1rem; margin-top: 1rem; }
     .muted { color: #b9c3d5; }
     @media (max-width: 760px) { .settings-grid { grid-template-columns: 1fr; } }
    `
  ]
})
export class DeviceSettingsComponent {
  private readonly api = inject(TelemetryApiService);
  private readonly route = inject(ActivatedRoute);
  protected readonly device = signal<DeviceSummary | undefined>(undefined);
  protected form = { name: '', protocol: 'Ethernet', address: '127.0.0.1', port: 9000 };

  constructor() {
    this.route.parent?.paramMap.subscribe((params) => {
      const deviceId = params.get('id');
      if (!deviceId) {
        return;
      }

      this.api.getDeviceById(deviceId).subscribe((device) => {
        this.device.set(device);
        if (device) {
          this.form = {
            name: device.name,
            protocol: device.protocol,
            address: device.address,
            port: device.port
          };
        }
      });
    });
  }

  protected saveChanges() {
    const currentDevice = this.device();
    if (!currentDevice) {
      return;
    }

    this.api.registerDevice(currentDevice.id, {
      ...currentDevice,
      name: this.form.name,
      protocol: this.form.protocol,
      address: this.form.address,
      port: Number(this.form.port) || currentDevice.port
    }).subscribe();
  }
}
