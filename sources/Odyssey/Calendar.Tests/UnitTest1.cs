namespace Calendar.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var userName = new UserName("John", "Fitzgerald", "Kennedy");
        var startDate = new StartDate(DateTimeOffset.Parse("2023-02-01"));
        var email = new Email("foo@bar.com");
        var user = new User(userName, email);
        var timeOffSettings = TimeOffSettings.CreateDefault();

        var actor = Actor.Crete(user, startDate, timeOffSettings);
    }
}