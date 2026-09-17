using System.Text.Json;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Configuration;
using DataLogging.Ingestion.Models;
using Microsoft.Extensions.Options;

namespace DataLogging.Ingestion.Registry;

public sealed class FileDeviceRegistrationStore : IDeviceRegistrationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly DeviceRegistrationOptions _options;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public FileDeviceRegistrationStore(IOptions<DeviceRegistrationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<DeviceDefinition>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_options.FilePath))
            {
                return [];
            }

            await using var stream = File.OpenRead(_options.FilePath);
            var devices = await JsonSerializer.DeserializeAsync<DeviceDefinition[]>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);

            return devices?.OrderBy(device => device.DeviceId).ToArray() ?? [];
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task UpsertAsync(DeviceDefinition device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            current[device.DeviceId] = device;
            await WriteUnsafeAsync(current.Values.OrderBy(item => item.DeviceId), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<bool> RemoveAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var removed = current.Remove(deviceId);
            if (removed)
            {
                await WriteUnsafeAsync(current.Values.OrderBy(item => item.DeviceId), cancellationToken).ConfigureAwait(false);
            }

            return removed;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<Dictionary<string, DeviceDefinition>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.FilePath))
        {
            return new Dictionary<string, DeviceDefinition>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(_options.FilePath);
        var items = await JsonSerializer.DeserializeAsync<DeviceDefinition[]>(
            stream,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);

        return items?.ToDictionary(item => item.DeviceId, StringComparer.Ordinal)
            ?? new Dictionary<string, DeviceDefinition>(StringComparer.Ordinal);
    }

    private async Task WriteUnsafeAsync(IEnumerable<DeviceDefinition> devices, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_options.FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_options.FilePath);
        await JsonSerializer.SerializeAsync(stream, devices, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
