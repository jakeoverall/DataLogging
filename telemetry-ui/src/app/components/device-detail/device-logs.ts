import { CommonModule } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { filter, map, switchMap } from 'rxjs';
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
        <div class="pager" aria-label="Log pagination">
          <button type="button" class="pager-button" (click)="previousPage()" [disabled]="page() === 0">Previous</button>
          <span>Page {{ page() + 1 }}</span>
          <button type="button" class="pager-button" (click)="nextPage()" [disabled]="page() >= maxPage()">Next</button>
        </div>

        <ul class="log-list">
          @for (entry of pagedLogs(); track entry.id) {
            <li>
              <div class="log-head">
                <span class="log-tag" [ngClass]="entry.level">{{ entry.level }}</span>
                <strong>{{ entry.source }}</strong>
                <time>{{ entry.timestamp | date: 'short' }}</time>
              </div>
              @if (entry.rawJson) {
                <pre class="raw-json"><code>{{ entry.rawJson }}</code></pre>
              }
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
     .pager { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; margin-bottom: 1rem; color: #e2e8f0; }
     .pager-button { background: rgba(59, 130, 246, 0.18); color: white; border: 1px solid rgba(96, 165, 250, 0.25); border-radius: 10px; padding: 0.45rem 0.8rem; cursor: pointer; }
     .pager-button[disabled] { opacity: 0.45; cursor: not-allowed; }
     .raw-json { margin: 0.65rem 0 0; padding: 0.75rem; border-radius: 10px; background: rgba(2, 6, 23, 0.7); color: #dbeafe; overflow-x: auto; border: 1px solid rgba(148, 163, 184, 0.15); }
     p { margin: 0; color: white; }
     strong { color: white; }
    `
  ]
})
export class DeviceLogsComponent {
  private readonly api = inject(TelemetryApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly pageSize = 5;
  protected readonly logs = signal<DeviceLogEntry[]>([]);
  protected readonly page = signal(0);

  protected readonly maxPage = computed(() => {
    const totalPages = Math.max(1, Math.ceil(this.logs().length / this.pageSize));
    return totalPages - 1;
  });

  protected readonly pagedLogs = computed(() => {
    const start = this.page() * this.pageSize;
    return this.logs().slice(start, start + this.pageSize);
  });

  constructor() {
    const destroyRef = inject(DestroyRef);

    this.route.parent?.paramMap
      .pipe(
        map((params) => params.get('id')),
        filter((deviceId): deviceId is string => Boolean(deviceId)),
        switchMap((deviceId) => this.api.getLogs(deviceId).pipe(
          map((items) => items.map((item) => ({
            ...item,
            rawJson: this.formatRawJson(item.payloadText)
          })))
        )),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((logs) => {
        this.logs.set(logs);
        this.page.set(0);
      });
  }

  protected previousPage() {
    this.page.update((current) => Math.max(0, current - 1));
  }

  protected nextPage() {
    this.page.update((current) => Math.min(this.maxPage(), current + 1));
  }

  private formatRawJson(payloadText: string): string | undefined {
    const trimmed = payloadText.trim();
    if (!trimmed || !trimmed.startsWith('{') && !trimmed.startsWith('[')) {
      return undefined;
    }

    try {
      const parsed = JSON.parse(trimmed);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return undefined;
    }
  }
}
