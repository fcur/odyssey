namespace Odyssey.HRMS.Domain.Base;

public abstract record NestedDomainEntity<TId>()
{
    public TId Id { get; init; }

    protected NestedDomainEntity(TId id) : this()
    {
        Id = id;
    }
}