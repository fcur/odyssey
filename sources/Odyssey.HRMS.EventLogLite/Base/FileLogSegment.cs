using Odyssey.HRMS.EventLogLite.Entities;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventLogger<in TSegment> where TSegment : LogSegment
{
    Task Write<TEvent>(LogMessage<TEvent> logMessage, TSegment segment, CancellationToken cancellationToken = default) where TEvent : class;

    Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(TSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
    
    IAsyncEnumerable<LogMessage<TEvent>> Poll<TEvent>(PollRequest request, FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;

    Task Commit(LogOffsetRequest request, FileLogSegment segment, CancellationToken cancellationToken = default);
}

public interface IFileEventLogger : IEventLogger<FileLogSegment>
{
    new Task Write<TEvent>(LogMessage<TEvent> logMessage, FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;

    new Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;

    new IAsyncEnumerable<LogMessage<TEvent>> Poll<TEvent>(PollRequest request, FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;

    new Task Commit(LogOffsetRequest request, FileLogSegment segment, CancellationToken cancellationToken = default);
}

public sealed class JsonFileEventLogger : IFileEventLogger
{
    // log divider symbol, equals to '\n'
    private const byte EventLogDivider = 10;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };
    private readonly ConcurrentDictionary<byte, long> _lastPosition = new();
    
    public async Task Write<TEvent>(LogMessage<TEvent> logMessage, FileLogSegment segment, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        await using var fs = new FileStream(segment.FilePath, FileMode.OpenOrCreate, FileAccess.Write);
        fs.Seek(0, SeekOrigin.End);

        await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);
        await JsonSerializer.SerializeAsync(fs, logMessage, SerializerOptions, cancellationToken);
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
            if (readBuffer[0] == EventLogDivider)
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

    public async IAsyncEnumerable<LogMessage<TEvent>> Poll<TEvent>(PollRequest request, FileLogSegment segment, [EnumeratorCancellation] CancellationToken cancellationToken = default) where TEvent : class
    {
        await using var fs = new FileStream(segment.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length == 0)
        {
            yield break;
        }

        var lastPosition = _lastPosition.GetValueOrDefault(segment.PartitionId, 0);
        var counter = 0;

        fs.Seek(lastPosition, SeekOrigin.Begin);

        using var reader = new StreamReader(fs);
        while (await reader.ReadLineAsync(cancellationToken) is { } line && counter < request.BatchSize)
        {
            var position = fs.Position;
            _lastPosition.AddOrUpdate(segment.PartitionId, position, (key, value) => position);
            
            var message =  JsonSerializer.Deserialize<LogMessage<TEvent>>(line);
            
            ArgumentNullException.ThrowIfNull(message);
            
            counter++;
            
            yield return message;
        }
    }

    public async Task Commit(LogOffsetRequest request, FileLogSegment segment, CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(segment.FilePath, FileMode.OpenOrCreate, FileAccess.Write);
        fs.Seek(0, SeekOrigin.End);
        await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);
        await JsonSerializer.SerializeAsync(fs, request, SerializerOptions, cancellationToken);
    }
}

public class FileLogSegment(byte partitionId, string filePath) : LogSegment(partitionId)
{
    public string FilePath => filePath;
    private const string EventLogFileExtension = ".log";

    public static Dictionary<byte, FileLogSegment> MapPartitionsWithSegments(EventLogTopic topic)
    {
        var workingDirectory = Path.Combine(Environment.CurrentDirectory, topic.Value);
        var partitionsCount = topic.Partitions;

        var logSegments = GetOrCreateLogSegments(workingDirectory, partitionsCount);

        var segmentsMap = Enumerable.Range(0, logSegments.Length).Zip(logSegments, (k, v) => new { key = (byte)k, val = v })
            .ToDictionary(v => v.key, v => new FileLogSegment(v.key, v.val));

        return segmentsMap;
    }

    public static void InitWorkingDirectory(string topicName)
    {
        var workingDirectory = Path.Combine(Environment.CurrentDirectory, topicName);
        Directory.CreateDirectory(workingDirectory);
    }

    private static string[] GetOrCreateLogSegments(string workingDirectory, byte partitionsCount)
    {
        var logSegments = new List<string>(GetLogSegments(workingDirectory));
        var newFilesCount = Math.Max(partitionsCount, logSegments.Count) - logSegments.Count;
        if (newFilesCount == 0)
        {
            return logSegments.ToArray();
        }

        var fileNames = logSegments.Select(Path.GetFileNameWithoutExtension).Select(v => int.Parse(v!)).ToArray();
        var maxSegment = fileNames.Length > 0 ? fileNames.Max() : -1;
        var newFiles = Enumerable.Range(maxSegment + 1, newFilesCount).Select(v => FileLogSegment.PreparePath(workingDirectory, v)).ToArray();
        logSegments.AddRange(newFiles);

        return logSegments.ToArray();
    }

    private static string[] GetLogSegments(string workingDirectory) => Directory.GetFiles(workingDirectory, $"*{EventLogFileExtension}");

    private static string PreparePath(string workingDirectory, int fileIndex) =>
        Path.Combine(workingDirectory, $"{fileIndex}{EventLogFileExtension}");
}