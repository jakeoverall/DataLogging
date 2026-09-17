using System.Text.Json;
using DataLogging.Core.Models;
using DataLogging.Storage.Configuration;
using DataLogging.Storage.Writers;
using Microsoft.Extensions.Options;
using Xunit;

namespace DataLogging.Ingestion.Tests;

public sealed class StorageWriteBehaviorTests
{
    [Fact]
    public async Task NdjsonLogStore_WritesPersistedRecordsToDisk_AndReadsThemBack() 
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"datalogging-{Guid.NewGuid():N}.ndjson");
        try
        {
            var options = Options.Create(new StorageOptions
            {
                Provider = "file",
                FilePath = tempFile
            });

            var store = new NdjsonLogStore(options);

            var expected = new LogRecordEnvelope
            {
                Metadata = new LogRecordMetadata
                {
                    RecordId = Guid.NewGuid(),
                    VehicleId = "vehicle-42",
                    DeviceId = "device-001",
                    Source = "mock-ros2",
                    DataType = "imu",
                    Schema = "imu.v1",
                    RecordedTimestamp = DateTimeOffset.UtcNow,
                    SequenceNumber = 7,
                    Kind = LogRecordKind.Telemetry,
                    Priority = LogPriority.Normal
                },
                Payload = JsonSerializer.SerializeToUtf8Bytes(new { accelX = 1.23, accelY = 2.34 })
            };

            await store.WriteAsync(expected);

            var records = new List<LogRecordEnvelope>();
            await foreach (var entry in store.ReadAsync(new Core.Queries.LogQuery { DeviceId = "device-001" }))
            {
                records.Add(entry);
            }

            Assert.Single(records);
            Assert.Equal(expected.Metadata.RecordId, records[0].Metadata.RecordId);
            Assert.Equal(expected.Metadata.DeviceId, records[0].Metadata.DeviceId);
            Assert.Equal(expected.Metadata.Source, records[0].Metadata.Source);
            Assert.Equal(expected.Metadata.DataType, records[0].Metadata.DataType);
            Assert.Equal(expected.Metadata.SequenceNumber, records[0].Metadata.SequenceNumber);
            Assert.True(File.Exists(tempFile));
            var fileText = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("device-001", fileText, StringComparison.Ordinal);
            Assert.Contains("mock-ros2", fileText, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task Storage_Provider_Configuration_UsesFilePath_WhenEnabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"datalogging-config-{Guid.NewGuid():N}.ndjson");
        try
        {
            var options = new StorageOptions
            {
                Provider = "file",
                FilePath = path
            };

            var filePath = options.FilePath;

            Assert.Equal("file", options.Provider, ignoreCase: true);
            Assert.Equal(path, filePath);
            Assert.EndsWith(".ndjson", filePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task NdjsonLogStore_RotatesAndPrunes_WhenFileExceedsConfiguredSize()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"datalogging-rotation-{Guid.NewGuid():N}");
        var tempFile = Path.Combine(tempDirectory, "logs.ndjson");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var options = Options.Create(new StorageOptions
            {
                Provider = "file",
                FilePath = tempFile,
                MaxFileSizeBytes = 800,
                MaxRetainedFiles = 2
            });

            var store = new NdjsonLogStore(options);
            for (var index = 0; index < 8; index++)
            {
                var record = new LogRecordEnvelope
                {
                    Metadata = new LogRecordMetadata
                    {
                        RecordId = Guid.NewGuid(),
                        VehicleId = "vehicle-42",
                        DeviceId = "device-001",
                        Source = "mock-ros2",
                        DataType = "imu",
                        Schema = "imu.v1",
                        RecordedTimestamp = DateTimeOffset.UtcNow,
                        SequenceNumber = (ulong)index,
                        Kind = LogRecordKind.Telemetry,
                        Priority = LogPriority.Normal
                    },
                    Payload = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        index,
                        message = new string('x', 180)
                    })
                };

                await store.WriteAsync(record);
            }

            Assert.True(File.Exists(tempFile));

            var rotated = Directory.EnumerateFiles(tempDirectory, "logs.*.ndjson").ToArray();
            Assert.NotEmpty(rotated);
            Assert.True(rotated.Length <= 2, $"Expected at most 2 rotated files but found {rotated.Length}.");

            var currentLength = new FileInfo(tempFile).Length;
            Assert.True(currentLength <= 800, $"Expected active log file <= 800 bytes but found {currentLength}.");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
