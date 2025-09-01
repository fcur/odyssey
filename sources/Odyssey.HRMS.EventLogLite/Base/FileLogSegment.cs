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
    
    public static string Init(string topicName, byte partitions)
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

    public static string Init(EventLogTopic topic)
    {
        return Init(topic.Name, topic.Partitions);
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
    
    public static IReadOnlyCollection<FileLogSegment> Scan(string topicName)
    {
        // workingDirectory/topicName
        var workingDirectory = GetWorkingDirectory(topicName);
        
        // workingDirectory/topicName/0
        // workingDirectory/topicName/1
        // workingDirectory/topicName/2
        var existingDirectories = Directory.GetDirectories(workingDirectory).Select(v => new DirectoryInfo(v)).ToArray();
        
        var logSegmentRoots = existingDirectories
            .Select(v => byte.TryParse(v.Name, out var partitionIdResult) ? new FileLogSegmentRoot(partitionIdResult, v.FullName) : null)
            .Where(v => v is not null).ToArray();
        
        var segments = new List<FileLogSegment>();

        foreach (var logRoot in logSegmentRoots)
        {
            var partition = logRoot!.PartitionId;
            var logFiles = Directory.GetFiles(logRoot.Path, EventFileType.LogFile.GetSearchPattern());
            var indexFiles = Directory.GetFiles(logRoot.Path, EventFileType.IndexFile.GetSearchPattern());
            var timeIndexFiles = Directory.GetFiles(logRoot.Path, EventFileType.TimeIndexFile.GetSearchPattern());

            if (logFiles.Length == 0)
            {
                continue;
            }

            var validIndex = logFiles.Length == indexFiles.Length;
            var validTimeIndex = logFiles.Length == timeIndexFiles.Length;
            if (!validIndex || !validTimeIndex)
            {
                throw new LogSegmentException("Index files mismatch.", $"Root: '{logRoot.Path}', log files: '{logFiles.Length}', index files: '{indexFiles.Length}', time index files: '{timeIndexFiles.Length}'.");
            }

            foreach (var logSegmentFilePath in logFiles)
            {
                var logSegmentFileName = Path.GetFileNameWithoutExtension(logSegmentFilePath);
                
                if (!long.TryParse(logSegmentFileName, out var baseOffset))
                {
                    throw new LogSegmentException("Log segment file name mismatch.", $"Filename '{logSegmentFilePath}' should be integer.");
                }

                var logSegmentFileInfo = new FileInfo(logSegmentFilePath);
                var size = logSegmentFileInfo.Exists ? logSegmentFileInfo.Length : 0;
                var segment = new FileLogSegment(partition, workingDirectory, baseOffset, 0, size, false);
                
                segments.Add(segment);
            }
            
        }
        
        
        return segments.ToArray();
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
}

public record LogSegment(byte Partition, long BaseOffset, long BaseTime)
{
}

public sealed record FileLogSegment(byte Partition, string Root, long BaseOffset, long BaseTime, long Size, bool IsActive)
    : LogSegment(Partition, BaseOffset, BaseTime)
{

    private const string EventLogFileExtension = ".log";
    private const string EventIndexFileExtension = ".index";
    private const string EventTimeFileExtension = ".tindex";

    public string GetLogFilePath() => GetFilePath(Root, Partition, BaseOffset, EventLogFileExtension);
    public string GetIndexFilePath() => GetFilePath(Root, Partition, BaseOffset, EventIndexFileExtension);
    public string GetTimeIndexFilePath() => GetFilePath(Root, Partition, BaseOffset, EventTimeFileExtension);

    public string GetPath(EventFileType eventType)
    {
        var extension = eventType.GetExtension();

        return GetFilePath(Root, Partition, BaseOffset, extension);
    }

    public bool IsEmpty() => BaseTime == 0 && Size == 0;

    public static FileLogSegment New(byte partition, string root)
    {
        return new FileLogSegment(partition, root, 0, 0, 0, false);
    }

    private static string GetFilePath(string root, byte partition, long baseOffset, string extension)
    {
        return Path.Combine(root, partition.ToString(), $"{baseOffset:0000000000000000000}{extension}");
    }
}