using Odyssey.HRMS.Domain.Base;

namespace Odyssey.HRMS.Domain.UserEntity;

public sealed record User(UserId Id, UserName Name, Email Email,DateTimeOffset ChangedAt, DomainVersion Version, ulong RowVersion)
 : DomainEntity<UserId>(Id, ChangedAt, Version)
{
    public static User Create(UserId id, UserName name, Email email)
    {
        var changedAt = DateTimeOffset.UtcNow;
        var rowVersion = 0UL;
        var version = DomainVersion.New;
        
        return new User(id, name, email, changedAt, version, rowVersion);
    }
}
