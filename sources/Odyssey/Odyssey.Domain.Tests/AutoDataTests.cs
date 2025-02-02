using System.Diagnostics.CodeAnalysis;
using AutoFixture.Xunit2;
using FluentAssertions;
using FluentAssertions.Execution;
using Odyssey.Domain.UserEntity;

namespace Odyssey.Domain.Tests;

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
}