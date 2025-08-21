using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
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

    public Task WriteBatch<TEvent>(IReadOnlyCollection<LogMessage<TEvent>> logMessages, FileLogSegment segment,
        CancellationToken cancellationToken = default) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public Task<PositionPair> Write<TEvent>(LogMessage<TEvent> logMessage, FileLogSegment segment,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        return WriteInternal(logMessage, segment, cancellationToken);

        // await using var fs = new FileStream(segment.FilePath, FileMode.OpenOrCreate, FileAccess.Write);
        // fs.Seek(0, SeekOrigin.End);
        //
        // var startPosition = fs.Position;
        // await JsonSerializer.SerializeAsync(fs, logMessage, SerializerOptions, cancellationToken);
        // await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);
        //
        // return new PositionPair(startPosition, fs.Position);
    }

    public async Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(FileLogSegment segment, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        await using var fs = new FileStream(segment.FilePath, FileMode.OpenOrCreate, FileAccess.Read);
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
        await using var fs = new FileStream(segment.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length == 0)
        {
            yield break;
        }

        // var lastPosition = _lastPosition.GetValueOrDefault(segment.PartitionId, 0);
        var counter = 0;

        // fs.Seek(lastPosition, SeekOrigin.Begin);
        // fs.Seek(startPosition - 1, SeekOrigin.Begin);
        fs.Seek(startPosition, SeekOrigin.Begin);

        using var reader = new StreamReader(fs);
        while (await reader.ReadLineAsync(cancellationToken) is { } line && counter < request.BatchSize)
        {
            var position = fs.Position;
            _lastPosition.AddOrUpdate(segment.PartitionId, position, (key, value) => position);

            var message = JsonSerializer.Deserialize<LogMessage<TEvent>>(line);

            ArgumentNullException.ThrowIfNull(message);

            counter++;

            yield return message;
        }
    }

    public Task<PositionPair> Commit(LogOffsetRequest request, FileLogSegment segment, CancellationToken cancellationToken = default)
    {
        var message = new LogOffsetMessage { Key = request.Key, Value = request.Value, Metadata = request.Metadata, OccuredAt = request.OccuredAt };
        
        return WriteInternal(message, segment, cancellationToken);

        // await using var fs = new FileStream(segment.FilePath, FileMode.OpenOrCreate, FileAccess.Write);
        // fs.Seek(0, SeekOrigin.End);
        //
        // var startPosition = fs.Position;
        // await JsonSerializer.SerializeAsync(fs, request, SerializerOptions, cancellationToken);
        // await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);
        //
        // return new PositionPair(startPosition, fs.Position);
    }

    public async Task<LogOffsetMessage> ReadSavedOffset(LogOffsetKey key, FileLogSegment segment, CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(segment.FilePath, FileMode.OpenOrCreate, FileAccess.Read);
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

        // fs.Seek(-1, SeekOrigin.End);
        // var readBuffer = new byte[1];
        // var writeBuffer = new Stack<byte>();
        //
        // while (fs.Position > 0)
        // {
        //     await fs.ReadExactlyAsync(readBuffer, 0, 1, cancellationToken);
        //
        //     if (readBuffer[0] == EventLogDivider)
        //     {
        //         using var ms = new MemoryStream(writeBuffer.ToArray());
        //         var logOffset = await JsonSerializer.DeserializeAsync<LogOffsetRequest>(ms, cancellationToken: cancellationToken);
        //
        //         if (logOffset!.Key == key)
        //         {
        //             return new ReadOffsetResult
        //             {
        //                 Key = key, Value = logOffset.Value, Metadata = logOffset.Metadata, OccuredAt = logOffset.OccuredAt
        //             };
        //         }
        //
        //         writeBuffer.Clear();
        //         continue;
        //     }
        //
        //     writeBuffer.Push(readBuffer[0]);
        //     fs.Seek(-2, SeekOrigin.Current);
        // }

        return LogOffsetMessage.CreateNew(key);
    }

    private async Task<PositionPair> WriteInternal<TPayload>(TPayload payload, FileLogSegment segment, CancellationToken cancellationToken)
        where TPayload : class
    {
        await using var fs = new FileStream(segment.FilePath, FileMode.OpenOrCreate, FileAccess.Write);
        fs.Seek(0, SeekOrigin.End);

        var startPosition = fs.Position;
        await JsonSerializer.SerializeAsync(fs, payload, SerializerOptions, cancellationToken);
        await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);

        return new PositionPair(startPosition, fs.Position);
    }
}