import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import type { DeviceLogEntry } from '../../models/device';
import type { SystemLogFileDescriptor } from '../../models/system-logs';
import { EXPORT_COLUMNS, type ExportColumnKey, type ExportFormat } from '../device-detail/device-logs.models';
import { buildDelimitedLogExport, getLogParsedJsonSource } from '../../utils/log-export';

@Component({
  selector: 'app-system-logs',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './system-logs.html',
  styleUrl: './system-logs.scss'
})
export class SystemLogsComponent {
  private readonly api = inject(TelemetryApiService);
  private readonly pageSize = 25;

  protected readonly exportColumns = EXPORT_COLUMNS;
  protected readonly logs = signal<DeviceLogEntry[]>([]);
  protected readonly files = signal<SystemLogFileDescriptor[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly isLoadingFiles = signal(false);
  protected readonly searchText = signal('');
  protected readonly page = signal(0);
  protected readonly activeTab = signal<'records' | 'files'>('records');
  protected readonly expandParsedJsonColumns = signal(false);
  protected readonly selectedColumns = signal<Record<ExportColumnKey, boolean>>({
    timestamp: true,
    level: true,
    source: true,
    dataType: true,
    deviceId: true,
    payloadText: true,
    rawJson: false
  });

  protected readonly filteredLogs = computed(() => {
    const query = this.searchText().trim().toLowerCase();
    if (!query) {
      return this.logs();
    }

    return this.logs().filter((entry) => {
      const sourceText = `${entry.timestamp} ${entry.level} ${entry.source} ${entry.dataType} ${entry.deviceId} ${entry.payloadText} ${entry.rawJson ?? ''}`.toLowerCase();
      return sourceText.includes(query);
    });
  });

  protected readonly activeExportColumns = computed(() =>
    this.exportColumns.filter((column) => this.selectedColumns()[column.key])
  );

  protected readonly canExport = computed(() =>
    this.filteredLogs().length > 0 && this.activeExportColumns().length > 0
  );

  protected readonly maxPage = computed(() => {
    const totalPages = Math.max(1, Math.ceil(this.filteredLogs().length / this.pageSize));
    return totalPages - 1;
  });

  protected readonly pagedLogs = computed(() => {
    const start = this.page() * this.pageSize;
    return this.filteredLogs().slice(start, start + this.pageSize);
  });

  protected readonly distinctDeviceCount = computed(() =>
    new Set(this.filteredLogs().map((entry) => entry.deviceId)).size
  );

  protected readonly filteredFiles = computed(() => {
    const query = this.searchText().trim().toLowerCase();
    if (!query) {
      return this.files();
    }

    return this.files().filter((file) => {
      const text = `${file.name} ${file.path} ${file.sizeBytes} ${file.lastModifiedUtc} ${file.isCurrent ? 'current' : 'rotated'}`.toLowerCase();
      return text.includes(query);
    });
  });

  constructor() {
    this.refresh();
  }

  protected refresh() {
    this.isLoading.set(true);
    this.isLoadingFiles.set(true);
    this.api.getSystemLogs(1000).subscribe({
      next: (logs) => {
        const normalized = logs
          .map((item) => ({
            ...item,
            rawJson: this.formatRawJson(item.payloadText)
          }))
          .sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime());

        this.logs.set(normalized);
        this.page.set(0);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });

    this.api.getSystemLogFiles().subscribe({
      next: (files) => {
        this.files.set(files);
        this.isLoadingFiles.set(false);
      },
      error: () => {
        this.isLoadingFiles.set(false);
      }
    });
  }

  protected setActiveTab(tab: 'records' | 'files') {
    this.activeTab.set(tab);
    this.searchText.set('');
    this.page.set(0);
  }

  protected onSearchInput(event: Event) {
    const target = event.target as HTMLInputElement;
    this.searchText.set(target.value ?? '');
    this.page.set(0);
  }

  protected onColumnToggle(column: ExportColumnKey, event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedColumns.update((current) => ({
      ...current,
      [column]: input.checked
    }));
  }

  protected onExpandParsedJsonColumnsToggled(event: Event) {
    const input = event.target as HTMLInputElement;
    this.expandParsedJsonColumns.set(input.checked);
  }

  protected isColumnSelected(column: ExportColumnKey): boolean {
    return this.selectedColumns()[column] ?? false;
  }

  protected exportLogs(format: ExportFormat) {
    const columns = this.activeExportColumns();
    const rows = this.filteredLogs();
    if (columns.length === 0 || rows.length === 0) {
      return;
    }

    const content = buildDelimitedLogExport(rows, columns, format, {
      expandParsedJsonColumns: format === 'csv' && this.expandParsedJsonColumns(),
      getColumnValue: (entry, column) => this.toColumnValue(entry, column),
      getParsedJsonSource: (entry) => getLogParsedJsonSource(entry)
    });
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `system-logs.${format}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  protected fileSummary(file: SystemLogFileDescriptor): string {
    const sizeKb = Math.max(0, Math.round(file.sizeBytes / 102.4) / 10);
    return `${sizeKb} KB · ${file.isCurrent ? 'current' : 'rotated'}`;
  }

  protected previousPage() {
    this.page.update((current) => Math.max(0, current - 1));
  }

  protected nextPage() {
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
