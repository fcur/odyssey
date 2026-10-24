using System.Runtime.CompilerServices;
using System.Text;

namespace Odyssey.HRMS.EventLogLite.Serializer;

public readonly struct EventLogUtf8StringKeySerializer : IEventLogKeySerializer
{
    private static readonly Type KeyStringType = typeof(string);
    
    public EventLogKeyRaw SerializeKey<TKey>(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        if (typeof(TKey) != KeyStringType)
        {
            throw new ArgumentException("TKey must be of type string");
        }

        var result = new EventLogKeyRaw();
        var keyString = Unsafe.As<TKey, string>(ref key);
        if (string.IsNullOrEmpty(keyString))
        {
            return result;
        }
        
        var keyLength = Encoding.UTF8.GetByteCount(keyString);
        var rentedArray = result.RentMemory(keyLength);
        
        Encoding.UTF8.GetBytes(keyString.AsSpan(), rentedArray.AsSpan(0, keyLength));
        
        result.Commit(keyLength);
        
        return result;
    }
}