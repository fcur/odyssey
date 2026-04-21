using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Odyssey.HRMS.EventLogLite.Base;


public sealed class LogIndexAccessor: IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly int _recordCount;
    private unsafe byte* _basePointer = null;
    
    public LogIndexAccessor(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        
        // index file size limit 2Gb
        if (fileSize > int.MaxValue)
        {
            throw new NotSupportedException();
        }

        _recordCount = (int)(fileSize / Marshal.SizeOf<IndexEntry>());

        _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        unsafe
        {
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
        }
    }
    
    private ReadOnlySpan<IndexEntry> Data
    {
        get
        {
            unsafe
            {
                return new ReadOnlySpan<IndexEntry>(_basePointer, _recordCount);
            }
        }
    }
    
    
    public (long Offset, long Position) FindNearest(long targetOffset)
    {
        if (_recordCount == 0)
        {
            throw new InvalidOperationException("Empty index");
        }

        ReadOnlySpan<IndexEntry> data = Data;

        var left = 0;
        var right = _recordCount - 1;

        while (left <= right)
        {
            var mid = left + (right - left) / 2;
            
            ref readonly var entry = ref data[mid];

            if (entry.Offset == targetOffset)
            {
                return (entry.Offset, entry.Position);
            }

            if (entry.Offset < targetOffset)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        var leftIndexClamp = Math.Clamp(left, 0, _recordCount - 1);
        var rightIndexClamp = Math.Clamp(right, 0, _recordCount - 1);

        ref readonly var leftIndexEntry = ref data[leftIndexClamp];
        ref readonly var rightIndexEntry = ref data[rightIndexClamp];

        if (Math.Abs(targetOffset - leftIndexEntry.Offset) < Math.Abs(targetOffset - rightIndexEntry.Offset))
        {
            return (leftIndexEntry.Offset, leftIndexEntry.Position);
        }
        
        return (rightIndexEntry.Offset, rightIndexEntry.Position);
    }

    public void Dispose()
    {
        unsafe
        {
            if (_basePointer != null)
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
        _accessor?.Dispose();
        _mmf?.Dispose();
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IndexEntry
{
    public long Offset;
    public long Position;
}