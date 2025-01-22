using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Odyssey.Domain.EmployeeEntity;
using Odyssey.Domain.UserEntity;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Odyssey.Domain.Tests;

[ExcludeFromCodeCoverage]
public sealed class SingleUserTests
{
    private const string SingleUserFilename = "single-user.yaml";
    private static readonly ISerializer YamlSerializer;
    private static readonly IDeserializer YamlDeserializer;

    static SingleUserTests()
    {
        var serializerBuilder = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
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
        var user = User.Create(UserId.Empty, userName, email);
        var leaveSettings = LeaveSettings.CreateDefault();
        var startDate = new StartDate(DateTimeOffset.Parse("2023-02-13"));
        var actor = Employee.Create(EmployeeId.Empty, user, startDate, leaveSettings);
        var dates = new[] { "2023-08-01", "2023-08-05", "2023-10-01", "2023-10-03", "2023-11-20", "2023-11-30" }
            .Select(DateTimeOffset.Parse).ToArray();
        var index = 0;

        var timeOffRequests = new[]
        {
            TimeOffRequest.CreatePaidTimeOffRequest(TimeOffRequestSettings.Create(dates[index++], dates[index++]).GetValueOrDefault()),
            TimeOffRequest.CreatePaidTimeOffRequest(TimeOffRequestSettings.Create(dates[index++], dates[index++]).GetValueOrDefault()),
            TimeOffRequest.CreateUnpaidTimeOffRequest(TimeOffRequestSettings.Create(dates[index++], dates[index++]).GetValueOrDefault())
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
        Func<ILeaveDetails?, bool> checkRecurringSettings = (details) =>
        {
            var settings = details as RecurringTimeOffSettings;
            return settings is { Accrual: { Policy.Name: TimeOffAccrualPolicy.YearlyTypeName, Value.Days: 20 } };
        };
        Func<ILeaveDetails?, bool> checkBankHolidaySettings = (details) =>
        {
            var settings = details as BankHolidaySettings;
            return settings is { Days.Length: 1 };
        };
        
        using var scope = new AssertionScope();

        actor.Should().NotBeNull();
        actor.User.Should().NotBeNull();
        actor.StartDate.Should().NotBeNull();
        actor.StartDate.Value.Should().Be(DateTimeOffset.Parse("2023-02-13 0:00:00 +02:00"));

        actor.LeaveSettings.Should().NotBeNull();

        actor.User.Email.Should().NotBeNull();
        actor.User.Email.Value.Should().Be("foo@bar.com");

        actor.User.Name.Should().NotBeNull();
        actor.User.Name.Values.Should().ContainInOrder("John", "Fitzgerald", "Kennedy");

        actor.LeaveSettings.Count.Should().Be(4);
        actor.LeaveSettings.Should().ContainSingle(v => v.Type == LeaveType.PaidTimeOff && v.Details is RecurringTimeOffSettings && checkRecurringSettings(v.Details));
        actor.LeaveSettings.Should().ContainSingle(v => v.Type == LeaveType.UnpaidTimeOff && checkRecurringSettings(v.Details));
        actor.LeaveSettings.Should().ContainSingle(v => v.Type == LeaveType.SickLeave && v.Details == null);
        actor.LeaveSettings.Should().ContainSingle(v => v.Type == LeaveType.BankHoliday && checkBankHolidaySettings(v.Details));

        timeOffRequests.Should().NotBeEmpty();
        timeOffRequests.Length.Should().Be(3);

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
    }

    [Fact]
    public void LeaveSettingsSerializationTest()
    {
        var leaveSettings = LeaveSettings.CreateDefault();
        var yamlConfig = leaveSettings.Select(v => v.ToConfig()).ToArray();
        var yamlConfigString = YamlSerializer.Serialize(yamlConfig);
    }
}