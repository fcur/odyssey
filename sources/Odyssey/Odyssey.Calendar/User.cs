namespace Odyssey.Calendar;

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

public sealed record UserName(params string[] Values)
{
    public static UserName Parse(string parameters)
    {
        var nameParts = parameters.Split(" ");
        return new UserName(nameParts);
    }
}

public sealed record Email(string Value);

public sealed record UserId(Guid Value)
{
    public static UserId Parse(string id)
    {
        return new UserId(Guid.Parse(id));
    }
}