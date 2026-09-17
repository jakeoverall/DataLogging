import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { ReplaySubject, of } from 'rxjs';
import {
  DeviceLogsFacade
} from './device-logs.facade';
import {
  TelemetryApiService,
  type DeviceLogStreamUpdate
} from '../../services/telemetry-api.service';
import type { DeviceLogEntry } from '../../models/device';

class TelemetryApiServiceStub {
  private readonly stream$ = new ReplaySubject<DeviceLogStreamUpdate>(10);

  getLogs() {
    return of([]);
  }

  streamLogs() {
    return this.stream$.asObservable();
  }

  emit(update: DeviceLogStreamUpdate) {
    this.stream$.next(update);
  }
}

describe('DeviceLogsFacade', () => {
  let facade: DeviceLogsFacade;
  let api: TelemetryApiServiceStub;
  let routeParams$: ReplaySubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(() => {
    routeParams$ = new ReplaySubject(1);

    TestBed.configureTestingModule({
      providers: [
        DeviceLogsFacade,
        {
          provide: TelemetryApiService,
          useClass: TelemetryApiServiceStub
        },
        {
          provide: ActivatedRoute,
          useValue: {
            parent: {
              paramMap: routeParams$.asObservable()
            }
          }
        }
      ]
    });

    api = TestBed.inject(TelemetryApiService) as unknown as TelemetryApiServiceStub;
    routeParams$.next(convertToParamMap({ id: 'turtlesim001' }));
    facade = TestBed.inject(DeviceLogsFacade);
  });

  it('filters logs using search text and keeps pagination bounded', () => {
    api.emit({
      state: 'live',
      entries: [
        createLog('a', 'ws-source', 'imu', '{"rpm":1234}', '2026-09-17T10:00:00.000Z'),
        createLog('b', 'ros-source', 'lidar', '{"distance":42}', '2026-09-17T10:00:01.000Z')
      ]
    });

    expect(facade.filteredLogs().length).toBe(2);

    facade.onSearchChanged('lidar');

    expect(facade.filteredLogs().length).toBe(1);
    expect(facade.pagedLogs().length).toBe(1);
    expect(facade.page()).toBe(0);

    facade.nextPage();
    expect(facade.page()).toBe(0);
  });

  it('exports selected columns in CSV with escaped values', () => {
    api.emit({
      state: 'live',
      entries: [
        createLog(
          'x1',
          'ws-source',
          'imu',
          'value,"quoted"\nline',
          '2026-09-17T10:00:05.000Z',
          'warn'
        )
      ]
    });

    facade.onColumnToggled({ column: 'rawJson', checked: true });

    const anchor = {
      href: '',
      download: '',
      click: vi.fn()
    } as unknown as HTMLAnchorElement;

    const createElementSpy = vi.spyOn(document, 'createElement').mockReturnValue(anchor);
    const appendSpy = vi.spyOn(document.body, 'appendChild').mockImplementation(() => anchor);
    const removeSpy = vi.spyOn(document.body, 'removeChild').mockImplementation(() => anchor);

    class FakeBlob {
      public readonly content: string;

      constructor(parts: BlobPart[]) {
        this.content = parts.map((part) => String(part)).join('');
      }
    }

    const blobCtorSpy = vi.spyOn(globalThis, 'Blob').mockImplementation(FakeBlob as unknown as typeof Blob);
    let exportedContent = '';
    const createObjectUrlSpy = vi.spyOn(URL, 'createObjectURL').mockImplementation((blob: Blob | MediaSource) => {
      exportedContent = (blob as unknown as { content?: string }).content ?? '';
      return 'blob:test';
    });
    const revokeObjectUrlSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});

    facade.exportLogs('csv');

    expect(anchor.download).toContain('turtlesim001-logs.csv');
    expect(anchor.click).toHaveBeenCalledTimes(1);

    expect(exportedContent).toContain('Timestamp,Level,Source,Data Type,Device ID,Payload Text,Raw JSON');
    expect(exportedContent).toContain('"value,""quoted""');

    blobCtorSpy.mockRestore();
    createElementSpy.mockRestore();
    appendSpy.mockRestore();
    removeSpy.mockRestore();
    createObjectUrlSpy.mockRestore();
    revokeObjectUrlSpy.mockRestore();
  });
});

function createLog(
  id: string,
  source: string,
  dataType: string,
  payloadText: string,
  timestamp: string,
  level: 'info' | 'warn' | 'error' = 'info'
): DeviceLogEntry {
  return {
    id,
    deviceId: 'turtlesim001',
    source,
    dataType,
    timestamp,
    level,
    payloadText
  };
}
