namespace Odyssey.Domain.UserEntity;

public sealed record UserName(params string[] Values)
{
    public static UserName Parse(string parameters)
    {
        var nameParts = parameters.Split(" ");
        return new UserName(nameParts);
    }
}