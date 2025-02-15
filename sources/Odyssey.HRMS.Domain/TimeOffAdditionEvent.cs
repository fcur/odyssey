using Odyssey.HRMS.Domain.EmployeeEntity;

namespace Odyssey.HRMS.Domain;

public sealed record TimeOffAccrualResult(LeaveType Type, TimeSpan Duration);

// public interface ITimeOffAdditionEvent
// {
//     TimeOffType Type { get; }
//     TimeOffDuration Duration { get; }
// }
//
// public sealed record PaidTimeOffAdditionEvent(TimeOffDuration Duration, TimeOffType Type) : ITimeOffAdditionEvent
// {
//     public static ITimeOffAdditionEvent Create(TimeAccrual timeAccrual)
//     {
//         var timeOffDuration = new TimeOffDuration(timeAccrual.Ticks);
//         return new PaidTimeOffAdditionEvent(timeOffDuration, TimeOffType.Paid);
//     }
// }
//
// public sealed record UnPaidTimeOffAdditionEvent(TimeOffDuration Duration, TimeOffType Type) : ITimeOffAdditionEvent
// {
//     public static ITimeOffAdditionEvent Create(TimeOffDuration duration)
//     {
//         return new UnPaidTimeOffAdditionEvent(duration, TimeOffType.Unpaid);
//     }
// }

// public sealed record TimeOffDuration(double Ticks)
// {
//     private TimeSpan Duration => TimeSpan.FromTicks((long)Ticks);
// }