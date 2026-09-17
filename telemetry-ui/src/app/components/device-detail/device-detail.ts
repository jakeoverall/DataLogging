import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink, RouterOutlet } from '@angular/router';
import { TelemetryApiService } from '../../services/telemetry-api.service';
import type { DeviceSummary } from '../../models/device';

@Component({
  selector: 'app-device-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterOutlet],
  templateUrl: './device-detail.html',
  styleUrl: './device-detail.scss'
})
export class DeviceDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(TelemetryApiService);

  protected readonly device = signal<DeviceSummary | undefined>(undefined);

  protected readonly healthClass = computed(() => {
    const health = this.device()?.health ?? 0;
    if (health >= 85) return 'good';
    if (health >= 60) return 'warning';
    return 'bad';
  });

  protected readonly deviceId = computed(() => this.device()?.id ?? this.route.snapshot.paramMap.get('id') ?? '');

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const deviceId = params.get('id');
      if (!deviceId) {
        this.router.navigateByUrl('/dashboard');
        return;
      }

      this.api.getDeviceById(deviceId).subscribe((device) => this.device.set(device ?? undefined));
    });
  }
}
