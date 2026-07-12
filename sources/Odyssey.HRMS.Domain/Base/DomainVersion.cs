namespace Odyssey.HRMS.Domain.Base;

public sealed record DomainVersion(long Value)
{
    public static readonly DomainVersion New = new(1L);
    public static readonly DomainVersion? Unset = null;

    public static DomainVersion operator ++(DomainVersion version) => new(version.Value + 1L);
    public static implicit operator long(DomainVersion version) => version.Value;
    public override string ToString() => Value.ToString();
    public override int GetHashCode() => Value.GetHashCode();
}