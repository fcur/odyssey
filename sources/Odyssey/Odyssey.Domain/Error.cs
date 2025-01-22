namespace Odyssey.Domain;

public abstract record Error(string Type, string? Message=null, Dictionary<string, object>? Data = null );