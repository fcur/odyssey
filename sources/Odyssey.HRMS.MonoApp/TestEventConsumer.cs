using Odyssey.HRMS.EventLogLite;
using Odyssey.HRMS.EventLogLite.Consumer;

namespace Odyssey.HRMS.MonoApp;

public sealed class TestEventConsumer1 : IEventConsumerImpl<TestEvent>
{
}

public sealed class TestEventConsumer2 : IEventConsumerImpl<TestEvent>
{
}

public interface IEventConsumerImpl<TEvent>: IEventConsumerImpl where TEvent : class
{
}

public interface IEventConsumerImpl
{
    
}