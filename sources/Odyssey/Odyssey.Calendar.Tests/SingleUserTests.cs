using FluentAssertions;
using FluentAssertions.Execution;
using Odyssey.Calendar.Tests.YamlConverter;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Odyssey.Calendar.Tests;

public sealed class SingleUserTests
{
    private const string SingleUserFilename = "single-user.yaml";
    private static readonly ISerializer YamlSerializer;
    private static readonly IDeserializer YamlDeserializer;

    static SingleUserTests()
    {
        var serializerBuilder = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .WithTypeConverter(new StartDateYamlConverter())
            .WithTypeConverter(new UserNameYamlConverter())
            .WithTypeConverter(new UserIdYamlConverter())
            .WithTypeConverter(new EmailYamlConverter())
            .WithTypeConverter(new PaidTimeOffDurationYamlConverter())
            .WithTypeConverter(new UnpaidTimeOffDurationYamlConverter())
            .WithTypeConverter(new FamilyTimeOffDurationYamlConverter())
            .WithTypeConverter(new TimeOffRoundingYamlConverter());

        var deserializerBuilder = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .WithTypeConverter(new StartDateYamlConverter())
            .WithTypeConverter(new UserNameYamlConverter())
            .WithTypeConverter(new UserIdYamlConverter())
            .WithTypeConverter(new EmailYamlConverter())
            .WithTypeConverter(new PaidTimeOffDurationYamlConverter())
            .WithTypeConverter(new UnpaidTimeOffDurationYamlConverter())
            .WithTypeConverter(new FamilyTimeOffDurationYamlConverter())
            .WithTypeConverter(new TimeOffRoundingYamlConverter());

        YamlSerializer = serializerBuilder.Build();
        YamlDeserializer = deserializerBuilder.Build();
    }

    [Fact]
    public void SerializationTest()
    {
        var userName = new UserName("John", "Fitzgerald", "Kennedy");
        var email = new Email("foo@bar.com");
        var user = User.Create(userName, email);
        var timeOffSettings = TimeOffSettings.CreateDefault();
        var startDate = new StartDate(DateTimeOffset.Parse("2023-02-13"));
        var actor = CalendarActor.Crete(user, startDate, timeOffSettings);

        var yamlString = YamlSerializer.Serialize(actor);

        using var scope = new AssertionScope();
        yamlString.Should().NotBeNull().And.NotBeEmpty();
    }

    [Fact]
    public void DeserializationTest()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), SingleUserFilename);
        var yamlString = File.ReadAllText(filePath);

        var userId = Guid.Parse("01943690-779a-7a89-8f95-fd624aa78dfc");
        var startDate = DateTimeOffset.Parse("2023-02-13 0:00:00 +02:00");
        var paidTimeOffDuration = TimeSpan.FromDays(20);
        var unpaidTimeOffDuration = TimeSpan.Zero;
        var familyTimeOffDuration = TimeSpan.Zero;
        var timeOffRoundingInterval = TimeSpan.FromSeconds(1);

        var config = YamlDeserializer.Deserialize<SingleUserActorYamlConfig>(yamlString);
        var actor = config.ToCalendarActor();

        using var scope = new AssertionScope();
        actor.Should().NotBeNull();
        actor.User.Should().NotBeNull();
        actor.StartDate.Should().NotBeNull();
        actor.StartDate.Value.Should().Be(startDate);
        
        actor.TimeOffSettings.Should().NotBeNull();
        actor.User.Id.Should().NotBeNull();
        actor.User.Id.Value.Should().Be(userId);

        actor.User.Email.Should().NotBeNull();
        actor.User.Email.Value.Should().Be("foo@bar.com");

        actor.User.Name.Should().NotBeNull();
        actor.User.Name.Values.Should().ContainInOrder("John", "Fitzgerald", "Kennedy");
        
        actor.TimeOffSettings.Should().NotBeNull();
        
        actor.TimeOffSettings.Paid.Should().NotBeNull();
        actor.TimeOffSettings.Paid.Value.Should().Be(paidTimeOffDuration);
        
        actor.TimeOffSettings.Unpaid.Should().NotBeNull();
        actor.TimeOffSettings.Unpaid.Value.Should().Be(unpaidTimeOffDuration);
        
        actor.TimeOffSettings.Family.Should().NotBeNull();
        actor.TimeOffSettings.Family.Value.Should().Be(familyTimeOffDuration);

        actor.TimeOffSettings.Rounding.Should().NotBeNull();
        actor.TimeOffSettings.Rounding.Value.Should().Be(timeOffRoundingInterval);
    }
}