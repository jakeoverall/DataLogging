import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import type { DeviceLogEntry } from '../../models/device';

@Component({
  selector: 'app-device-logs-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './device-logs-list.html',
  styleUrl: './device-logs-list.scss'
})
export class DeviceLogsListComponent {
  @Input() entries: DeviceLogEntry[] = [];
  @Input() hasLogs = false;
  @Input() page = 0;
  @Input() maxPage = 0;

  @Output() readonly previous = new EventEmitter<void>();
  @Output() readonly next = new EventEmitter<void>();
}
