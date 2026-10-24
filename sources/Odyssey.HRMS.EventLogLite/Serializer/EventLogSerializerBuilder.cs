using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.EventLogLite.Serializer;

public sealed class EventLogSerializerBuilder
{
    private IEventLogKeySerializer _keySerializer = null!;
    private IEventLogMessageSerializer _messageSerializer = null!;
    

    public EventLogSerializerBuilder WithMessageSerializer(IEventLogMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _messageSerializer = serializer;
        return this;
    }

    public EventLogSerializerBuilder WithKeySerializer(IEventLogKeySerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _keySerializer = serializer;
        return this;
    }
    
    public IEventLogSerializer Build()
    {
        if (_keySerializer is null || _messageSerializer is null)
        {
            throw new InvalidOperationException();
        }

        return new CompositeEventLogSerializer(_keySerializer, _messageSerializer);
    }
}