namespace Odyssey.HRMS.Domain.UserEntity;

public interface IUserRepository
{
    Task<User?> Find(UserId id);

    Task Insert(User user);
        
    Task Update(User user);
}