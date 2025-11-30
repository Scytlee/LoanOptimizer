using System.Diagnostics;
using LoanOptimizer.Application;
using LoanOptimizer.Application.UseCases.RunSimulation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
    })
    .Build();

var loans = new[]
{
    new LoanData
    {
        Id = 1,
        Amount = 8000,
        YearlyInterestRate = 10.49m,
        StartDate = new DateTime(2024, 5, 2),
        FirstInstalmentDate = new DateTime(2024, 6, 10),
        NumberOfInstalments = 36,
        Overpayments = []
    },
    new LoanData
    {
        Id = 2,
        Amount = 4000,
        YearlyInterestRate = 12.99m,
        StartDate = new DateTime(2025, 9, 11),
        FirstInstalmentDate = new DateTime(2025, 10, 11),
        NumberOfInstalments = 48,
        Overpayments = []
    },
    new LoanData
    {
        Id = 3,
        Amount = 3000,
        YearlyInterestRate = 8.24m,
        StartDate = new DateTime(2025, 11, 25),
        FirstInstalmentDate = new DateTime(2025, 12, 15),
        NumberOfInstalments = 36,
        Overpayments = []
    }
};

var paymentPlan = new[]
{
    new PaymentDay { Date = new DateTime(2025, 12, 10), Type = PaymentDayType.OnlyInstalments },
    new PaymentDay { Date = new DateTime(2026, 1, 10), Type = PaymentDayType.OnlyInstalments },
    new PaymentDay { Date = new DateTime(2026, 2, 10), Type = PaymentDayType.InstalmentsAndOverpayments, TotalBudget = 3550 },
    new PaymentDay { Date = new DateTime(2026, 3, 10), Type = PaymentDayType.InstalmentsAndOverpayments, TotalBudget = 3550 },
    new PaymentDay { Date = new DateTime(2026, 4, 10), Type = PaymentDayType.InstalmentsAndOverpayments, TotalBudget = 3550 }
};

var initialState = loans.Select(loan =>
{
    var payments = loan.CalculateInstalments(loan.NumberOfInstalments, loan.FirstInstalmentDate).ToArray();
    var loanState = new LoanState { Payments = payments };
    loanState.ApplyOverpayments(loan, loan.Overpayments);
    return loanState;
}).ToArray();

var command = RunSimulationCommand.Create(loans, paymentPlan);

var sender = host.Services.GetRequiredService<ISender>();

var stopwatch = Stopwatch.StartNew();
var result = await sender.Send(command);
stopwatch.Stop();

if (result.IsSuccess)
{
    var data = result.Value;

    Console.WriteLine($"Simulation finished in {stopwatch.ElapsedMilliseconds} milliseconds");
    Console.WriteLine($"Simulation was {(data.SimulationResult.InvalidReason is null ? "valid" : "invalid")}");

    if (data.SimulationResult.InvalidReason is not null)
    {
        Console.WriteLine($"Invalid reason: {data.SimulationResult.InvalidReason}");
        return;
    }

    Console.WriteLine($"Cache hits: {LoanCache.Hits}");
    Console.WriteLine($"Cache misses: {LoanCache.Misses}");

    foreach (var (loanState, index) in data.FinalState.Select((loanState, index) => (loanState, index)))
    {
        var loan = data.Loans[index];
        Console.WriteLine($"Loan {loan.Id} - Amount: {loan.Amount:C}, Yearly interest rate: {loan.YearlyInterestRate}%, Start date: {loan.StartDate:yyyy-MM-dd}");
        var overpayments = loanState.Payments.Where(payment => payment.Type == PaymentType.Overpayment && payment.Date >= data.PaymentPlan.First().Date).ToArray();
        if (overpayments.Any())
        {
            foreach (var overpayment in overpayments)
            {
                Console.WriteLine($"Overpayment on {overpayment.Date:yyyy-MM-dd}: {overpayment.Amount:C}");
            }
        }
        else
        {
            Console.WriteLine("No overpayments");
        }
    }

    var interestPaidWithNoOverpayments = initialState.Sum(loan => loan.Payments.Last().OverallInterest);
    var interestPaidAfterSimulatedOverpayments = data.FinalState.Sum(loan => loan.Payments.Last().OverallInterest);
    var interestSaved = interestPaidWithNoOverpayments - interestPaidAfterSimulatedOverpayments;
    Console.WriteLine($"Interest paid with no overpayments: {interestPaidWithNoOverpayments:C}");
    Console.WriteLine($"Interest paid after simulated overpayments: {interestPaidAfterSimulatedOverpayments:C}");
    Console.WriteLine($"Interest saved: {interestSaved:C}");

    Console.WriteLine("Payment days:");
    for (var i = 0; i < data.PaymentPlan.Length; i++)
    {
        var currentPaymentDay = data.PaymentPlan[i];
        var nextPaymentDay = i + 1 < data.PaymentPlan.Length ? data.PaymentPlan[i + 1] : null;

        Console.WriteLine($"{currentPaymentDay.Date:yyyy-MM-dd} - Type: {currentPaymentDay.Type}, Total budget: {currentPaymentDay.TotalBudget:C}, Instalments to pay: {currentPaymentDay.InstalmentsToPay:C}, Overpayment budget: {currentPaymentDay.OverpaymentBudget:C}");
        var payments = data.FinalState.SelectMany(loanState => loanState.Payments.Where(payment => payment.Date >= currentPaymentDay.Date && (nextPaymentDay is null || payment.Date < nextPaymentDay.Date))).ToArray();
        var instalmentSum = payments.Where(payment => payment.Type == PaymentType.Instalment).Sum(payment => payment.Amount);
        var overpaymentSum = payments.Where(payment => payment.Type == PaymentType.Overpayment).Sum(payment => payment.Amount);
        Console.WriteLine($"Instalments paid: {instalmentSum}");
        Console.WriteLine($"Overpayments paid: {overpaymentSum}");
        Console.WriteLine($"Total paid: {instalmentSum + overpaymentSum}");
    }
}
else
{
    Console.WriteLine($"Error: {result.Error.Message}");
}
