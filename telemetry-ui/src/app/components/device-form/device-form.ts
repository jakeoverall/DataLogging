import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface DeviceFormValue {
  deviceId: string;
  name: string;
  deviceType: string;
  protocol: string;
  address: string;
  port: number;
}

@Component({
  selector: 'app-device-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <form class="device-form" (ngSubmit)="submit()">
      <h3>{{ title }}</h3>

      @if (showDeviceId) {
      <label>
        <span>Device ID (optional)</span>
        <input [(ngModel)]="model.deviceId" name="deviceId" placeholder="e.g. turtle-sim" />
      </label>
      }

      <label>
        <span>Name</span>
        <input [(ngModel)]="model.name" name="name" placeholder="e.g. Nav Camera" />
      </label>
      <label>
        <span>Type</span>
        <input [(ngModel)]="model.deviceType" name="deviceType" placeholder="e.g. CAN Gateway or ROS Bridge" />
      </label>
      <label>
        <span>Protocol</span>
        <select [(ngModel)]="model.protocol" name="protocol">
          @for (option of protocolOptions; track option) {
            <option [value]="option">{{ option }}</option>
          }
        </select>
      </label>
      <label>
        <span>Address</span>
        <input [(ngModel)]="model.address" name="address" placeholder="10.12.0.90" />
      </label>
      <label>
        <span>Port</span>
        <input type="number" [(ngModel)]="model.port" name="port" min="1" max="65535" />
      </label>

      <div class="form-actions">
        @if (showCancel) {
          <button type="button" class="ghost-button" (click)="cancel.emit()">Cancel</button>
        }
        <button type="submit" class="primary-button">{{ submitLabel }}</button>
      </div>
    </form>
  `,
  styles: [
    `:host { display: block; }
     .device-form { display: flex; flex-direction: column; gap: 0.8rem; }
     .device-form h3 { margin: 0; color: white; }
     .device-form label { display: flex; flex-direction: column; gap: 0.4rem; color: #dbeafe; }
     .device-form input, .device-form select {
       background: rgba(15, 23, 42, 0.4);
       border: 1px solid rgba(148, 163, 184, 0.2);
       border-radius: 10px;
       color: white;
       padding: 0.7rem 0.8rem;
     }
     .device-form select {
       appearance: none;
       padding-right: 2rem;
       background-image:
         linear-gradient(45deg, transparent 50%, #93c5fd 50%),
         linear-gradient(135deg, #93c5fd 50%, transparent 50%);
       background-position:
         calc(100% - 14px) calc(50% - 2px),
         calc(100% - 9px) calc(50% - 2px);
       background-size: 5px 5px, 5px 5px;
       background-repeat: no-repeat;
     }
     .form-actions { display: flex; justify-content: flex-end; align-items: center; gap: 0.6rem; }
     .ghost-button {
       border: 1px solid rgba(148, 163, 184, 0.3);
       border-radius: 10px;
       padding: 0.75rem 1rem;
       background: rgba(30, 41, 59, 0.65);
       color: #dbeafe;
       font-weight: 600;
       cursor: pointer;
     }
     .primary-button {
       border: none;
       border-radius: 10px;
       padding: 0.75rem 1rem;
       background: linear-gradient(135deg, #38bdf8, #8b5cf6);
       color: white;
       font-weight: 600;
       cursor: pointer;
     }
    `
  ]
})
export class DeviceFormComponent implements OnChanges {
  @Input() title = 'Register device';
  @Input() submitLabel = 'Add device';
  @Input() showDeviceId = false;
  @Input() showCancel = true;
  @Input() protocolOptions: string[] = [];
  @Input() initialValue: DeviceFormValue = {
    deviceId: '',
    name: '',
    deviceType: 'Custom gateway',
    protocol: 'Ethernet',
    address: '10.12.0.90',
    port: 9000
  };

  @Output() readonly formSubmit = new EventEmitter<DeviceFormValue>();
  @Output() readonly cancel = new EventEmitter<void>();

  protected model: DeviceFormValue = {
    deviceId: '',
    name: '',
    deviceType: 'Custom gateway',
    protocol: 'Ethernet',
    address: '10.12.0.90',
    port: 9000
  };

  ngOnChanges() {
    this.model = {
      deviceId: this.initialValue.deviceId ?? '',
      name: this.initialValue.name ?? '',
      deviceType: this.initialValue.deviceType ?? 'Custom gateway',
      protocol: this.initialValue.protocol ?? 'Ethernet',
      address: this.initialValue.address ?? '10.12.0.90',
      port: Number(this.initialValue.port) || 9000
    };
  }

  protected submit() {
    this.formSubmit.emit({
      deviceId: this.model.deviceId.trim(),
      name: this.model.name,
      deviceType: this.model.deviceType,
      protocol: this.model.protocol,
      address: this.model.address,
      port: Number(this.model.port) || 9000
    });
  }
}
