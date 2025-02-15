namespace Odyssey.HRMS.Domain.Base;

public sealed record DomainVersion(ulong Value)
{
    public static readonly DomainVersion New = new DomainVersion(1UL);
    public static readonly DomainVersion? Unknown = null;

    public static DomainVersion operator ++(DomainVersion version) => new DomainVersion(version.Value + 1UL);

    public override string ToString() => Value.ToString();

    public override int GetHashCode() => Value.GetHashCode();
}