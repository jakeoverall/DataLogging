import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import type { DeviceLogEntry } from '../../models/device';

@Component({
  selector: 'app-device-logs',
  standalone: true,
  imports: [CommonModule],
  template: `
    <article class="panel">
      <h2>Recent logs</h2>
      @if (logs().length) {
        <ul class="log-list">
          @for (entry of logs(); track entry.id) {
            <li>
              <div class="log-head">
                <span class="log-tag" [ngClass]="entry.level">{{ entry.level }}</span>
                <strong>{{ entry.source }}</strong>
                <time>{{ entry.timestamp | date: 'short' }}</time>
              </div>
              <p>{{ entry.payloadText }}</p>
            </li>
          }
        </ul>
      } @else {
        <p class="muted">No logs available for this device yet.</p>
      }
    </article>
  `,
  styles: [
    `:host { display: block; }
     .panel { background: rgba(15, 23, 42, 0.75); border: 1px solid rgba(148, 163, 184, 0.2); border-radius: 16px; padding: 1rem 1.25rem; }
     h2 { margin: 0 0 1rem; color: white; }
     .log-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.8rem; }
     .log-list li { display: flex; flex-direction: column; gap: 0.55rem; border: 1px solid rgba(148, 163, 184, 0.15); border-radius: 10px; background: rgba(15, 23, 42, 0.35); padding: 0.8rem 0.9rem; }
     .log-head { display: flex; justify-content: space-between; gap: 0.8rem; align-items: center; }
     .muted, time { color: #b9c3d5; }
     .log-tag { display: inline-flex; align-items: center; justify-content: center; border-radius: 999px; padding: 0.2rem 0.5rem; font-size: 0.72rem; font-weight: 700; text-transform: uppercase; }
     .log-tag.info { background: rgba(59, 130, 246, 0.18); color: #93c5fd; }
     .log-tag.warn { background: rgba(251, 191, 36, 0.18); color: #fcd34d; }
     .log-tag.error { background: rgba(248, 113, 113, 0.18); color: #fca5a5; }
     p { margin: 0; color: white; }
     strong { color: white; }
    `
  ]
})
export class DeviceLogsComponent {
  private readonly api = inject(TelemetryApiService);
  private readonly route = inject(ActivatedRoute);
  protected readonly logs = signal<DeviceLogEntry[]>([]);

  constructor() {
    this.route.parent?.paramMap.subscribe((params) => {
      const deviceId = params.get('id');
      if (!deviceId) {
        return;
      }

      this.api.getLogs(deviceId).subscribe((logs) => this.logs.set(logs));
    });
  }
}
