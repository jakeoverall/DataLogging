import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import type { DeviceSummary } from '../../models/device';

@Component({
  selector: 'app-device-overview',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (device(); as currentDevice) {
      <div class="panel-grid">
        <article class="panel">
          <h2>Live telemetry</h2>
          <p class="muted">Current value: {{ currentDevice.lastValue }}</p>
          <div class="metric-stack">
            <div>
              <span>Signal quality</span>
              <strong>{{ currentDevice.health }}%</strong>
            </div>
            <div>
              <span>Alerts</span>
              <strong>{{ currentDevice.alertCount }}</strong>
            </div>
            <div>
              <span>Enabled</span>
              <strong>{{ currentDevice.enabled ? 'Yes' : 'No' }}</strong>
            </div>
          </div>
        </article>

        <article class="panel">
          <h2>Connection</h2>
          <ul class="info-list">
            <li><span>Type</span><strong>{{ currentDevice.deviceType }}</strong></li>
            <li><span>Protocol</span><strong>{{ currentDevice.protocol }}</strong></li>
            <li><span>Port</span><strong>{{ currentDevice.port }}</strong></li>
            <li><span>Endpoint</span><strong>{{ currentDevice.address }}</strong></li>
          </ul>
        </article>
      </div>
    } @else {
      <p class="muted">Loading device overview…</p>
    }
  `,
  styles: [
    `:host { display: block; }
     .panel-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
     .panel { background: rgba(15, 23, 42, 0.75); border: 1px solid rgba(148, 163, 184, 0.2); border-radius: 16px; padding: 1rem 1.25rem; }
     h2 { margin: 0 0 0.75rem; color: white; }
     .muted, .info-list span, .metric-stack span { color: #b9c3d5; }
     .metric-stack { display: flex; flex-direction: column; gap: 0.9rem; margin-top: 1rem; }
     .metric-stack > div, .info-list li { border: 1px solid rgba(148, 163, 184, 0.15); border-radius: 10px; background: rgba(15, 23, 42, 0.35); padding: 0.8rem 0.9rem; }
     .info-list { list-style: none; padding: 0; margin: 1rem 0 0; display: flex; flex-direction: column; gap: 0.8rem; }
     .info-list li { display: flex; justify-content: space-between; gap: 0.8rem; }
     strong { color: white; }
     @media (max-width: 760px) { .panel-grid { grid-template-columns: 1fr; } }
    `
  ]
})
export class DeviceOverviewComponent {
  private readonly api = inject(TelemetryApiService);
  private readonly route = inject(ActivatedRoute);
  protected readonly device = signal<DeviceSummary | undefined>(undefined);
  protected readonly healthClass = computed(() => {
    const health = this.device()?.health ?? 0;
    if (health >= 85) return 'good';
    if (health >= 60) return 'warning';
    return 'bad';
  });

  constructor() {
    this.route.parent?.paramMap.subscribe((params) => {
      const deviceId = params.get('id');
      if (!deviceId) {
        return;
      }

      this.api.getDeviceById(deviceId).subscribe((device) => this.device.set(device));
    });
  }
}
