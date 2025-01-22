namespace Odyssey.Domain.UserEntity;

public sealed record User(UserId Id, UserName Name, Email Email)
{
    public static User Create(UserId id, UserName name, Email email)
    {
        return new User(id, name, email);
    }
}