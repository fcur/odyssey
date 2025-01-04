namespace Calendar;

public sealed record User(UserName Name, Email Email, UserId Id)
{
    public static User Create(UserName name, Email email)
    {
        var timeNow = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7(timeNow);
        var userId = new UserId(id);

        return new User(name, email, userId);
    }
}

public sealed record UserName(params string[] Name);

public sealed record Email(string Value);

public sealed record UserId(Guid Id);