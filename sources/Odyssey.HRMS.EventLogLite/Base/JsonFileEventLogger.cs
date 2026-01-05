using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite.Base;

public record struct PositionPair(long Start, long Next);

public sealed class JsonFileEventLogger : IFileEventLogger
{
    // log divider symbol, equals to '\n'
    private const byte EventLogDivider = 10;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };
    private readonly ConcurrentDictionary<byte, long> _lastPosition = new();

    public async Task<PositionPair> WriteBatch<TEvent>(IReadOnlyCollection<LogMessage<TEvent>> logMessages, FileLogSegment segment,
        CancellationToken cancellationToken = default) where TEvent : class
    {
        await using var fs = new FileStream(segment.GetLogFilePath(), FileMode.OpenOrCreate, FileAccess.Write);
        fs.Seek(0, SeekOrigin.End);

        var startPosition = fs.Position;

        foreach (var payload in logMessages)
        {
            await JsonSerializer.SerializeAsync(fs, payload, SerializerOptions, cancellationToken);
            await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);
        }

        return new PositionPair(startPosition, fs.Position);
    }

    public async Task<PositionPair> Write<TEvent>(LogMessage<TEvent> logMessage, FileLogSegment segment,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        var position = await WriteInternal(logMessage, segment, cancellationToken);

        if (segment.IsEmpty())
        {
            segment = segment with { BaseTime = logMessage.Timestamp, Size = position.Next };
        }
        else
        {
            segment = segment with { Size = position.Next };
        }
        
        WriteIndexInternal(new LogIndex(logMessage.Offset, position.Start), EventFileType.IndexFile, segment, cancellationToken);
        WriteIndexInternal(new LogIndex(logMessage.Timestamp, position.Start), EventFileType.TimeIndexFile, segment, cancellationToken);
        return position;
    }

    public async Task<LogMessage<TEvent>?> ReadLast<TEvent>(FileLogSegment segment, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        await using var fs = new FileStream(segment.GetLogFilePath(), FileMode.OpenOrCreate, FileAccess.Read);
        if (fs.Length == 0)
        {
            return null;
        }

        fs.Seek(-1, SeekOrigin.End);
        var readBuffer = new byte[1];
        var writeBuffer = new Stack<byte>();

        while (fs.Position > 0)
        {
            await fs.ReadExactlyAsync(readBuffer, 0, 1, cancellationToken);
            if (readBuffer[0] == EventLogDivider && writeBuffer.Count > 0)
            {
                break;
            }

            writeBuffer.Push(readBuffer[0]);

            fs.Seek(-2, SeekOrigin.Current);
        }

        if (writeBuffer.Count == 0)
        {
            return null;
        }

        using var ms = new MemoryStream(writeBuffer.ToArray());
        var message = await JsonSerializer.DeserializeAsync<LogMessage<TEvent>>(ms, cancellationToken: cancellationToken);

        return message;
    }

    public async IAsyncEnumerable<LogMessage<TEvent>> Poll<TEvent>(PollRequest request, FileLogSegment segment, long startPosition,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where TEvent : class
    {
        await using var fs = new FileStream(segment.GetLogFilePath(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length == 0)
        {
            yield break;
        }

        // var lastPosition = _lastPosition.GetValueOrDefault(segment.PartitionId, 0);
        var counter = 0;

        // fs.Seek(lastPosition, SeekOrigin.Begin);
        fs.Seek(startPosition, SeekOrigin.Begin);

        using var reader = new StreamReader(fs);
        while (await reader.ReadLineAsync(cancellationToken) is { } line && counter < request.BatchSize)
        {
            var position = fs.Position;
            _lastPosition.AddOrUpdate(segment.Partition, position, (key, value) => position);

            var message = JsonSerializer.Deserialize<LogMessage<TEvent>>(line);

            ArgumentNullException.ThrowIfNull(message);

            counter++;

            yield return message;
        }
    }

    public async Task<PositionPair> Commit(LogOffsetRequest request, FileLogSegment segment, CancellationToken cancellationToken = default)
    {
        var occuredAt = DateTimeOffset.FromUnixTimeMilliseconds(request.Value.CommitTimestamp);
        var message = new LogOffsetMessage { Key = request.Key, Value = request.Value, Metadata = request.Metadata, OccuredAt = occuredAt };

        var position = await WriteInternal(message, segment, cancellationToken);
        
        if (segment.IsEmpty())
        {
            segment = segment with { BaseTime = request.Value.CommitTimestamp, Size = position.Next };
        }
        else
        {
            segment = segment with { Size = position.Next };
        }
        
        WriteIndexInternal(new LogIndex(request.Value.Offset, position.Start), EventFileType.IndexFile, segment, cancellationToken);
        WriteIndexInternal(new LogIndex(request.Value.CommitTimestamp, position.Start), EventFileType.TimeIndexFile, segment, cancellationToken);

        return position;
    }

    public async Task<LogOffsetMessage> ReadSavedOffset(LogOffsetKey key, FileLogSegment segment, CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(segment.GetLogFilePath(), FileMode.OpenOrCreate, FileAccess.Read);
        if (fs.Length == 0)
        {
            return LogOffsetMessage.CreateNew(key);
        }

        using var reader = new StreamReader(fs);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var logOffset = JsonSerializer.Deserialize<LogOffsetMessage>(line);

            ArgumentNullException.ThrowIfNull(logOffset);

            return logOffset;
        }

        return LogOffsetMessage.CreateNew(key);
    }

    private async Task<PositionPair> WriteInternal<TPayload>(TPayload payload, FileLogSegment segment, CancellationToken cancellationToken)
        where TPayload : class
    {
        var path = segment.GetLogFilePath();
        
        await using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
        fs.Seek(0, SeekOrigin.End);

        var startPosition = fs.Position;
        await JsonSerializer.SerializeAsync(fs, payload, SerializerOptions, cancellationToken);
        await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);

        return new PositionPair(startPosition, fs.Position);
    }

    private void WriteIndexInternal(LogIndex payload, EventFileType fileType, FileLogSegment segment, CancellationToken cancellationToken)
    {
        var path = segment.GetPath(fileType);

        var offset = Path.Exists(path)? new FileInfo(path).Length: 0;

        using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.OpenOrCreate, mapName: null, capacity: offset + 16);
        using var accessor = mmf.CreateViewAccessor(offset, 16);
        accessor.Write(0, payload.Index);
        accessor.Write(8, payload.Position);
    }
}


/// <param name="Index">time or offset</param>
/// <param name="Position">position in bytes</param>
public record struct LogIndex(long Index, long Position);