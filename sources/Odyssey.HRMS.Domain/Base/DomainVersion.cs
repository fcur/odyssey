namespace Odyssey.HRMS.Domain.Base;

public sealed record DomainVersion(ulong Value)
{
    public static readonly DomainVersion New = new(1UL);
    public static readonly DomainVersion? Unset = null;

    public static DomainVersion operator ++(DomainVersion version) => new(version.Value + 1UL);
    public static implicit operator ulong(DomainVersion version) => version.Value;
    public override string ToString() => Value.ToString();
    public override int GetHashCode() => Value.GetHashCode();
}