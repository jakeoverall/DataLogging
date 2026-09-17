import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { DeviceLogsToolbarComponent } from './device-logs-toolbar';
import { DeviceLogsListComponent } from './device-logs-list';
import {
  type ExportColumnKey,
  type ExportFormat
} from './device-logs.models';
import { DeviceLogsFacade } from './device-logs.facade';

@Component({
  selector: 'app-device-logs',
  standalone: true,
  providers: [DeviceLogsFacade],
  imports: [CommonModule, DeviceLogsToolbarComponent, DeviceLogsListComponent],
  templateUrl: './device-logs.html',
  styleUrl: './device-logs.scss'
})
export class DeviceLogsComponent {
  private readonly facade = inject(DeviceLogsFacade);

  protected readonly exportColumns = this.facade.exportColumns;
  protected readonly logs = this.facade.logs;
  protected readonly page = this.facade.page;
  protected readonly searchText = this.facade.searchText;
  protected readonly selectedColumns = this.facade.selectedColumns;
  protected readonly streamState = this.facade.streamState;
  protected readonly streamStateLabel = this.facade.streamStateLabel;
  protected readonly streamAgeLabel = this.facade.streamAgeLabel;
  protected readonly filteredLogs = this.facade.filteredLogs;
  protected readonly canExport = this.facade.canExport;
  protected readonly maxPage = this.facade.maxPage;
  protected readonly pagedLogs = this.facade.pagedLogs;

  protected onSearchChanged(searchText: string) {
    this.facade.onSearchChanged(searchText);
  }

  protected onColumnToggled(change: { column: ExportColumnKey; checked: boolean }) {
    this.facade.onColumnToggled(change);
  }

  protected exportLogs(format: ExportFormat) {
    this.facade.exportLogs(format);
  }

  protected previousPage() {
    this.facade.previousPage();
  }

  protected nextPage() {
    this.facade.nextPage();
  }
}
