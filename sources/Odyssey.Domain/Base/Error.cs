namespace Odyssey.Domain.Base;

public abstract record Error(string Type, string? Message=null, Dictionary<string, object>? Extensions = null );