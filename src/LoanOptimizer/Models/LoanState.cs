using LoanOptimizer.Input;
using System.Diagnostics;

namespace LoanOptimizer.Models;

public class LoanState
{
    public required Payment[] Payments { get; set; }
    public bool Paid { get; set; }

    public void ApplyOverpayment(LoanData loan, DateTime date, decimal amount)
    {
        ApplyOverpayments(loan, new OverpaymentInput
        {
            Date = date,
            Amount = amount
        });
    }

    public void ApplyOverpayments(LoanData loan, params OverpaymentInput[] overpayments)
    {
        if (overpayments.Length == 0)
        {
            return;
        }

        if (Payments.Last() is { Type: PaymentType.Overpayment })
        {
            Payments = Payments.Select(payment => new Payment
            {
                Type = payment.Type,
                Date = payment.Date,
                Amount = payment.Amount,
                InterestPart = payment.InterestPart,
                PrincipalPart = payment.PrincipalPart,
                OverallInterest = payment.OverallInterest,
                RemainingPrincipal = payment.RemainingPrincipal,
                Paid = payment.Paid
            })
                                          .ToArray();
            return;
        }

        var dailyInterestRate = loan.YearlyInterestRate / 365m;

        // overpayments are ordered
        var firstOverpaymentDate = overpayments.First().Date;

        var payments = Payments.Where(payment => payment.Date < firstOverpaymentDate)
                                .Select(payment => new Payment
                                {
                                    Type = payment.Type,
                                    Date = payment.Date,
                                    Amount = payment.Amount,
                                    InterestPart = payment.InterestPart,
                                    PrincipalPart = payment.PrincipalPart,
                                    OverallInterest = payment.OverallInterest,
                                    RemainingPrincipal = payment.RemainingPrincipal,
                                    Paid = payment.Paid
                                })
                                .ToList();

        var firstInstalmentAfterFirstOverpayment = Payments.First(payment => payment.Type == PaymentType.Instalment && payment.Date >= firstOverpaymentDate);
        Payment? referenceInstalment = null;

        if (payments.Any())
        {
            referenceInstalment = payments.LastOrDefault(payment => payment.Type == PaymentType.Instalment);
        }

        var emi = referenceInstalment?.Amount ?? firstInstalmentAfterFirstOverpayment.Amount;
        var remainingPrincipal = referenceInstalment?.RemainingPrincipal ?? loan.Amount;
        var currentDate = referenceInstalment?.Date ?? loan.StartDate;
        var nextDueDate = firstInstalmentAfterFirstOverpayment.Date;
        var overallInterest = referenceInstalment?.OverallInterest ?? 0m;

        var allOverpayments = Payments.Where(payment => payment.Type == PaymentType.Overpayment)
                                       .Select(overpayment => new OverpaymentInput { Amount = overpayment.Amount, Date = overpayment.Date })
                                       .Concat(overpayments)
                                       .OrderBy(overpayment => overpayment.Date)
                                       .GroupBy(overpayment => overpayment.Date)
                                       .Select(overpaymentGroup => new OverpaymentInput
                                       {
                                           Amount = overpaymentGroup.Sum(overpayment => overpayment.Amount),
                                           Date = overpaymentGroup.First().Date
                                       })
                                       .ToArray();

        foreach (var instalment in Payments.Where(payment => payment.Type == PaymentType.Instalment && payment.Date >= firstOverpaymentDate))
        {
            var date = currentDate;
            var dueDate = nextDueDate;
            decimal interest = 0;
            var currentOverpayments = allOverpayments.Where(overpayment => overpayment.Date > date && overpayment.Date <= dueDate)
                                                     .OrderBy(overpayment => overpayment.Date)
                                                     .ToArray();
            if (currentOverpayments.Any())
            {
                foreach (var overpayment in currentOverpayments)
                {
                    var daysBeforeOverpayment = (overpayment.Date - currentDate).Days;
                    interest += remainingPrincipal * dailyInterestRate * daysBeforeOverpayment / 100;
                    if (remainingPrincipal <= overpayment.Amount)
                    {
                        if (remainingPrincipal + interest <= overpayment.Amount) // loan paid off
                        {
                            interest = Math.Round(interest, 2);
                            payments.Add(new Payment
                            {
                                Type = PaymentType.Overpayment,
                                Date = overpayment.Date,
                                Amount = remainingPrincipal + interest,
                                InterestPart = interest,
                                PrincipalPart = remainingPrincipal,
                                OverallInterest = overallInterest + interest,
                                RemainingPrincipal = 0
                            });
                            Payments = payments.ToArray();
                            return;
                        }

                        // principal paid off, interest remaining
                        var interestPaid = overpayment.Amount - remainingPrincipal;
                        overallInterest += interestPaid;
                        interest -= interestPaid;
                        payments.Add(new Payment
                        {
                            Type = PaymentType.Overpayment,
                            Date = overpayment.Date,
                            Amount = overpayment.Amount,
                            InterestPart = interestPaid,
                            PrincipalPart = remainingPrincipal,
                            OverallInterest = overallInterest,
                            RemainingPrincipal = 0
                        });
                        remainingPrincipal = 0;
                    }
                    else
                    {
                        remainingPrincipal -= overpayment.Amount;
                        payments.Add(new Payment
                        {
                            Type = PaymentType.Overpayment,
                            Date = overpayment.Date,
                            Amount = overpayment.Amount,
                            InterestPart = 0,
                            PrincipalPart = overpayment.Amount,
                            OverallInterest = overallInterest,
                            RemainingPrincipal = remainingPrincipal
                        });
                    }
                    currentDate = overpayment.Date;
                }
            }

            var remainingDays = (nextDueDate - currentDate).Days;
            interest = Math.Round(interest + remainingPrincipal * dailyInterestRate * remainingDays / 100, 2);
            overallInterest += interest;

            // Theoretically, there is a case where remaining principal is 0, but due interest is still over EMI.
            // But, realistically, this would require yearly interest rate to be over 1200%. So, I assume that this case is not possible.
            // (And if it happens, I blame the customer for taking a loan with a through-the-roof interest rate)
            if (remainingPrincipal + interest <= emi)
            {
                payments.Add(new Payment
                {
                    Type = PaymentType.Instalment,
                    Date = nextDueDate,
                    Amount = remainingPrincipal + interest,
                    InterestPart = interest,
                    PrincipalPart = remainingPrincipal,
                    OverallInterest = overallInterest,
                    RemainingPrincipal = 0,
                    Paid = instalment.Paid
                });
                Payments = payments.ToArray();
                return;
            }

            var principalPart = emi - interest;
            remainingPrincipal -= principalPart;

            payments.Add(new Payment
            {
                Type = PaymentType.Instalment,
                Date = nextDueDate,
                Amount = emi,
                InterestPart = interest,
                PrincipalPart = principalPart,
                OverallInterest = overallInterest,
                RemainingPrincipal = remainingPrincipal,
                Paid = instalment.Paid
            });

            currentDate = nextDueDate;
            nextDueDate = currentDate.AddMonths(1);
        }
    }

    public decimal ComputeStateAfterOverpayments(LoanData loan, params OverpaymentInput[] overpayments)
    {
        if (Payments.Last() is { Type: PaymentType.Overpayment })
        {
            return Payments.Last().OverallInterest;
        }

        var dailyInterestRate = loan.YearlyInterestRate / 365m;

        // overpayments are ordered
        var firstOverpaymentDate = overpayments.First().Date;

        var firstInstalmentAfterFirstOverpayment = Payments.First(payment => payment.Type == PaymentType.Instalment && payment.Date >= firstOverpaymentDate);
        var referenceInstalment = Payments.Where(payment => payment.Date < firstOverpaymentDate).LastOrDefault(payment => payment.Type == PaymentType.Instalment);

        var emi = referenceInstalment?.Amount ?? firstInstalmentAfterFirstOverpayment.Amount;
        var remainingPrincipal = referenceInstalment?.RemainingPrincipal ?? loan.Amount;
        var currentDate = referenceInstalment?.Date ?? loan.StartDate;
        var nextDueDate = firstInstalmentAfterFirstOverpayment.Date;
        var overallInterest = referenceInstalment?.OverallInterest ?? 0m;

        var allOverpayments = Payments.Where(payment => payment.Type == PaymentType.Overpayment)
                                       .Select(overpayment => new OverpaymentInput { Amount = overpayment.Amount, Date = overpayment.Date })
                                       .Concat(overpayments)
                                       .OrderBy(overpayment => overpayment.Date)
                                       .GroupBy(overpayment => overpayment.Date)
                                       .Select(overpaymentGroup => new OverpaymentInput
                                       {
                                           Amount = overpaymentGroup.Sum(overpayment => overpayment.Amount),
                                           Date = overpaymentGroup.First().Date
                                       })
                                       .ToArray();

        foreach (var _ in Payments.Where(payment => payment.Type == PaymentType.Instalment && payment.Date >= firstOverpaymentDate))
        {
            var date = currentDate;
            var dueDate = nextDueDate;
            decimal interest = 0;
            var currentOverpayments = allOverpayments.Where(overpayment => overpayment.Date > date && overpayment.Date <= dueDate)
                                                     .OrderBy(overpayment => overpayment.Date)
                                                     .ToArray();
            if (currentOverpayments.Any())
            {
                foreach (var overpayment in currentOverpayments)
                {
                    var daysBeforeOverpayment = (overpayment.Date - currentDate).Days;
                    interest += remainingPrincipal * dailyInterestRate * daysBeforeOverpayment / 100;
                    if (remainingPrincipal <= overpayment.Amount)
                    {
                        if (remainingPrincipal + interest <= overpayment.Amount) // loan paid off
                        {
                            interest = Math.Round(interest, 2);
                            return overallInterest + interest;
                        }

                        // principal paid off, interest remaining
                        var interestPaid = overpayment.Amount - remainingPrincipal;
                        overallInterest += interestPaid;
                        interest -= interestPaid;
                        remainingPrincipal = 0;
                    }
                    else
                    {
                        remainingPrincipal -= overpayment.Amount;
                    }
                    currentDate = overpayment.Date;
                }
            }

            var remainingDays = (nextDueDate - currentDate).Days;
            interest = Math.Round(interest + remainingPrincipal * dailyInterestRate * remainingDays / 100, 2);
            overallInterest += interest;

            // Theoretically, there is a case where remaining principal is 0, but due interest is still over EMI.
            // But, realistically, this would require yearly interest rate to be over 1200%. So, I assume that this case is not possible.
            // (And if it happens, I blame the customer for taking a loan with a through-the-roof interest rate)
            if (remainingPrincipal + interest <= emi)
            {
                return overallInterest;
            }

            var principalPart = emi - interest;
            remainingPrincipal -= principalPart;
            currentDate = nextDueDate;
            nextDueDate = currentDate.AddMonths(1);
        }

        throw new UnreachableException();
    }

    public decimal CalculateAmountToPay(LoanData loan, DateTime date)
    {
        if (Paid)
        {
            return 0;
        }

        var dailyInterestRate = loan.YearlyInterestRate / 365m;

        var lastInstalment = Payments.Where(payment => payment.Type == PaymentType.Instalment && payment.Date < date).MaxBy(payment => payment.Date);
        var overpayments = Payments.Where(payment => payment.Type == PaymentType.Overpayment && payment.Date > lastInstalment!.Date && payment.Date <= date)
                                    .OrderByDescending(payment => payment.Date)
                                    .ToArray();

        var remainingPrincipal = lastInstalment?.RemainingPrincipal ?? loan.Amount;
        var interest = 0m;
        var currentDate = lastInstalment?.Date ?? loan.StartDate;

        foreach (var overpayment in overpayments)
        {
            var daysBeforeOverpayment = (overpayment.Date - currentDate).Days;
            interest += remainingPrincipal * dailyInterestRate * daysBeforeOverpayment / 100;
            if (remainingPrincipal <= overpayment.Amount)
            {
                if (remainingPrincipal + interest <= overpayment.Amount) // loan paid off
                {
                    return 0;
                }

                // principal paid off, interest remaining
                interest -= overpayment.Amount - remainingPrincipal;
                remainingPrincipal = 0;
            }
            else
            {
                remainingPrincipal -= overpayment.Amount;
            }
            currentDate = overpayment.Date;
        }

        var remainingDays = (date - currentDate).Days;
        interest = Math.Round(interest + remainingPrincipal * dailyInterestRate * remainingDays / 100, 2);

        return remainingPrincipal + interest;
    }
}

public static class LoanStateExtensions
{
    public static IEnumerable<Payment> SelectPaymentsInInterval(this LoanState loanState, PaymentType paymentType, DateTime startDate, DateTime? endDate = null)
    {
        return loanState.Payments.Where(payment => payment.Type == paymentType && payment.Date >= startDate && (endDate is null || payment.Date < endDate));
    }

    public static IEnumerable<Payment> SelectPaymentsInInterval(this IEnumerable<LoanState> loanStates, PaymentType paymentType, DateTime startDate, DateTime? endDate = null)
    {
        return loanStates.SelectMany(loanState => loanState.SelectPaymentsInInterval(paymentType, startDate, endDate));
    }

    public static IEnumerable<Payment> SelectPaymentsOnDate(this IEnumerable<LoanState> loanStates, PaymentType paymentType, DateTime onDate)
    {
        return loanStates.SelectMany(loanState => loanState.Payments.Where(payment => payment.Type == paymentType && payment.Date == onDate));
    }
}
