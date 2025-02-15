namespace Odyssey.HRMS.Domain.Base;

public abstract record NestedDomainEntity<TId>()
{
    protected TId Id { get; init; }

    protected NestedDomainEntity(TId id) : this()
    {
        Id = id;
    }
}