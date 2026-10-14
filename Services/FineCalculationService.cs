
namespace LibraryManagement.Api.Services;

public sealed class FineCalculationService
{
    public const int GracePeriodDays = 7;
    public const decimal FinePerDay = 10m;

    public (int OverdueDays, decimal FineAmount) Calculate(
        DateTime dueDate,
        DateTime returnRequestDate)
    {
        var daysPastDue = (
            returnRequestDate.Date - dueDate.Date
        ).Days;

        var overdueDays = Math.Max(
            0,
            daysPastDue - GracePeriodDays
        );

        var fineAmount = overdueDays * FinePerDay;

        return (overdueDays, fineAmount);
    }
}
