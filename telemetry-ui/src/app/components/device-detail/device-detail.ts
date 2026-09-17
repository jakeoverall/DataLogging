import { CommonModule } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter, map, switchMap } from 'rxjs';
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
    const destroyRef = inject(DestroyRef);

    this.route.paramMap
      .pipe(
        map((params) => params.get('id')),
        filter((deviceId): deviceId is string => Boolean(deviceId)),
        switchMap((deviceId) => this.api.streamDevices().pipe(
          map((devices) => devices.find((entry) => entry.id === deviceId || entry.deviceId === deviceId) ?? undefined)
        )),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((device) => this.device.set(device));

    this.route.paramMap
      .pipe(
        map((params) => params.get('id')),
        filter((deviceId): deviceId is string => !deviceId),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe(() => this.router.navigateByUrl('/dashboard'));
  }
}
