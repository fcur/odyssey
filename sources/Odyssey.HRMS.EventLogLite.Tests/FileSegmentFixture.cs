using Odyssey.HRMS.EventLogLite.Entities;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class FileSegmentFixture : IAsyncLifetime
{
    private const string DirectoryPath = "TestEvent";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public string WorkingDirectory => Path.Combine(Environment.CurrentDirectory, DirectoryPath);
    public string GetFilePath(byte partition) => Path.Combine(Environment.CurrentDirectory, DirectoryPath, $"test-event.{partition}.log");
    public string GetOffsetsPath(byte partition) => Path.Combine(Environment.CurrentDirectory, DirectoryPath, $"__consumer_offsets.{partition}.log");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(WorkingDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        var files = Directory.GetFiles(WorkingDirectory);
        foreach (var path in files)
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async Task<long> WriteManyLines(int linesCount, string filePath, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
        for (var i = 0; i < linesCount; i++)
        {
            await fs.WriteAsync(new byte[] { 10 }, cancellationToken);
        }

        return fs.Position;
    }

    public long GetLinesCount(string filePath)
    {
        const byte newLine = 10;
        var count = 1L;

        using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        for (long i = 0; i < accessor.Capacity; i++)
        {
            var b = accessor.ReadByte(i);
            if (b != newLine)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public long GetBytesCount<TPayload>(TPayload payload)  where TPayload : class
    {
        var jsonString = JsonSerializer.Serialize(payload);
        var sizeInBytes = Encoding.UTF8.GetByteCount(jsonString);
        
        return sizeInBytes;
    }
}