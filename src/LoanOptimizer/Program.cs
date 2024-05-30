using LoanOptimizer;
using LoanOptimizer.Input;
using LoanOptimizer.Models;
using System.Text.Json;

if (args.Length == 0)
{
    Console.WriteLine("Please specify the input JSON file path as an argument.");
    return;
}

var json = File.ReadAllText(args[0]);
ProgramInput? input;
try
{
    input = JsonSerializer.Deserialize<ProgramInput>(json);
}
catch (Exception exception)
{
    Console.WriteLine("Error deserializing JSON file: " + exception.Message);
    return;
}

if (input is null)
{
    Console.WriteLine("Error deserializing JSON file: Input is null.");
    return;
}

// Validate input
var validator = new ProgramInputValidator();
var results = validator.Validate(input);

if (!results.IsValid)
{
    foreach (var failure in results.Errors)
    {
        Console.WriteLine("Property " + failure.PropertyName + " failed validation. Error was: " + failure.ErrorMessage);
    }
    return;
}

var loans = input.Loans.Select((loanInput, index) => new LoanData
                 {
                     Id = index + 1, Amount = loanInput.Amount, YearlyInterestRate = loanInput.YearlyInterestRate, StartDate = loanInput.StartDate
                 })
                 .ToArray();

var initialState = loans.Zip(input.Loans)
                        .Select(zip =>
                        {
                            var loan = zip.First;
                            var loanInput = zip.Second;
                            var loanState = new LoanState
                            {
                                Payments = loan.CalculateInstalments(loanInput.NumberOfInstalments, loanInput.FirstInstalmentDate).ToArray()
                            };
                            loanState.ApplyOverpayments(loan, loanInput.Overpayments.ToArray());
                            return loanState;
                        })
                        .ToArray();

var paymentPlan = input.PaymentPlan.Select(paymentDayInput =>
                       {
                           var paymentDayType = paymentDayInput switch
                           {
                               { OnlyInstalments: true } => PaymentDayType.OnlyInstalments,
                               { TotalBudget: > 0 } => PaymentDayType.InstalmentsAndOverpayments,
                               _ => throw new Exception("Invalid payment day type.")
                           };

                           return new PaymentDay
                           {
                               Date = paymentDayInput.Date,
                               Type = paymentDayType,
                               TotalBudget = paymentDayType is PaymentDayType.InstalmentsAndOverpayments ? paymentDayInput.TotalBudget : null
                           };
                       })
                       .ToArray();

Simulator.RunSimulation(loans, initialState, paymentPlan);
