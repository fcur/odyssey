namespace Odyssey.HRMS.EventLogLite;

public static class Extensions
{
    public static ScopeState With(this ScopeState state, string key, object value)
    {
        state.Set(key, value);
        return state;
    }

    public static ScopeState WithOffset(this ScopeState state, ulong offset) => state.With("Offset", offset);
    public static ScopeState WithTopic(this ScopeState state, string topic) => state.With("Topic", topic);
    public static ScopeState WithGroup(this ScopeState state, string group) => state.With("Group", group);
    public static ScopeState WithPartition(this ScopeState state, byte partition) => state.With("Partition", partition);
    public static ScopeState WithRequestId(this ScopeState state, Guid requestId) => state.With("RequestId", requestId.ToString("D"));
}

public sealed class ScopeState(Dictionary<string, object> state)
{
    public Dictionary<string, object> State => state;
    
    public static ScopeState Create() => new(new Dictionary<string, object>());
    
    public void Set(string key, object value)
    {
        state[key] = value;
    }
}