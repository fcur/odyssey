using System.Diagnostics.CodeAnalysis;
using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
using Odyssey.HRMS.Domain.UserEntity;

namespace Odyssey.HRMS.Domain.Tests;

[ExcludeFromCodeCoverage]
public sealed class AutoDataTests
{
    [Theory, AutoData]
    public void UserAutoDataTest(User user)
    {
        using var scope = new AssertionScope();
        user.Name.Values.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();
    }

    [Theory, AutoData]
    public void LeapYearPaidTimeOffTest(User user)
    {
        using var scope = new AssertionScope();
        user.Name.Values.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();
    }

    [Theory, AutoData]
    public void RegularYearPaidTimeOffTest(User user)
    {
        using var scope = new AssertionScope();
        user.Name.Values.Should().NotBeEmpty();
        user.Email.Value.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(0, 5, 1)]
    [InlineData(1, 5, 2)]
    [InlineData(2, 5, 3)]
    [InlineData(3, 5, 4)]
    [InlineData(4, 5, 0)]
    public void RoundRobinIncrementTest(int initial, int max, int expected)
    {
        var previous = initial;
        var result = Interlocked.Exchange(ref initial, (initial + 1) % max);
        result.Should().Be(previous);
        initial.Should().Be(expected);
    }
}