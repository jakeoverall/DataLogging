import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard';
import { DeviceDetailComponent } from './components/device-detail/device-detail';
import { DeviceLogsComponent } from './components/device-detail/device-logs';
import { DeviceOverviewComponent } from './components/device-detail/device-overview';
import { DeviceSettingsComponent } from './components/device-detail/device-settings';
import { SystemLogsComponent } from './components/system-logs/system-logs';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', component: DashboardComponent },
  { path: 'logs', component: SystemLogsComponent },
  {
    path: 'devices/:id',
    component: DeviceDetailComponent,
    children: [
      { path: '', redirectTo: 'overview', pathMatch: 'full' },
      { path: 'overview', component: DeviceOverviewComponent },
      { path: 'logs', component: DeviceLogsComponent },
      { path: 'settings', component: DeviceSettingsComponent }
    ]
  }
];
