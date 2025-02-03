using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.JavaScript;
using FluentAssertions;
using FluentAssertions.Execution;

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
    [InlineData("2025-02-01", 12.24)]
    [InlineData("2025-03-01", 8.9)]
    public void Test_Until_PreviousPeriod_Reset(string checkpoint, decimal expected)
    {
        var withdrawal1 = BalanceRecord.Withdrawal(DateOnly.Parse("2024-05-13"), 5M);
        var withdrawal2 = BalanceRecord.Withdrawal(DateOnly.Parse("2024-08-25"), 5M);
        var withdrawal3 = BalanceRecord.Withdrawal(DateOnly.Parse("2025-01-02"), 1M);
        var withdrawal4 = BalanceRecord.Withdrawal(DateOnly.Parse("2025-02-10"), 5M);

        var checkpointDate = DateOnly.Parse(checkpoint);
        var balanceRecords = MergeBalanceRecords(checkpointDate,
            withdrawal1,
            withdrawal2,
            withdrawal3,
            withdrawal4);

        var (_, _, balance) = BalanceHandler.Handle(balanceRecords);

        balance.Should().Be(expected);
    }

    [Theory]
    // [InlineData("2024-12-01", 9.92, 0, 9.92)]
    // [InlineData("2025-01-01", 11.58, 11.58, 0)]
    // [InlineData("2025-02-01", 12.24, 10.58, 1.66)]
    // [InlineData("2025-03-01", 8.9, 5.58, 3.32)]
    // [InlineData("2025-04-01", 4.98, 0, 4.98)] // ok
    [InlineData("2025-05-01", 6.64, 0, 6.64)] // ok
    // [InlineData("2025-06-01", 8.3, 0, 8.3)]
    // [InlineData("2025-07-01", 9.96, 0, 9.96)]
    // [InlineData("2025-08-01", 11.62, 0, 11.62)]
    // [InlineData("2025-09-01", 13.28, 0, 13.28)]
    // [InlineData("2025-10-01", 14.94, 0, 14.94)]
    // [InlineData("2025-11-01", 16.6, 0, 16.6)]
    // [InlineData("2025-12-01", 18.26, 0, 18.26)]
    public void Test_Including_PreviousPeriod_Reset(string checkpoint, decimal expected, decimal previous, decimal current)
    {
        var withdrawal1 = BalanceRecord.Withdrawal(DateOnly.Parse("2024-08-25"), 10M);
        var withdrawal2 = BalanceRecord.Withdrawal(DateOnly.Parse("2025-01-02"), 1M);
        var withdrawal3 = BalanceRecord.Withdrawal(DateOnly.Parse("2025-02-10"), 5M);
        var checkpointDate = DateOnly.Parse(checkpoint);
        var resetDate = DateOnly.Parse("2025-03-31");

        var balanceRecords = MergeBalanceRecords(checkpointDate, withdrawal1, withdrawal2, withdrawal3);

        var (prev, cur, balance) = BalanceHandler.Handle(balanceRecords, resetDate);

        using var scope = new AssertionScope();
        balance.Should().Be(expected);
        previous.Should().Be(prev);
        current.Should().Be(cur);
    }

    private IReadOnlyCollection<BalanceRecord> PrepareDepositRecords()
    {
        var initialDate = DateOnly.Parse("2023-12-01");
        const decimal monthlyAmount = 1.66M;

        return Enumerable.Range(1, 100)
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
    
    public static BalanceRecord Reset(DateOnly date, decimal amount)
    {
        return new BalanceRecord(date, amount, BalanceRecordType.Reset);
    }
}

public record struct BalanceRecordType(string Name, sbyte Code)
{
    public static readonly BalanceRecordType Bonus = new BalanceRecordType("Bonus", 2);
    public static readonly BalanceRecordType Deposit = new BalanceRecordType("Deposit", 1);
    public static readonly BalanceRecordType Withdrawal = new BalanceRecordType("Withdrawal", -1);
    public static readonly BalanceRecordType Reset = new BalanceRecordType("Reset", -2);
}

public sealed record BalanceSplitResult(
    IReadOnlyCollection<BalanceRecord> PrevDeposit,
    IReadOnlyCollection<BalanceRecord> CurrentDeposit,
    IReadOnlyCollection<BalanceRecord> WithdrawalUntilReset,
    IReadOnlyCollection<BalanceRecord> WithdrawalAfterReset)
{
    public decimal PrevDepositAmount => PrevDeposit.Aggregate(0M, (current, deposit) => current + deposit.Amount);
    public decimal CurrentDepositAmount => CurrentDeposit.Aggregate(0M, (current, deposit) => current + deposit.Amount);
    public decimal WithdrawalUntilResetAmount => WithdrawalUntilReset.Aggregate(0M, (current, withdrawal) => current + withdrawal.Amount);
    public decimal WithdrawalAfterResetAmount => WithdrawalAfterReset.Aggregate(0M, (current, withdrawal) => current + withdrawal.Amount);

    public static BalanceSplitResult Empty => new BalanceSplitResult(Array.Empty<BalanceRecord>(),
        Array.Empty<BalanceRecord>(),
        Array.Empty<BalanceRecord>(),
        Array.Empty<BalanceRecord>());


    public static BalanceSplitResult Create(IReadOnlyCollection<BalanceRecord> records,
        DateOnly resetDate,
        DateOnly previousPeriodEnd)
    {
        var prevDepositRecords = records
            .Where(v =>  v.Date <= previousPeriodEnd && (v.Type == BalanceRecordType.Deposit /*|| v.Type == BalanceRecordType.Bonus*/)).ToArray();

        var currentDepositRecords = records
            .Where(v => v.Date > previousPeriodEnd && (v.Type == BalanceRecordType.Deposit /*|| v.Type == BalanceRecordType.Bonus*/)).ToArray();

        var withdrawalUntilReset = records
            .Where(v => v.Date <= resetDate && (v.Type == BalanceRecordType.Withdrawal /*|| v.Type == BalanceRecordType.Reset*/)).ToArray();

        var withdrawalAfterReset = records
            .Where(v => v.Date > resetDate && (v.Type == BalanceRecordType.Withdrawal /*|| v.Type == BalanceRecordType.Reset*/)).ToArray();

        return new BalanceSplitResult(
            prevDepositRecords,
            currentDepositRecords,
            withdrawalUntilReset,
            withdrawalAfterReset);
    }
}

public static class BalanceHandler
{
    public static (decimal prev, decimal cur, decimal balance) Handle(IReadOnlyCollection<BalanceRecord> balanceRecords, DateOnly? resetDate = null)
    {
        var result = 0M;

        if (TryGetPreviousPeriodEnd(resetDate, out var previousPeriodEnd))
        {
            // ReSharper disable once NullableWarningSuppressionIsUsed
            balanceRecords = ConsiderBalanceReset(balanceRecords, resetDate!.Value, previousPeriodEnd!.Value);
        }

        foreach (var record in balanceRecords)
        {
            if (record.Type == BalanceRecordType.Deposit || record.Type == BalanceRecordType.Bonus)
            {
                result += record.Amount;
            }
            else if (record.Type == BalanceRecordType.Withdrawal || record.Type == BalanceRecordType.Reset)
            {
                result -= record.Amount;
            }
        }

        var splitResult = SplitBalanceRecords(balanceRecords, resetDate, previousPeriodEnd);
        var resetAmount = balanceRecords.Where(v=>v.Type == BalanceRecordType.Reset).Aggregate(0M, (current, reset) => current + reset.Amount);
        var prevResult = splitResult.PrevDepositAmount - splitResult.WithdrawalUntilResetAmount - resetAmount;
        var curResult = splitResult.CurrentDepositAmount - splitResult.WithdrawalAfterResetAmount;

        return (prevResult, curResult, result);
    }

    private static bool TryGetPreviousPeriodEnd(this DateOnly? resetDate, out DateOnly? result)
    {
        if (!resetDate.HasValue)
        {
            result = null;
            return false;
        }

        result = new DateOnly(resetDate.Value.Year, 01, 01);
        return true;
    }

    private static IReadOnlyCollection<BalanceRecord> ConsiderBalanceReset(
        IReadOnlyCollection<BalanceRecord> balanceRecords, DateOnly resetDate, DateOnly previousPeriodEnd)
    {
        var splitResult =  BalanceSplitResult.Create(balanceRecords, resetDate, previousPeriodEnd);
        var resetAmount = splitResult.PrevDepositAmount - splitResult.WithdrawalUntilResetAmount;

        if (resetAmount <= 0)
        {
            return balanceRecords;
        }

        var resetRecord = BalanceRecord.Reset(resetDate, resetAmount);
        var newBalanceRecords = balanceRecords.Append(resetRecord);

        return newBalanceRecords.OrderBy(v => v.Date).ToArray();
    }


    private static BalanceSplitResult SplitBalanceRecords(IReadOnlyCollection<BalanceRecord> balanceRecords, DateOnly? resetDate, DateOnly? previousPeriodEnd)
    {
        var resetDateOrMin = resetDate ?? DateOnly.MinValue;
        var previousPeriodEndOrMin = previousPeriodEnd ?? DateOnly.MinValue;

        var splitResult =  BalanceSplitResult.Create(balanceRecords, resetDateOrMin, previousPeriodEndOrMin);

        return splitResult;
    }
}