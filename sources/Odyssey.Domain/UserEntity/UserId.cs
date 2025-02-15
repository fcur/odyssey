namespace Odyssey.Domain.UserEntity;

public readonly record struct UserId(Guid Value)
{
    public static readonly UserId Empty = new UserId(Guid.Empty);

    public static UserId Parse(string id)
    {
        return new UserId(Guid.Parse(id));
    }

    public static UserId New()
    {
        var timeNow = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7(timeNow);
        var userId = new UserId(id);

        return userId;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
    
    public override string ToString()
    {
        return Value.ToString("D");
    }
}