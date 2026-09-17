using DataLogging.Ingestion.Configuration;
using DataLogging.Ingestion.Models;
using DataLogging.Ingestion.Registry;
using Microsoft.Extensions.Options;
using Xunit;

namespace DataLogging.Ingestion.Tests;

public sealed class DeviceRegistrationStoreTests
{
    [Fact]
    public async Task InMemoryStore_UpsertAndLoad_Works()
    {
        var store = new InMemoryDeviceRegistrationStore();
        await store.UpsertAsync(new DeviceDefinition
        {
            DeviceId = "device-001",
            Name = "IMU",
            DeviceType = "IMU",
            Protocol = DeviceProtocol.Ros2
        });

        var devices = await store.LoadAsync();
        Assert.Single(devices);
        Assert.Equal("device-001", devices.Single().DeviceId);
    }

    [Fact]
    public async Task FileStore_UpsertAndRemove_Persists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"device-registry-{Guid.NewGuid():N}.json");
        try
        {
            var options = Options.Create(new DeviceRegistrationOptions
            {
                Provider = "file",
                FilePath = tempFile
            });
            var store = new FileDeviceRegistrationStore(options);
            var device = new DeviceDefinition
            {
                DeviceId = "device-002",
                Name = "Battery",
                DeviceType = "Battery Monitor",
                Protocol = DeviceProtocol.Ros2,
                Enabled = true
            };

            await store.UpsertAsync(device);
            var loaded = await store.LoadAsync();
            Assert.Single(loaded);
            Assert.Equal("device-002", loaded.Single().DeviceId);

            var removed = await store.RemoveAsync("device-002");
            Assert.True(removed);
            var afterRemove = await store.LoadAsync();
            Assert.Empty(afterRemove);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
