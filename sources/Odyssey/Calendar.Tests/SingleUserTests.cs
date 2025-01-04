using FluentAssertions;
using FluentAssertions.Execution;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Calendar.Tests;

public sealed class SingleUserTests
{
    [Fact]
    public void SerializationTest()
    {
        var userName = new UserName("John", "Fitzgerald", "Kennedy");
        var email = new Email("foo@bar.com");
        var user = User.Create(userName, email);
        var timeOffSettings = TimeOffSettings.CreateDefault();
        var startDate = new StartDate(DateTimeOffset.Parse("2023-02-13"));

        var actor = CalendarActor.Crete(user, startDate, timeOffSettings);

        var serializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .WithTypeConverter(new DateTimeOffsetYamlConverter())
            .Build();
        var yamlString = serializer.Serialize(actor);

        using var scope = new AssertionScope();
        yamlString.Should().NotBeNull().And.NotBeEmpty();
    }
}

