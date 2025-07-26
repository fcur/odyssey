namespace Odyssey.HRMS.EventLogLite.Base;

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