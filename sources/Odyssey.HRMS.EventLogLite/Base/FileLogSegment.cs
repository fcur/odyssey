using Odyssey.HRMS.EventLogLite.Entities;
using System.Text.Json;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IEventLogger<in TSegment> where TSegment : LogSegment
{
    Task Write<TEvent>(LogMessage<TEvent> logMessage, TSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
    
    Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(TSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
}

public interface IFileEventLogger: IEventLogger<FileLogSegment>
{
    new Task Write<TEvent>(LogMessage<TEvent> logMessage, FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;

    new Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class;
}

public sealed class JsonFileEventLogger: IFileEventLogger
{
    // log divider symbol, equals to '\n'
    private const byte EventLogDivider = 10;
    
    public async Task Write<TEvent>(LogMessage<TEvent> logMessage, FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class
    {
        await using var fs = new FileStream(segment.FilePath, FileMode.OpenOrCreate, FileAccess.Write);
        fs.Seek(0, SeekOrigin.End);
        
        await fs.WriteAsync(new[] { EventLogDivider }, cancellationToken);
        await JsonSerializer.SerializeAsync(fs, logMessage, new JsonSerializerOptions { WriteIndented = false }, cancellationToken);
    }
    
    public async Task<LogMessage<TEvent>?> ReadLastMessage<TEvent>(FileLogSegment segment, CancellationToken cancellationToken = default) where TEvent : class
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

    public static void InitWorkingDirectory(EventLogTopic topic)
    {
        var workingDirectory = Path.Combine(Environment.CurrentDirectory, topic.Value);
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
        var newFiles = Enumerable.Range(maxSegment + 1, newFilesCount).Select(v =>  FileLogSegment.PreparePath(workingDirectory, v)).ToArray();
        logSegments.AddRange(newFiles);

        return logSegments.ToArray();
    }

    private static string[] GetLogSegments(string workingDirectory) => Directory.GetFiles(workingDirectory, $"*{EventLogFileExtension}");

    private static string PreparePath(string workingDirectory, int fileIndex) =>
        Path.Combine(workingDirectory, $"{fileIndex}{EventLogFileExtension}");
}