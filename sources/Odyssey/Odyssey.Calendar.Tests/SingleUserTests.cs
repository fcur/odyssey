using FluentAssertions;
using FluentAssertions.Execution;
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
            .WithNamingConvention(HyphenatedNamingConvention.Instance);
        // .WithTypeConverter(new StartDateYamlConverter())
        // .WithTypeConverter(new UserNameYamlConverter())
        // .WithTypeConverter(new UserIdYamlConverter())
        // .WithTypeConverter(new EmailYamlConverter())
        // .WithTypeConverter(new PaidTimeOffDurationYamlConverter())
        // .WithTypeConverter(new UnpaidTimeOffDurationYamlConverter())
        // .WithTypeConverter(new FamilyTimeOffDurationYamlConverter())
        // .WithTypeConverter(new TimeOffRoundingYamlConverter())

        var deserializerBuilder = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance);

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
        var dates = new[] { "2023-08-01", "2023-08-05", "2023-10-01", "2023-10-03", "2023-11-20", "2023-11-30", "2023-12-20", "2023-12-24" }
            .Select(DateTimeOffset.Parse).ToArray();
        var index = 0;
        
        var timeOffRequests = new[]
        {
            TimeOffRequest.CreatePaidRequest(TimeOffRequestSettings.Create(dates[index++], dates[index++]).Value),
            TimeOffRequest.CreatePaidRequest(TimeOffRequestSettings.Create(dates[index++], dates[index++]).Value),
            TimeOffRequest.CreateUnpaidRequest(TimeOffRequestSettings.Create(dates[index++], dates[index++]).Value),
            TimeOffRequest.CreateFamilyRequest(TimeOffRequestSettings.Create(dates[index++], dates[index]).Value)
        };

        var yamlConfig = (actor.Value, timeOffRequests).ToConfig();
        var yamlString = YamlSerializer.Serialize(yamlConfig);

        using var scope = new AssertionScope();
        yamlString.Should().NotBeNull().And.NotBeEmpty();
        // TBD
    }

    [Fact]
    public void DeserializationTest()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), SingleUserFilename);
        if (!Path.Exists(filePath))
        {
            return;
        }
        var yamlString = File.ReadAllText(filePath);
        var config = YamlDeserializer.Deserialize<SingleUserYamlConfig>(yamlString);
        var (actor, timeOffRequests) = config.ToDomain();
        var index = 0;

        using var scope = new AssertionScope();

        actor.Should().NotBeNull();
        actor.User.Should().NotBeNull();
        actor.StartDate.Should().NotBeNull();
        actor.StartDate.Value.Should().Be(DateTimeOffset.Parse("2023-02-13 0:00:00 +02:00"));

        actor.TimeOffSettings.Should().NotBeNull();
        actor.User.Id.Should().NotBeNull();
        actor.User.Id.Value.Should().Be(Guid.Parse("01943690-779a-7a89-8f95-fd624aa78dfc"));

        actor.User.Email.Should().NotBeNull();
        actor.User.Email.Value.Should().Be("foo@bar.com");

        actor.User.Name.Should().NotBeNull();
        actor.User.Name.Values.Should().ContainInOrder("John", "Fitzgerald", "Kennedy");

        actor.TimeOffSettings.Should().NotBeNull();

        actor.TimeOffSettings.Paid.Should().NotBeNull();
        actor.TimeOffSettings.Paid.Value.Should().Be(TimeSpan.FromDays(20));

        actor.TimeOffSettings.Unpaid.Should().NotBeNull();
        actor.TimeOffSettings.Unpaid.Value.Should().Be(TimeSpan.Zero);

        actor.TimeOffSettings.Family.Should().NotBeNull();
        actor.TimeOffSettings.Family.Value.Should().Be(TimeSpan.Zero);

        actor.TimeOffSettings.Rounding.Should().NotBeNull();
        actor.TimeOffSettings.Rounding.Value.Should().Be(TimeSpan.FromSeconds(1));

        timeOffRequests.Should().NotBeEmpty();
        timeOffRequests.Length.Should().Be(4);

        index.Should().Be(0);
        timeOffRequests[index].Type.Should().Be(TimeOffType.Paid);
        timeOffRequests[index].StartAt.Should().Be(DateTimeOffset.Parse("2023-08-01"));
        timeOffRequests[index].FinishAt.Should().Be(DateTimeOffset.Parse("2023-08-05"));

        index++;
        index.Should().Be(1);
        timeOffRequests[index].Type.Should().Be(TimeOffType.Paid);
        timeOffRequests[index].StartAt.Should().Be(DateTimeOffset.Parse("2023-10-01"));
        timeOffRequests[index].FinishAt.Should().Be(DateTimeOffset.Parse("2023-10-03"));

        index++;
        index.Should().Be(2);
        timeOffRequests[index].Type.Should().Be(TimeOffType.Unpaid);
        timeOffRequests[index].StartAt.Should().Be(DateTimeOffset.Parse("2023-11-20"));
        timeOffRequests[index].FinishAt.Should().Be(DateTimeOffset.Parse("2023-11-30"));

        index++;
        index.Should().Be(3);
        timeOffRequests[index].Type.Should().Be(TimeOffType.Family);
        timeOffRequests[index].StartAt.Should().Be(DateTimeOffset.Parse("2023-12-20"));
        timeOffRequests[index].FinishAt.Should().Be(DateTimeOffset.Parse("2023-12-24"));
    }
}