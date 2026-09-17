import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import type { ExportColumn, ExportColumnKey, ExportFormat } from './device-logs.models';

@Component({
  selector: 'app-device-logs-toolbar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './device-logs-toolbar.html',
  styleUrl: './device-logs-toolbar.scss'
})
export class DeviceLogsToolbarComponent {
  @Input() searchText = '';
  @Input() exportColumns: ExportColumn[] = [];
  @Input() selectedColumns: Record<ExportColumnKey, boolean> = {
    timestamp: true,
    level: true,
    source: true,
    dataType: true,
    deviceId: true,
    payloadText: true,
    rawJson: false
  };
  @Input() canExport = false;

  @Output() readonly searchChanged = new EventEmitter<string>();
  @Output() readonly columnToggled = new EventEmitter<{ column: ExportColumnKey; checked: boolean }>();
  @Output() readonly exportRequested = new EventEmitter<ExportFormat>();

  protected onSearchInput(event: Event) {
    const target = event.target as HTMLInputElement;
    this.searchChanged.emit(target.value ?? '');
  }

  protected isColumnSelected(column: ExportColumnKey): boolean {
    return this.selectedColumns[column] ?? false;
  }

  protected onColumnToggle(column: ExportColumnKey, event: Event) {
    const input = event.target as HTMLInputElement;
    this.columnToggled.emit({ column, checked: input.checked });
  }

  protected requestExport(format: ExportFormat) {
    this.exportRequested.emit(format);
  }
}
