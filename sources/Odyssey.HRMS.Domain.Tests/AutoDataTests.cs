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
    [InlineData(5, 5, 1)]
    [InlineData(6, 5, 2)]
    [InlineData(7, 5, 3)]
    [InlineData(8, 5, 4)]
    [InlineData(9, 5, 0)]
    [InlineData(10, 5, 1)]
    [InlineData(11, 5, 2)]
    [InlineData(12, 5, 3)]
    [InlineData(13, 5, 4)]
    [InlineData(14, 5, 0)]
    public void RoundRobinIncrementTest(int initial, int count, int expected)
    {
        var previous = initial;
        var result = Interlocked.Exchange(ref initial, (initial + 1) % count);
        result.Should().Be(previous);
        initial.Should().Be(expected);
    }

    [Fact]
    public void MapSegmentsWithPartitionsTest()
    {
        var workingDirectory = "/home/vm/EventLogLite/test-event/";
        var existingLogSegments = Enumerable.Range(22, 3).Select(i => $"{workingDirectory}{i}.log").ToArray();
        var partitionsCount = Math.Max(5, existingLogSegments.Length);

        var newFilesCount = partitionsCount - existingLogSegments.Length;

        var files = existingLogSegments.Select(Path.GetFileNameWithoutExtension).Select(v => int.Parse(v!));
        var maxSegment = files.Max();
        var newLogSegments = Enumerable.Range(maxSegment + 1, newFilesCount).Select(v => Path.Combine(workingDirectory, $"{v}.log")).ToArray();

        var logSegments = existingLogSegments.Concat(newLogSegments).ToArray();
        var partitions = Enumerable.Range(0, partitionsCount).ToArray();
        var partitionsMap = partitions.Zip(logSegments, (k, v) => new { key = k, val = v }).ToDictionary(v => v.key, v => v.val);

        using var scope = new AssertionScope();
        partitionsMap.Keys.Should().ContainInOrder(partitions);
        partitionsMap[0].Should().Contain("22.log");
        partitionsMap[1].Should().Contain("23.log");
        partitionsMap[2].Should().Contain("24.log");
        partitionsMap[3].Should().Contain("25.log");
        partitionsMap[4].Should().Contain("26.log");
    }


    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 6)]
    [InlineData(3, 10)]
    public void ConsumerAssignmentTest(int consumersCount, int partitionsCount)
    {
        var consumers = Enumerable.Range(0, consumersCount).Select(TestConsumer.Create).ToArray();

        for (var i = 0; i < partitionsCount; i++)
        {
            var consumerIndex = i % consumersCount;

            consumers[consumerIndex] = consumers[consumerIndex].Assign(i);
        }
    }


    private readonly record struct TestConsumer(int Index, int[] Partitions)
    {
        public static TestConsumer Create(int index) => new TestConsumer(index, []);

        public TestConsumer Assign(int partition)
        {
            var partitions = new List<int>(Partitions) { partition }.ToArray();

            return this with { Partitions = partitions };
        }
    }
}