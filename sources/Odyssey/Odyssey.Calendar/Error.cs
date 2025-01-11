namespace Odyssey.Calendar;

public sealed record Error(string ErrorType, string? Message, string? ErrorCode);