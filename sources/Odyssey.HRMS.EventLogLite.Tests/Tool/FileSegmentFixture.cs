using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Buffers;
using System.Buffers.Text;
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

    public int SaveBuffer(string path, ArrayBufferWriter<byte> sharedBuffer)
    {
        const int capacity = 1 * 1024 * 1024; // 1Mb
        
        var currentFileOffset = 0;
        var dataToWrite = sharedBuffer.WrittenSpan;
        var length = dataToWrite.Length;
        
        using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Create, null, capacity);
        using var accessor = mmf.CreateViewAccessor(offset:0, size: length, access: MemoryMappedFileAccess.ReadWrite);
        
        unsafe
        {
            byte* ptr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            try
            {
                var destSpan = new Span<byte>(ptr + accessor.PointerOffset + currentFileOffset, length);
                dataToWrite.CopyTo(destSpan);
            }
            finally
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                sharedBuffer.Clear();
            }
        }

        currentFileOffset += length;
        return length;
    }

    public async IAsyncEnumerable<TResult> ReadFile<TResult>(string path)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length == 0)
        {
            yield break;
        }
        fs.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);
        
        while (await reader.ReadLineAsync() is { } line)
        {
            var message = JsonSerializer.Deserialize<TResult>(line);
            ArgumentNullException.ThrowIfNull(message);

            yield return message;
        }
    }

    public IEnumerable<LogMessageBatch> ReadHeaders(string path)
    {
        var fileLength = new FileInfo(path).Length;
        const int headersSize = 351;

        using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, fileLength, MemoryMappedFileAccess.Read);
        using var accessor = mmf.CreateViewAccessor(0, fileLength, MemoryMappedFileAccess.Read);

        var offset = 0L;

        while (offset < fileLength)
        {
            var item = ParseHeader(accessor, offset, fileLength,headersSize);
            if (item == null)
            {
                yield break;
            }
            
            yield return item;
            offset += item.BatchLength;
        }
    }
    
    
    public void CutAndCloseSegment(string path, int length)
    {
        // release mmf during testing, do not use in production
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        // close segment
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        fs.SetLength(length);
    }

    private unsafe LogMessageBatch? ParseHeader(MemoryMappedViewAccessor accessor, long offset, long fileLength, long headersLength)
    {
        const int baseOffsetOffset = 15;
        const int batchLengthOffset = 52;
        const int versionOffset = 76;
        const int checksumOffset = 92;
        const int attributesOffset = 118;
        const int lastOffsetDeltaOffset = 142;
        const int firstTimestampOffset = 182;   
        const int maxTimestampOffset = 220;
        const int producerIdOffset = 256;
        const int producerEpochOffset = 295;
        const int batchOrderOffset = 314;
        const int itemsCountOffset = 340;
        
        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

        try
        {
            var blockSize = (int)Math.Min(headersLength, fileLength - offset);
            if (blockSize < headersLength)
            {
                return null;
            }
            
            var fileSpan = new ReadOnlySpan<byte>(ptr + offset, blockSize);

            var baseOffset = ParseInt64(fileSpan.Slice(baseOffsetOffset, 20));
            var batchLength = ParseInt32(fileSpan.Slice(batchLengthOffset, 10));
            var version = ParseByte(fileSpan.Slice(versionOffset, 3));
            var checksum = ParseInt32(fileSpan.Slice(checksumOffset, 10));
            var attributes = ParseByte(fileSpan.Slice(attributesOffset, 3));
            var lastOffsetDelta = ParseInt64(fileSpan.Slice(lastOffsetDeltaOffset, 20));
            var firstTimestamp = ParseInt64(fileSpan.Slice(firstTimestampOffset, 20));
            var maxTimestamp = ParseInt64(fileSpan.Slice(maxTimestampOffset, 20));
            var producerId = ParseInt64(fileSpan.Slice(producerIdOffset, 20));
            var producerEpoch = ParseByte(fileSpan.Slice(producerEpochOffset, 3));
            var batchOrder = ParseInt32(fileSpan.Slice(batchOrderOffset, 10));
            var itemCount = ParseInt32(fileSpan.Slice(itemsCountOffset, 10));
            
            var headerItem = new LogMessageBatch
            {
                BaseOffset = baseOffset, BatchLength = batchLength, Version = version, Checksum = checksum, Attributes = attributes,
                LastOffsetDelta = lastOffsetDelta, FirstTimestamp = firstTimestamp, MaxTimestamp = maxTimestamp,
                ProducerId = producerId, ProducerEpoch = producerEpoch, BatchOrder =  batchOrder, ItemsCount =  itemCount
            };

            return headerItem;
        }
        finally
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }

        int ParseInt32(ReadOnlySpan<byte> source)
        {
            return Utf8Parser.TryParse(source, out int value, out _) ? value : 0;
        }
        
        long ParseInt64(ReadOnlySpan<byte> source)
        {
            return Utf8Parser.TryParse(source, out long value, out _) ? value : 0;
        }
        
        byte ParseByte(ReadOnlySpan<byte> source)
        {
            return Utf8Parser.TryParse(source, out byte value, out _) ? value : byte.MinValue;
        }
    }
}