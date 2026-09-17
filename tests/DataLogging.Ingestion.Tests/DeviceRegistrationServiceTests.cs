using DataLogging.Ingestion.Models;
using DataLogging.Ingestion.Registry;
using Xunit;

namespace DataLogging.Ingestion.Tests;

public sealed class DeviceRegistrationServiceTests
{
    [Fact]
    public async Task Initialize_LoadsDevicesIntoRegistry()
    {
        var registry = new DeviceRegistry();
        var store = new InMemoryDeviceRegistrationStore();
        await store.UpsertAsync(new DeviceDefinition
        {
            DeviceId = "device-003",
            Name = "Safety Scanner",
            DeviceType = "Safety Scanner",
            Protocol = DeviceProtocol.Ethernet
        });

        var service = new DeviceRegistrationService(registry, store);
        await service.InitializeAsync();

        var allDevices = service.GetAll();
        Assert.Single(allDevices);
        Assert.Equal("device-003", allDevices.Single().DeviceId);
    }
}
