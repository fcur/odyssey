using Odyssey.HRMS.EventLogLite.Entities;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventBroker: IEventLogLite
{
}

public interface IEventBroker<TEvent>: IEventBroker where TEvent : class
{
    Task<EventLogResult> LogEvent(LogRequest<TEvent> request, CancellationToken cancellationToken = default);

    void Join(IEventConsumer<TEvent> consumer, CancellationToken cancellationToken = default);
}


public sealed record EventLogResult(string TopicName, byte PartitionId, ulong Offset);

public sealed record EventLogTopic(string Value, byte Partitions);



public abstract class LogSegment(byte partitionId)
{
    public byte PartitionId => partitionId;

    public abstract Task Write<TEvent>(LogMessage<TEvent> logMessage, CancellationToken cancellationToken = default) where TEvent : class;

    public abstract Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(CancellationToken cancellationToken = default) where TEvent : class;
    
}

public sealed class FileLogSegment(byte partitionId, string filePath) : LogSegment(partitionId)
{
    // log divider symbol, equals to '\n'
    private const byte EventLogDivider = 10;
    private const string EventLogFileExtension = ".log";
    
    public string FilePath => filePath;
    public override async Task Write<TEvent>(LogMessage<TEvent> logMessage, CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
        fs.Seek(0, SeekOrigin.End);

        await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);
        await JsonSerializer.SerializeAsync(fs, logMessage, new JsonSerializerOptions { WriteIndented = false }, cancellationToken);
    }

    public override async Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(CancellationToken cancellationToken = default)
    {
        await using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read);
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


    public static Dictionary<byte, FileLogSegment> MapPartitionsWithSegments(string workingDirectory, byte partitionsCount)
    {
        var logSegments = GetOrCreateLogSegments(workingDirectory, partitionsCount);
        
        var segmentsMap = Enumerable.Range(0, logSegments.Length).Zip(logSegments, (k, v) => new { key = (byte)k, val = v })
            .ToDictionary(v => v.key, v => new FileLogSegment(v.key, v.val));
        
        return segmentsMap;
    }
    
    private static string[] GetOrCreateLogSegments(string workingDirectory, byte partitionsCount)
    {
        var logSegments = new List<string>(FileLogSegment.GetLogSegments(workingDirectory));
        var newFilesCount = Math.Max(partitionsCount, logSegments.Count) - logSegments.Count;
        if (newFilesCount == 0)
        {
            return logSegments.ToArray();
        }

        var fileNames = logSegments.Select(Path.GetFileNameWithoutExtension).Select(v => int.Parse(v!)).ToArray();
        var maxSegment = fileNames.Length > 0 ? fileNames.Max() : -1;
        var newFiles = Enumerable.Range(maxSegment + 1, newFilesCount).Select(v =>  FileLogSegment.PreparePath(workingDirectory, v)).ToArray();
        logSegments.AddRange(newFiles);

        return logSegments.ToArray();
    }

    private static string[] GetLogSegments(string workingDirectory) => Directory.GetFiles(workingDirectory, $"*{EventLogFileExtension}");

    private static string PreparePath(string workingDirectory, int fileIndex) =>
        Path.Combine(workingDirectory, $"{fileIndex}{EventLogFileExtension}");

    
}
