namespace Calendar;

public sealed record User(UserName UserName, Email Email, Guid Id)
{
    public static User Create(UserName name, Email email)
    {
        var timeNow = DateTimeOffset.UtcNow;
        var userId = Guid.CreateVersion7(timeNow);

        return new User(name, email, userId);
    }
}

public sealed record UserName(params string[] Name);

public sealed record Email(string Value);