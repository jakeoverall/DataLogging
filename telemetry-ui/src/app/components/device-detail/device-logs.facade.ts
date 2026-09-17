import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { filter, map, merge, scan, switchMap } from 'rxjs';
import {
  TelemetryApiService,
  type DeviceLogStreamState,
  type DeviceLogStreamUpdate
} from '../../services/telemetry-api.service';
import type { DeviceLogEntry } from '../../models/device';
import { EXPORT_COLUMNS, type ExportColumnKey, type ExportFormat } from './device-logs.models';

@Injectable()
export class DeviceLogsFacade {
  private readonly api = inject(TelemetryApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly streamLimit = 20;
  private readonly pageSize = 5;

  readonly exportColumns = EXPORT_COLUMNS;
  readonly logs = signal<DeviceLogEntry[]>([]);
  readonly page = signal(0);
  readonly searchText = signal('');
  readonly currentDeviceId = signal('device');
  readonly lastEventAt = signal<number | null>(null);
  readonly nowTick = signal(Date.now());
  readonly selectedColumns = signal<Record<ExportColumnKey, boolean>>({
    timestamp: true,
    level: true,
    source: true,
    dataType: true,
    deviceId: true,
    payloadText: true,
    rawJson: false
  });
  readonly streamState = signal<DeviceLogStreamState>('connecting');

  readonly streamStateLabel = computed(() => {
    switch (this.streamState()) {
      case 'live':
        return 'Live';
      case 'idle':
        return 'Idle';
      case 'reconnecting':
        return 'Reconnecting';
      default:
        return 'Connecting';
    }
  });

  readonly streamAgeLabel = computed(() => {
    const tick = this.nowTick();
    const last = this.lastEventAt();

    if (this.streamState() === 'reconnecting') {
      return 'Trying to recover stream...';
    }

    if (this.streamState() === 'idle' && last !== null) {
      const ageMs = Math.max(0, tick - last);
      const seconds = Math.floor(ageMs / 1_000);
      if (seconds < 60) {
        return `Idle for ${seconds}s`;
      }

      const minutes = Math.floor(seconds / 60);
      return `Idle for ${minutes}m`;
    }

    if (last === null) {
      return 'No events received yet';
    }

    const ageMs = Math.max(0, tick - last);
    if (ageMs < 1_000) {
      return `${ageMs} ms ago`;
    }

    const seconds = Math.floor(ageMs / 1_000);
    if (seconds < 60) {
      return `${seconds} sec ago`;
    }

    const minutes = Math.floor(seconds / 60);
    return `${minutes} min ago`;
  });

  readonly filteredLogs = computed(() => {
    const query = this.searchText().trim().toLowerCase();
    if (!query) {
      return this.logs();
    }

    return this.logs().filter((entry) => {
      const sourceText = `${entry.timestamp} ${entry.level} ${entry.source} ${entry.dataType} ${entry.deviceId} ${entry.payloadText} ${entry.rawJson ?? ''}`.toLowerCase();
      return sourceText.includes(query);
    });
  });

  readonly activeExportColumns = computed(() =>
    this.exportColumns.filter((column) => this.selectedColumns()[column.key])
  );

  readonly canExport = computed(() =>
    this.filteredLogs().length > 0 && this.activeExportColumns().length > 0
  );

  readonly maxPage = computed(() => {
    const totalPages = Math.max(1, Math.ceil(this.filteredLogs().length / this.pageSize));
    return totalPages - 1;
  });

  readonly pagedLogs = computed(() => {
    const start = this.page() * this.pageSize;
    return this.filteredLogs().slice(start, start + this.pageSize);
  });

  constructor() {
    const ageInterval = setInterval(() => this.nowTick.set(Date.now()), 1_000);
    this.destroyRef.onDestroy(() => clearInterval(ageInterval));

    this.route.parent?.paramMap
      .pipe(
        map((params) => params.get('id')),
        filter((deviceId): deviceId is string => Boolean(deviceId)),
        map((deviceId) => {
          this.currentDeviceId.set(deviceId);
          this.streamState.set('connecting');
          this.lastEventAt.set(null);
          this.searchText.set('');
          this.page.set(0);
          return deviceId;
        }),
        switchMap((deviceId) => merge(
          this.api.getLogs(deviceId, this.streamLimit).pipe(
            map((entries) => ({ state: 'connecting', entries, idleAt: null } satisfies DeviceLogStreamUpdate))
          ),
          this.api.streamLogs(deviceId, this.streamLimit)
        ).pipe(
          scan((current, nextUpdate) => {
            const updated = new Map<string, DeviceLogEntry>();
            for (const entry of current.logs) {
              updated.set(entry.id, entry);
            }
            for (const entry of nextUpdate.entries) {
              updated.set(entry.id, entry);
            }

            const logs = [...updated.values()]
              .sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime())
              .slice(0, this.streamLimit)
              .map((item) => ({
                ...item,
                rawJson: this.formatRawJson(item.payloadText)
              }));

            return {
              state: nextUpdate.state,
              logs,
              batchCount: nextUpdate.entries.length,
              idleAt: nextUpdate.idleAt ?? null
            };
          }, { state: 'connecting' as DeviceLogStreamState, logs: [] as DeviceLogEntry[], batchCount: 0, idleAt: null as string | null })
        )),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((update) => {
        this.streamState.set(update.state);
        this.logs.set(update.logs);

        if (update.batchCount > 0) {
          this.lastEventAt.set(Date.now());
        } else if (update.idleAt) {
          this.lastEventAt.set(new Date(update.idleAt).getTime() || Date.now());
        }

        const currentPage = this.page();
        const max = Math.max(0, Math.ceil(this.filteredLogs().length / this.pageSize) - 1);
        if (currentPage > max) {
          this.page.set(max);
        }
      });
  }

  onSearchChanged(searchText: string) {
    this.searchText.set(searchText ?? '');
    this.page.set(0);
  }

  onColumnToggled(change: { column: ExportColumnKey; checked: boolean }) {
    this.selectedColumns.update((current) => ({
      ...current,
      [change.column]: change.checked
    }));
  }

  exportLogs(format: ExportFormat) {
    const columns = this.activeExportColumns();
    const rows = this.filteredLogs();
    if (columns.length === 0 || rows.length === 0) {
      return;
    }

    const delimiter = format === 'csv' ? ',' : '\t';
    const header = columns.map((column) => this.escapeCell(column.label, delimiter)).join(delimiter);
    const body = rows
      .map((row) =>
        columns
          .map((column) => this.escapeCell(this.toColumnValue(row, column.key), delimiter))
          .join(delimiter)
      )
      .join('\n');

    const content = `${header}\n${body}`;
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.currentDeviceId()}-logs.${format}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  previousPage() {
    this.page.update((current) => Math.max(0, current - 1));
  }

  nextPage() {
    this.page.update((current) => Math.min(this.maxPage(), current + 1));
  }

  private toColumnValue(entry: DeviceLogEntry, column: ExportColumnKey): string {
    switch (column) {
      case 'timestamp':
        return entry.timestamp;
      case 'level':
        return entry.level;
      case 'source':
        return entry.source;
      case 'dataType':
        return entry.dataType;
      case 'deviceId':
        return entry.deviceId;
      case 'payloadText':
        return entry.payloadText;
      case 'rawJson':
        return entry.rawJson ?? '';
      default:
        return '';
    }
  }

  private escapeCell(value: string, delimiter: string): string {
    const text = value ?? '';
    const needsQuotes = text.includes('"') || text.includes('\n') || text.includes('\r') || text.includes(delimiter);
    if (!needsQuotes) {
      return text;
    }

    return `"${text.replaceAll('"', '""')}"`;
  }

  private formatRawJson(payloadText: string): string | undefined {
    const trimmed = payloadText.trim();
    if (!trimmed || (!trimmed.startsWith('{') && !trimmed.startsWith('['))) {
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
