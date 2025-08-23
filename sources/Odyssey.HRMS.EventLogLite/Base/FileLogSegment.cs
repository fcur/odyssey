using System.Net.Http.Headers;

namespace Odyssey.HRMS.EventLogLite.Base;

public sealed class FileLogSegmentRoot(byte partitionId, string path) : IEquatable<FileLogSegmentRoot>
{
    public string Path { get; } = path;

    public byte PartitionId { get; } = partitionId;

    public bool Equals(FileLogSegmentRoot? other)
    {
        if (other is null)
        {
            return false;
        }

        return PartitionId == other.PartitionId;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is FileLogSegmentRoot other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Path, PartitionId);
    }
}

public sealed class FileLogSegment(byte partitionId, string filePath) : LogSegment(partitionId)
{
    public string FilePath => filePath;

    public const string EventLoggingRootKey = "EventLoggingRoot";
    private const string EventLogFileExtension = ".log";
    private const string EventIndexFileExtension = ".index";
    private const string EventTimeFileExtension = ".tindex";

    public static Dictionary<byte, FileLogSegment> MapPartitionsWithSegments(EventLogTopic topic)
    {
        var workingDirectory = Path.Combine(Environment.CurrentDirectory, topic.Name);
        var partitionsCount = topic.Partitions;

        var logSegments = GetOrCreateLogSegments(workingDirectory, partitionsCount);

        var segmentsMap = Enumerable.Range(0, logSegments.Length).Zip(logSegments, (k, v) => new { key = (byte)k, val = v })
            .ToDictionary(v => v.key, v => new FileLogSegment(v.key, v.val));

        return segmentsMap;
    }

    public static IReadOnlyCollection<string> InitWorkingDirectory(string topicName, byte partitions)
    {
        var baseDirectory = Environment.GetEnvironmentVariable(EventLoggingRootKey, EnvironmentVariableTarget.Process) ??
                            Environment.CurrentDirectory;

        var workingDirectory = Path.GetFullPath(Path.Combine(baseDirectory, topicName));
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
            return wantedFolders.Select(v => v.Path).ToArray();
        }

        var validFolders = existingFolders
            .Select(v => byte.TryParse(v.Name, out var partitionIdResult) ? new FileLogSegmentRoot(partitionIdResult, v.FullName) : null)
            .Where(v => v is not null).ToArray();

        var missingFolders = wantedFolders.Except(validFolders).ToArray();
        if (missingFolders.Length == 0)
        {
            return validFolders.Select(v => v!.Path).ToArray();
        }

        Array.ForEach(missingFolders, item => Directory.CreateDirectory(item!.Path));

        return validFolders.Concat(missingFolders).OrderBy(v => v!.PartitionId).Select(v => v!.Path).ToArray();
    }

    public static IReadOnlyCollection<string> InitWorkingDirectory(EventLogTopic topic)
    {
        return InitWorkingDirectory(topic.Name, topic.Partitions);
    }


    public static void CleanupWorkingDirectory(string topicName)
    {
        var baseDirectory = Environment.GetEnvironmentVariable(EventLoggingRootKey, EnvironmentVariableTarget.Process) ??
                         Environment.CurrentDirectory;

        var workingDirectory = Path.GetFullPath(Path.Combine(baseDirectory, topicName));
        if (!Directory.Exists(workingDirectory))
        {
            return;
        }

        Directory.Delete(workingDirectory, true);
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