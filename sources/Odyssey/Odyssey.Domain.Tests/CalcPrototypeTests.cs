using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

namespace Odyssey.Domain.Tests;

[ExcludeFromCodeCoverage]
public sealed class CalcPrototypeTests
{
    private readonly IReadOnlyCollection<BalanceRecord> _depositRecords;

    public CalcPrototypeTests()
    {
        _depositRecords = PrepareDepositRecords();
    }

    [Theory]
    [InlineData("2024-01-01", 1.66)]
    [InlineData("2024-02-01", 3.32)]
    [InlineData("2024-03-01", 4.98)]
    [InlineData("2024-04-01", 6.64)]
    [InlineData("2024-05-01", 8.3)]
    [InlineData("2024-06-01", 4.96)]
    [InlineData("2024-07-01", 6.62)]
    [InlineData("2024-08-01", 8.28)]
    [InlineData("2024-09-01", 4.94)]
    [InlineData("2024-10-01", 6.6)]
    [InlineData("2024-11-01", 8.26)]
    [InlineData("2024-12-01", 9.92)]
    [InlineData("2025-01-01", 11.58)]
    public void Test1(string checkpoint, decimal expected)
    {
        var withdrawal1 = BalanceRecord.Withdrawal(DateOnly.Parse("2024-05-13"), 5M);
        var withdrawal2 = BalanceRecord.Withdrawal(DateOnly.Parse("2024-08-25"), 5M);

        var checkpointDate = DateOnly.Parse(checkpoint);
        var balanceRecords = MergeBalanceRecords(checkpointDate, withdrawal1, withdrawal2);

        var balance = BalanceHandler.Handle(balanceRecords);

        balance.Should().Be(expected);
    }

    private IReadOnlyCollection<BalanceRecord> PrepareDepositRecords()
    {
        var initialDate = DateOnly.Parse("2023-12-01");
        var monthlyAmount = 1.66M;

        return Enumerable.Range(1, 17)
            .Select(v => BalanceRecord.Deposit(initialDate.AddMonths(v), monthlyAmount)).ToArray();
    }

    private IReadOnlyCollection<BalanceRecord> MergeBalanceRecords(DateOnly maxDate, params BalanceRecord[] records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var collection = new List<BalanceRecord>(records);
        collection.AddRange(_depositRecords);

        return collection.Where(v => v.Date <= maxDate).OrderBy(v => v.Date).ToArray();
    }
}

public sealed record BalanceRecord(DateOnly Date, decimal Amount, BalanceRecordType Type)
{
    public static BalanceRecord Deposit(DateOnly date, decimal amount)
    {
        return new BalanceRecord(date, amount, BalanceRecordType.Deposit);
    }

    public static BalanceRecord Withdrawal(DateOnly date, decimal amount)
    {
        return new BalanceRecord(date, amount, BalanceRecordType.Withdrawal);
    }
}

public record struct BalanceRecordType(string Name, sbyte Code)
{
    public static BalanceRecordType Deposit = new BalanceRecordType("Deposit", 1);
    public static BalanceRecordType Withdrawal = new BalanceRecordType("Withdrawal", -1);
}

public static class BalanceHandler
{
    public static decimal Handle(IReadOnlyCollection<BalanceRecord> balanceRecords)
    {
        var result = 0M;

        foreach (var record in balanceRecords)
        {
            if (record.Type == BalanceRecordType.Deposit)
            {
                result += record.Amount;
            }
            else if (record.Type == BalanceRecordType.Withdrawal)
            {
                result -= record.Amount;
            }
        }


        return result;
    }
}