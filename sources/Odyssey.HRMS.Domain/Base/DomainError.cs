namespace Odyssey.HRMS.Domain.Base;

public abstract record DomainError(string Type, string? Message=null, Dictionary<string, object>? Extensions = null );