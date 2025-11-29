namespace LoanOptimizer.Models;

public record LoanData
{
    public required int Id { get; init; }
    public required decimal Amount { get; init; }
    public required decimal YearlyInterestRate { get; init; }
    public required DateTime StartDate { get; init; }
}

public static class LoanDataExtensions
{
    public static IEnumerable<Payment> CalculateInstalments(this LoanData loan, int numberOfInstalments, DateTime firstInstalmentDate)
    {
        var dailyInterestRate = loan.YearlyInterestRate / 365m;

        // Initial rough estimate
        var estimatedEmi = Math.Round(loan.Amount / numberOfInstalments, 2, MidpointRounding.AwayFromZero);

        while (true)
        {
            var remainingPrincipal = loan.Amount;
            var currentDate = loan.StartDate;
            var nextDueDate = firstInstalmentDate;

            var payments = new List<Payment>();
            var overallInterest = 0m;

            for (var i = 1; i <= numberOfInstalments; i++)
            {
                var daysInPeriod = (nextDueDate - currentDate).Days;
                var interest = Math.Round(remainingPrincipal * dailyInterestRate * daysInPeriod / 100, 2);
                overallInterest += interest;

                if (i == numberOfInstalments)
                {
                    payments.Add(new Payment
                    {
                        Type = PaymentType.Instalment,
                        Date = nextDueDate,
                        Amount = remainingPrincipal + interest,
                        InterestPart = interest,
                        PrincipalPart = remainingPrincipal,
                        OverallInterest = overallInterest,
                        RemainingPrincipal = 0
                    });
                    break;
                }

                var principalPart = estimatedEmi - interest;
                remainingPrincipal -= principalPart;

                payments.Add(new Payment
                {
                    Type = PaymentType.Instalment,
                    Date = nextDueDate,
                    Amount = estimatedEmi,
                    InterestPart = interest,
                    PrincipalPart = principalPart,
                    OverallInterest = overallInterest,
                    RemainingPrincipal = remainingPrincipal
                });
                currentDate = nextDueDate;
                nextDueDate = currentDate.AddMonths(1);
            }

            var difference = estimatedEmi - payments.Last().Amount;
            if (difference > 0 && difference < numberOfInstalments / 100m)
            {
                return payments.AsEnumerable();
            }

            // Adjust EMI proportional to the difference
            if (difference / numberOfInstalments < 0 && difference / numberOfInstalments > -0.005m)
            {
                estimatedEmi += 0.01m;
            }
            estimatedEmi = Math.Round(estimatedEmi - difference / numberOfInstalments, 2, MidpointRounding.AwayFromZero);
        }
    }
}
