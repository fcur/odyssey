using Odyssey.HRMS.EventLogLite.Base;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite.Tests.Tool;

[ExcludeFromCodeCoverage]
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class FileSegmentFixture : IAsyncLifetime
{
    private const string BaseDirectoryRoot = "../../../../../JsonFileEventLoggerTests";
    private const string TopicName = "TestEvent";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    static FileSegmentFixture()
    {
        LogSegmentDirectory.SetEventLoggingRoot(BaseDirectoryRoot);
    }
    
    public string CreateSegmentRoot(byte partition)
    {
        var topicRoot = Path.Combine(Path.GetFullPath(BaseDirectoryRoot), TopicName);
        Directory.CreateDirectory(Path.Combine(topicRoot, partition.ToString()));
        return topicRoot;
    }
    
    public Task InitializeAsync()
    {
        var workingDirectory = Path.GetFullPath(BaseDirectoryRoot);
        Directory.CreateDirectory(workingDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        var workingDirectory = Path.GetFullPath(BaseDirectoryRoot);
        Directory.Delete(workingDirectory, true);
        return Task.CompletedTask;
    }

    public async Task<long> WriteManyLines(int linesCount, string filePath, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
        for (var i = 1; i < linesCount; i++)
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