using CSharpFunctionalExtensions;
using System.Collections.Concurrent;

namespace Odyssey.HRMS.EventLogLite.Base;

public sealed record EventFileType(byte Type)
{
    private const string EventLogFileExtension = ".log";
    private const string EventIndexFileExtension = ".index";
    private const string EventTimeFileExtension = ".tindex";

    private const string EventLogFilePattern = $"*{EventLogFileExtension}";
    private const string EventIndexFilePattern = $"*{EventIndexFileExtension}";
    private const string EventTimeFilePattern = $"*{EventTimeFileExtension}";

    public static readonly EventFileType LogFile = new EventFileType(0);
    public static readonly EventFileType IndexFile = new EventFileType(1);
    public static readonly EventFileType TimeIndexFile = new EventFileType(2);

    public string GetExtension()
    {
        return Type switch
        {
            0 => EventLogFileExtension,
            1 => EventIndexFileExtension,
            2 => EventTimeFileExtension,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public string GetSearchPattern()
    {
        return Type switch
        {
            0 => EventLogFilePattern,
            1 => EventIndexFilePattern,
            2 => EventTimeFilePattern,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public sealed class LogSegmentException : Exception
{
    public LogSegmentException(string message, string details) : base(message) { }
}

public static class LogSegmentDirectory
{
    public const string EventLogFileExtension = ".log";
    public const string EventIndexFileExtension = ".index";
    public const string EventTimeFileExtension = ".tindex";

    private const string EventLoggingRootKey = "EventLoggingRoot";


    // private const string EventLogFileExtension = ".log";
    // private const string EventIndexFileExtension = ".index";
    // private const string EventTimeFileExtension = ".tindex";

    // [Obsolete]
    // public static Dictionary<byte, FileLogSegment> MapPartitionsWithSegments(EventLogTopic topic)
    // {
    //     var workingDirectory = Path.Combine(Environment.CurrentDirectory, topic.Name);
    //     var partitionsCount = topic.Partitions;
    //
    //     var logSegments = GetOrCreateLogSegments(workingDirectory, partitionsCount);
    //
    //     var segmentsMap = Enumerable.Range(0, logSegments.Length).Zip(logSegments, (k, v) => new { key = (byte)k, val = v })
    //         .ToDictionary(v => v.key, v => new FileLogSegment(v.key, v.val, 0,0,0 ));
    //
    //     return segmentsMap;
    // }

    //
    // {
    //     var logSegments = new List<string>(GetLogSegments(workingDirectory));
    //     var newFilesCount = Math.Max(partitionsCount, logSegments.Count) - logSegments.Count;
    //     if (newFilesCount == 0)
    //     {
    //         return logSegments.ToArray();
    //     }
    //
    //     var fileNames = logSegments.Select(Path.GetFileNameWithoutExtension).Select(v => int.Parse(v!)).ToArray();
    //     var maxSegment = fileNames.Length > 0 ? fileNames.Max() : -1;
    //     var newFiles = Enumerable.Range(maxSegment + 1, newFilesCount).Select(v => FileLogSegment.PreparePath(workingDirectory, v)).ToArray();
    //     logSegments.AddRange(newFiles);
    //
    //     return logSegments.ToArray();
    // }
    //
    // private static string[] GetLogSegments(string workingDirectory) => Directory.GetFiles(workingDirectory, $"*{EventLogFileExtension}");

    public static string SetEventLoggingRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Environment.SetEnvironmentVariable(EventLoggingRootKey, fullPath, EnvironmentVariableTarget.Process);

        return fullPath;
    }

    public static string GetEventLoggingRoot()
    {
        var baseDirectory = Environment.GetEnvironmentVariable(EventLoggingRootKey, EnvironmentVariableTarget.Process) ??
                            Environment.CurrentDirectory;

        return baseDirectory;
    }

    public static string GetWorkingDirectory(string topicName)
    {
        var baseDirectory = GetEventLoggingRoot();
        var workingDirectory = Path.GetFullPath(Path.Combine(baseDirectory, topicName));

        return workingDirectory;
    }

    public static string GetOrCreate(string topicName, byte partitions)
    {
        var workingDirectory = GetWorkingDirectory(topicName);

        if (!Directory.Exists(workingDirectory))
        {
            Directory.CreateDirectory(workingDirectory);
        }

        var existingFolders = Directory.GetDirectories(workingDirectory).Select(v => new DirectoryInfo(v)).ToArray();
        var wantedFolders = Enumerable.Range(0, partitions).Select(v => new FileLogSegmentRoot((byte)v, Path.Combine(workingDirectory, v.ToString())))
            .ToArray();

        if (!existingFolders.Any())
        {
            Array.ForEach(wantedFolders, item => Directory.CreateDirectory(item.Path));
            // return wantedFolders.Select(v => v.Path).ToArray();
            return workingDirectory;
        }

        var validFolders = existingFolders
            .Select(v => byte.TryParse(v.Name, out var partitionIdResult) ? new FileLogSegmentRoot(partitionIdResult, v.FullName) : null)
            .Where(v => v is not null).ToArray();

        var missingFolders = wantedFolders.Except(validFolders).ToArray();
        if (missingFolders.Length == 0)
        {
            // return validFolders.Select(v => v!.Path).ToArray();
            return workingDirectory;
        }

        Array.ForEach(missingFolders, item => Directory.CreateDirectory(item!.Path));

        // return validFolders.Concat(missingFolders).OrderBy(v => v!.PartitionId).Select(v => v!.Path).ToArray();
        return workingDirectory;
    }

    public static string GetOrCreate(EventLogTopic topic)
    {
        return GetOrCreate(topic.Name, topic.Partitions);
    }

    public static void Cleanup(string topicName)
    {
        var workingDirectory = GetWorkingDirectory(topicName);

        if (!Directory.Exists(workingDirectory))
        {
            return;
        }

        Directory.Delete(workingDirectory, true);
    }
    
    public static IReadOnlyCollection<EventLogTopicScanResult> ScanLoggingRoot(string loggingRoot)
    {
        
        var scanResults = new ConcurrentBag<EventLogTopicScanResult>();

        Parallel.ForEach(Directory.GetDirectories(loggingRoot).Select(v => new DirectoryInfo(v)), td =>
        {
            var topicScanResult = ScanTopicRoot(td);
            scanResults.Add(topicScanResult);
        });

        return scanResults.ToArray();

        // topic1, 3 partitions
        // workingDirectory/topicName1
        // workingDirectory/topicName1/0
        // workingDirectory/topicName1/1
        // workingDirectory/topicName1/2

        // topic 2, 2 partitions
        // workingDirectory/topicName2
        // workingDirectory/topicName2/0
        // workingDirectory/topicName2/1
    }
    
    
    public static EventLogTopicScanResult ScanTopicRoot(DirectoryInfo directoryInfo)
    {
        var segments = ScanSegmentRoot(directoryInfo).ToArray();
        
        return new EventLogTopicScanResult(directoryInfo.Name, segments);
    }
    
    public static IEnumerable<PartitionSegments> ScanSegmentRoot(DirectoryInfo directoryInfo)
    {
        var directories = Directory.GetDirectories(directoryInfo.FullName).Select(v => new DirectoryInfo(v)).ToArray();
        if (directories.Length == 0)
        {
            var firstPath = Path.Combine(directoryInfo.FullName, "0");
            throw new DirectoryNotFoundException($"First partition directory NOT found: '{firstPath}'.");
        }

        foreach (var directory in directories)
        {
            if (!byte.TryParse(directory.Name, out var partitionIdResult))
            {
                // continue;
                throw new InvalidDataException($"Found invalid partition: '{directory.Name}' in directory: '{directory.FullName}'.");
            }
            
            var segments = ScanFileLogSegmentRoot(directory).ToArray();
            yield return new PartitionSegments(partitionIdResult, directory.FullName, segments);
                // (new FileLogSegmentRoot(partitionIdResult, directory.FullName), segments);
        }
    }

    public static IEnumerable<FileLogSegment> ScanFileLogSegmentRoot(DirectoryInfo directoryInfo)
    {
        var segmentSearchPattern = EventFileType.LogFile.GetSearchPattern();
        // foreach (var file in directoryInfo.EnumerateFiles(segmentSearchPattern, SearchOption.AllDirectories))
        // {
        //     var logFileResult = new FileLogSegmentPath(file.FullName);
        //     if (logFileResult.Result.IsFailure)
        //     {
        //         throw new Exception($"Found at least one invalid partition for log segment root: '{directoryInfo.FullName}'.");
        //     }
        //     
        // }
        // return;
        
        var logFiles = Directory.GetFiles(directoryInfo.FullName, segmentSearchPattern);
        if (logFiles.Length == 0)
        {
            yield break;
        }

        var logFilesWithValidationResult = logFiles.Select(v => new FileLogSegmentPath(v)).ToArray();
        var logFilesWithFailure = logFilesWithValidationResult.Where(v => v.Result.IsFailure).Select(v => v.Result.Error);
        if (logFilesWithFailure.Any())
        {
            // TODO: to aggregate exception with `logFilesWithFailure`
            throw new Exception($"Found at least one invalid partition for log segment root: '{directoryInfo.FullName}'.");
        }
        var logFilesResults = logFilesWithValidationResult.OrderByDescending(v => v.BaseOffset).ToArray();
        
        for (var index = 0; index < logFilesResults.Length; index++)
        {
            var partition = byte.Parse(directoryInfo.Name);
            var topicRoot = directoryInfo.Parent!.FullName;
            var segment = BuildFileLogSegment(logFilesResults[index], topicRoot, partition, index == 0);

            yield return segment;
        }
    }

    private static FileLogSegment BuildFileLogSegment(FileLogSegmentPath logSegmentPath, string topicRoot, byte partition, bool isActive)
    {
        var baseOffset = logSegmentPath.BaseOffset;

        var indexFileValid = ValidateSegmentIndex(topicRoot, partition, baseOffset);
        if (indexFileValid.HasValue)
        {
            throw indexFileValid.Value;
        }

        var timeIndexFileValid = ValidateSegmentTimeIndex(topicRoot, partition, baseOffset);
        if (timeIndexFileValid.IsFailure)
        {
            throw timeIndexFileValid.Error;
        }

        var logSegmentFileInfo = new FileInfo(logSegmentPath.Value);
        var size = logSegmentFileInfo.Length;
        var baseTime = timeIndexFileValid.Value;
        var segment = new FileLogSegment(partition, topicRoot, baseOffset, baseTime, size, isActive);

        return segment;
    }
    
    
    public static IReadOnlyDictionary<byte, LinkedList<FileLogSegment>> ScanOffsets(string topicName)
    {
        // workingDirectory/topicName
        var topicRoot = GetWorkingDirectory(topicName);

        // workingDirectory/topicName/0
        // workingDirectory/topicName/1
        // workingDirectory/topicName/2
        // var existingDirectories = Directory.GetDirectories(workingDirectory).Select(v => new DirectoryInfo(v)).ToArray();

        var logSegmentRoots = ScanSegmentRoot(new DirectoryInfo(topicRoot)).ToArray();
        // (FileLogSegmentRoot Root, FileLogSegment[] segments)[]? logSegmentRoots = ScanSegmentRoot(new  DirectoryInfo(topicRoot)).ToArray();
        // var logSegmentRoots = existingDirectories
        //     .Select(v => byte.TryParse(v.Name, out var partitionIdResult) ? new FileLogSegmentRoot(partitionIdResult, v.FullName) : null)
        //     .Where(v => v is not null).OrderBy(v=>v!.PartitionId).ToArray();

        var scanResult = new Dictionary<byte, LinkedList<FileLogSegment>>();

        foreach (var logRoot in logSegmentRoots)
        {
            var partition = logRoot.PartitionId;// Root.PartitionId;
            var logFiles = Directory.GetFiles(logRoot.Path//.Root.Path
                , EventFileType.LogFile.GetSearchPattern());

            if (logFiles.Length == 0)
            {
                var newSegment = FileLogSegment.New(partition, topicRoot);
                scanResult.Add(partition, new LinkedList<FileLogSegment>([newSegment]));
                continue;
            }

            var segments = new LinkedList<FileLogSegment>();
            
            var logFilesWithValidationResult = logFiles.Select(v => new FileLogSegmentPath(v)).ToArray();
            var logFilesWithFailure = logFilesWithValidationResult.Where(v => v.Result.IsFailure).Select(v => v.Result.Error).ToArray();
            if (logFilesWithFailure.Any())
            {
                // TODO: to aggregate exception with `logFilesWithFailure`
                throw new Exception();
            }

            var logFilesResults = logFilesWithValidationResult.OrderByDescending(v => v.BaseOffset).ToArray();

            for (var index = 0; index < logFilesResults.Length; index++)
            {
                var segment = BuildFileLogSegment(logFilesResults[index], topicRoot, partition, index == 0);
                segments.AddLast(segment);
            }

            scanResult.Add(partition, segments);
        }

        return scanResult.OrderBy(v=>v.Key).ToDictionary();
    }

    // private static string PreparePath(string partitionRoot, int fileIndex) =>
    //     Path.Combine(partitionRoot, $"{fileIndex}{EventLogFileExtension}");
    //
    //
    // public static string PreparePath(string partitionRoot, long initialOffset, EventFileType eventType)
    // {
    //     var extension = eventType.GetExtension();
    //
    //     return Path.Combine(partitionRoot, $"{initialOffset:0000000000000000000}{extension}");
    // }
    private static Result<long, LogSegmentException> ValidateSegmentFileName(string logSegmentPath)
    {
        var segmentFileName = Path.GetFileNameWithoutExtension(logSegmentPath);

        if (!long.TryParse(segmentFileName, out var baseOffset))
        {
            return new LogSegmentException("Log segment file name mismatch.", $"Filename '{segmentFileName}' should be integer.");
        }

        return baseOffset;
    }

    private static Maybe<LogSegmentException> ValidateSegmentIndex(string root, byte partition, long baseOffset)
    {
        var logSegmentIndexPath = FileLogSegment.GetFilePath(root, partition, baseOffset, EventIndexFileExtension);

        if (!Path.Exists(logSegmentIndexPath))
        {
            return new LogSegmentException("Log segment file name mismatch.", $"Index file '{logSegmentIndexPath}' should exist.");
        }

        return Maybe<LogSegmentException>.None;
    }
    
    private static Maybe<LogSegmentException> ValidateSegmentIndex(string partitionRoot, long baseOffset)
    {
        var logSegmentIndexPath = FileLogSegment.GetFilePath(partitionRoot, baseOffset, EventIndexFileExtension);

        if (!Path.Exists(logSegmentIndexPath))
        {
            return new LogSegmentException("Log segment file name mismatch.", $"Index file '{logSegmentIndexPath}' should exist.");
        }

        return Maybe<LogSegmentException>.None;
    }

    private static Result<long, LogSegmentException> ValidateSegmentTimeIndex(string root, byte partition, long baseOffset)
    {
        var logSegmentTimeIndexPath = FileLogSegment.GetFilePath(root, partition, baseOffset, EventTimeFileExtension);
        if (!Path.Exists(logSegmentTimeIndexPath))
        {
            return new LogSegmentException("Log segment file name mismatch.", $"Time index '{logSegmentTimeIndexPath}' file should exist.");
        }

        using var timeIndexReader = new BinaryReader(File.Open(logSegmentTimeIndexPath, FileMode.Open));
        var baseTime = timeIndexReader.ReadInt64();

        return baseTime;
    }
    
    private static Result<long, LogSegmentException> ValidateSegmentTimeIndex(string partitionRoot, long baseOffset)
    {
        var logSegmentTimeIndexPath = FileLogSegment.GetFilePath(partitionRoot, baseOffset, EventTimeFileExtension);
        if (!Path.Exists(logSegmentTimeIndexPath))
        {
            return new LogSegmentException("Log segment file name mismatch.", $"Time index '{logSegmentTimeIndexPath}' file should exist.");
        }

        using var timeIndexReader = new BinaryReader(File.Open(logSegmentTimeIndexPath, FileMode.Open));
        var baseTime = timeIndexReader.ReadInt64();

        return baseTime;
    }
}

public record LogSegment(byte Partition, long BaseOffset, long BaseTime, bool IsActive)
{
}

public sealed record FileLogSegment(byte Partition, string TopicRoot, long BaseOffset, long BaseTime, long Size, bool IsActive)
    : LogSegment(Partition, BaseOffset, BaseTime, IsActive)
{
    public string GetLogFilePath() => GetFilePath(TopicRoot, Partition, BaseOffset, LogSegmentDirectory.EventLogFileExtension);
    public string GetIndexFilePath() => GetFilePath(TopicRoot, Partition, BaseOffset, LogSegmentDirectory.EventIndexFileExtension);
    public string GetTimeIndexFilePath() => GetFilePath(TopicRoot, Partition, BaseOffset, LogSegmentDirectory.EventTimeFileExtension);

    public string GetPath(EventFileType eventType)
    {
        var extension = eventType.GetExtension();

        return GetFilePath(TopicRoot, Partition, BaseOffset, extension);
    }

    public bool IsEmpty() => BaseTime == 0 && Size == 0;

    public FileLogSegment Activate()
    {
        return this with { IsActive = true };
    }

    public static FileLogSegment New(byte partition, string topicRoot)
    {
        return new FileLogSegment(partition, topicRoot, 0, 0, 0, true);
    }

    public static string GetFilePath(string topicRoot, byte partition, long baseOffset, string extension)
    {
        return Path.Combine(topicRoot, partition.ToString(), $"{baseOffset:0000000000000000000}{extension}");
    }
    
    public static string GetFilePath(string partitionRoot, long baseOffset, string extension)
    {
        return Path.Combine(partitionRoot, $"{baseOffset:0000000000000000000}{extension}");
    }
}

public sealed class FileLogSegmentPath
{
    public string Value { get; init; }

    public long BaseOffset => Result.IsSuccess ? Result.Value : throw new ArgumentException();

    public Result<long, LogSegmentException> Result { get; private set; }

    public FileLogSegmentPath(string path)
    {
        Value = path;
        var fileName = Path.GetFileNameWithoutExtension(path);

        if (!long.TryParse(fileName, out var baseOffset))
        {
            Result = new LogSegmentException("Log segment file name mismatch.", $"Filename '{fileName}' should be integer.");
        }

        Result = baseOffset;
    }
}

