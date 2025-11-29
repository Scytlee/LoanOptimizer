using LoanOptimizer.Models;
using System.Buffers;
using System.Diagnostics;

namespace LoanOptimizer;

public enum InvalidStateReason
{
    NotEnoughMoneyForInstalments,
    LeftoverPayments
}

public static class Simulator
{
    public static void RunSimulation(LoanData[] loans, LoanState[] initialState, PaymentDay[] paymentPlan)
    {
        var stopwatch = Stopwatch.StartNew();

        var simulationResult = new SimulationResult();

        // Mark all payments before the first payment day as paid
        foreach (var payment in initialState.SelectMany(loan => loan.Payments.Where(payment => payment.Date < paymentPlan.First().Date)))
        {
            payment.Paid = true;
        }

        var currentState = initialState.Select(loan => new LoanState { Payments = loan.Payments }).ToArray();

        // Iterate through all payment days
        for (var i = 0; i < paymentPlan.Length; i++)
        {
            var currentPaymentDay = paymentPlan[i];
            var nextPaymentDay = i + 1 < paymentPlan.Length ? paymentPlan[i + 1] : null;

            if (currentPaymentDay.Type is PaymentDayType.InstalmentsAndOverpayments)
            {
                var remainingAmountsToPayOff = currentState.Select((loanState, index) => loanState.CalculateAmountToPay(loans[index], currentPaymentDay.Date)).ToArray();
                // If there is enough money to pay off all loans, ignore any remaining instalments
                if (remainingAmountsToPayOff.Sum() <= currentPaymentDay.TotalBudget)
                {
                    DetermineBudget(currentState, simulationResult, currentPaymentDay, nextPaymentDay, 0);
                    HandleOverpayments(loans, currentState, simulationResult, currentPaymentDay, remainingAmountsToPayOff);
                }
                else
                {
                    var currentStateCopy = currentState.Select(loanState => new LoanState { Payments = loanState.Payments }).ToArray();

                    decimal? adjustedInstalmentsToPay = null;
                    do
                    {
                        if (adjustedInstalmentsToPay is not null)
                        {
                            currentState = currentStateCopy.Select(loanState => new LoanState { Payments = loanState.Payments }).ToArray();
                        }
                        // First iteration - calculate the most optimal overpayment strategy with assumption that all instalments will be paid
                        // Next iterations - calculate the most optimal overpayment strategy with increased overpayment budget
                        DetermineBudget(currentState, simulationResult, currentPaymentDay, nextPaymentDay, adjustedInstalmentsToPay);
                        HandleOverpayments(loans, currentState, simulationResult, currentPaymentDay, remainingAmountsToPayOff);

                        // Simulation might become invalid
                        if (simulationResult.Finished)
                        {
                            break;
                        }

                        // Overpaying a loan might lower instalments due between current payment day and the next one, or pay the loan off completely
                        // Lower instalments will allow for higher overpayments, so we need to calculate new instalment sum
                        adjustedInstalmentsToPay = currentState.SelectPaymentsInInterval(PaymentType.Instalment, currentPaymentDay.Date, nextPaymentDay?.Date).Sum(payment => payment.Amount);
                    }
                    // Overpayments affected instalments, therefore overpayment strategy has to be recalculated with higher budget
                    while (adjustedInstalmentsToPay < currentPaymentDay.InstalmentsToPay);
                }
            }

            // Update simulation state and break the loop if simulation is finished
            UpdateStateCompletion(currentState, simulationResult, currentPaymentDay, nextPaymentDay);
            if (simulationResult.Finished)
            {
                break;
            }
        }

        // After iterating through all payment days, there is a possibility that some loans are not paid off
        simulationResult.Finished = true;
        if (currentState.Any(loan => !loan.Paid))
        {
            simulationResult.InvalidReason = InvalidStateReason.LeftoverPayments;
        }

        stopwatch.Stop();
        PrintResult(stopwatch, simulationResult, currentState, loans, paymentPlan, initialState);
    }

    private static void DetermineBudget(LoanState[] currentState, SimulationResult simulationResult, PaymentDay currentPaymentDay, PaymentDay? nextPaymentDay, decimal? instalmentBudget = null)
    {
        if (simulationResult.Finished)
        {
            // Simulation is invalid, do nothing
            return;
        }

        // If instalment budget is not provided, calculate it
        instalmentBudget ??= currentState.SelectPaymentsInInterval(PaymentType.Instalment, currentPaymentDay.Date, nextPaymentDay?.Date).Sum(instalment => instalment.Amount);

        currentPaymentDay.InstalmentsToPay = instalmentBudget;

        // If overpayments are to be paid on this day, total budget must cover all instalments
        if (currentPaymentDay.Type == PaymentDayType.InstalmentsAndOverpayments)
        {
            var budget = currentPaymentDay.TotalBudget!.Value;

            // Simulation becomes invalid if there is not enough money to pay all instalments in the current interval
            if (instalmentBudget > budget)
            {
                simulationResult.Finished = true;
                simulationResult.InvalidReason = InvalidStateReason.NotEnoughMoneyForInstalments;
                return;
            }

            currentPaymentDay.OverpaymentBudget = budget - instalmentBudget;
        }
        else
        {
            currentPaymentDay.TotalBudget = instalmentBudget;
        }
    }

    private static void HandleOverpayments(LoanData[] loans, LoanState[] currentState, SimulationResult simulationResult, PaymentDay currentPaymentDay, decimal[] remainingAmountsToPayOff, decimal overpaymentStep = 25)
    {
        if (simulationResult.Finished)
        {
            // Simulation is invalid, do nothing
            return;
        }

        var unpaidLoanIndices = currentState.Select((loan, index) => (Loan: loan, Index: index)).Where(tuple => !tuple.Loan.Paid).Select(tuple => tuple.Index).ToArray();
        var unpaidLoans = unpaidLoanIndices.Select(loanIndex => currentState[loanIndex]).ToArray();
        var unpaidLoanCaches = unpaidLoanIndices.Select(loanIndex => new LoanCache(currentState[loanIndex], loans[loanIndex], currentPaymentDay.Date)).ToArray();
        var maximumOverpayments = unpaidLoanIndices.Select(loanIndex => remainingAmountsToPayOff[loanIndex]).ToArray();

        // Compute initial overpayment strategy
        // This strategy will be either the most optimal, or very close to it
        var initialOverpayments = CalculateInitialOverpayments(unpaidLoans, unpaidLoanCaches, maximumOverpayments, currentPaymentDay, overpaymentStep);

        // Adjust overpayment strategy to be the most optimal
        var adjustedOverpayments = AdjustOverpayments(unpaidLoanCaches, initialOverpayments, maximumOverpayments, overpaymentStep);

        // Apply the computed overpayment strategy
        for (var i = 0; i < unpaidLoans.Length; i++)
        {
            if (adjustedOverpayments[i] > 0)
            {
                currentState[unpaidLoanIndices[i]].ApplyOverpayment(loans[unpaidLoanIndices[i]], currentPaymentDay.Date, adjustedOverpayments[i]);
            }
        }
    }

    private static decimal[] CalculateInitialOverpayments(LoanState[] loans, LoanCache[] loanCaches, decimal[] maximumOverpayments, PaymentDay currentPaymentDay, decimal overpaymentStep)
    {
        var overpaymentBudgetLeft = currentPaymentDay.OverpaymentBudget!.Value;

        var currentMaximumOverpayments = new decimal[loans.Length];
        Array.Copy(maximumOverpayments, currentMaximumOverpayments, loans.Length);

        var overpaymentsToPerform = new decimal[loans.Length];

        while (overpaymentBudgetLeft > 0 && currentMaximumOverpayments.Any(amount => amount > 0))
        {
            var maxIndex = -1;
            var maxIndexOverpayment = 0m;
            var maxInterestLoss = decimal.MinValue;
            for (var i = 0; i < loans.Length; i++)
            {
                if (currentMaximumOverpayments[i] == 0)
                {
                    continue;
                }

                var overpayment = CalculateOverpayment(overpaymentStep, overpaymentBudgetLeft, currentMaximumOverpayments[i]);
                var interestLoss = loanCaches[i].GetOrCalculate(overpaymentsToPerform[i]).OverallInterest - loanCaches[i].GetOrCalculate(overpaymentsToPerform[i] + overpayment).OverallInterest;

                if (interestLoss <= maxInterestLoss)
                {
                    continue;
                }

                maxIndex = i;
                maxIndexOverpayment = overpayment;
                maxInterestLoss = interestLoss;
            }

            overpaymentsToPerform[maxIndex] += maxIndexOverpayment;
            overpaymentBudgetLeft -= maxIndexOverpayment;
            currentMaximumOverpayments[maxIndex] -= maxIndexOverpayment;
        }

        return overpaymentsToPerform;
    }

    public static decimal CalculateOverpayment(decimal overpaymentStep, decimal overpaymentBudget, decimal amountToPay)
    {
        if (amountToPay == 0)
        {
            return 0;
        }

        var maximumOverpayment = Math.Min(overpaymentStep, overpaymentBudget);

        return amountToPay <= maximumOverpayment ? amountToPay : maximumOverpayment;
    }

    private static decimal[] AdjustOverpayments(LoanCache[] loanCaches, decimal[] initialOverpayments, decimal[] maximumOverpayments, decimal overpaymentStep)
    {
        const decimal stepDecreaseFactor = 0.2m;
        const decimal minimumStep = 0.01m;
        var arrayPool = ArrayPool<decimal>.Shared;

        var step = CalculateNextStep(overpaymentStep);
        var bestOverpayments = initialOverpayments;

        while (step >= minimumStep)
        {
            var currentBestOverpayments = arrayPool.Rent(bestOverpayments.Length);
            var currentMinimumOverallInterest = decimal.MaxValue;
            var currentMinimumNonZeroOverpayments = int.MaxValue;

            GenerateArrays(bestOverpayments, step, maximumOverpayments, ProcessArray);

            Array.Copy(currentBestOverpayments, bestOverpayments, bestOverpayments.Length);
            arrayPool.Return(currentBestOverpayments);

            // Decrease the step size for the next iteration
            step = CalculateNextStep(step);
            continue;

            void ProcessArray(decimal[] array)
            {
                var overallInterest = Enumerable.Range(0, bestOverpayments.Length).Select(index => loanCaches[index].GetOrCalculate(array[index])).Sum(state => state.OverallInterest);
                var nonZeroOverpayments = Enumerable.Range(0, bestOverpayments.Length).Count(index => array[index] > 0);
                if (overallInterest < currentMinimumOverallInterest || overallInterest == currentMinimumOverallInterest && nonZeroOverpayments < currentMinimumNonZeroOverpayments)
                {
                    currentMinimumOverallInterest = overallInterest;
                    Array.Copy(array, currentBestOverpayments, currentBestOverpayments.Length);
                    currentMinimumNonZeroOverpayments = nonZeroOverpayments;
                }

                arrayPool.Return(array);
            }
        }

        return bestOverpayments;

        decimal CalculateNextStep(decimal currentStep)
        {
            if (currentStep == minimumStep)
            {
                return minimumStep - 0.01m;
            }

            return Math.Max(minimumStep, Math.Round(currentStep * stepDecreaseFactor, 2, MidpointRounding.AwayFromZero));
        }
    }

    private static void GenerateArrays(decimal[] initialValues, decimal step, decimal[] maxValues, Action<decimal[]> processArray)
    {
        var arrayPool = ArrayPool<decimal>.Shared;

        Backtrack(0, initialValues.Length, initialValues.Sum(), arrayPool.Rent(initialValues.Length), GeneratePossibleValues(initialValues, step, maxValues));
        return;

        void Backtrack(int index, int valueCount, decimal remainingSum, decimal[] tempArray, decimal[][] possibleValues)
        {
            if (index == valueCount && remainingSum == 0)
            {
                processArray(tempArray);
            }
            else if (index < valueCount)
            {
                foreach (var value in possibleValues[index])
                {
                    tempArray[index] = value;
                    if (remainingSum - value >= 0)
                    {
                        Backtrack(index + 1, valueCount, remainingSum - value, tempArray, possibleValues);
                    }
                }
            }
        }
    }

    private static decimal[][] GeneratePossibleValues(decimal[] initialValues, decimal step, decimal[] maxValues)
    {
        var possibleValues = new decimal[initialValues.Length][];
        for (var i = 0; i < initialValues.Length; i++)
        {
            var valueSet = new HashSet<decimal>();
            for (var j = -5; j <= 5; j++)
            {
                var value = initialValues[i] + j * step;
                value = Math.Max(0, Math.Min(value, maxValues[i]));  // Ensure value is within bounds
                valueSet.Add(value);
            }
            possibleValues[i] = valueSet.ToArray();  // Convert HashSet to array after deduplication
        }
        return possibleValues;
    }

    private static void UpdateStateCompletion(LoanState[] currentState, SimulationResult simulationResult, PaymentDay currentPaymentDay, PaymentDay? nextPaymentDay)
    {
        // Do not do anything if simulation is invalid
        if (simulationResult.Finished)
        {
            return;
        }

        if (currentPaymentDay.Type is PaymentDayType.InstalmentsAndOverpayments)
        {
            // Mark all overpayments paid on this day as paid
            var overpaymentsPaid = currentState.SelectPaymentsOnDate(PaymentType.Overpayment, currentPaymentDay.Date);
            foreach (var overpayment in overpaymentsPaid)
            {
                overpayment.Paid = true;
            }
        }

        // Mark all instalments due between current payment day and the next one as paid
        var instalmentsPaid = currentState.SelectPaymentsInInterval(PaymentType.Instalment, currentPaymentDay.Date, nextPaymentDay?.Date);
        foreach (var instalment in instalmentsPaid)
        {
            instalment.Paid = true;
        }

        // If all payments for a loan are paid, mark the loan as paid
        foreach (var loan in currentState.Where(loan => !loan.Paid))
        {
            if (loan.Payments.All(payment => payment.Paid))
            {
                loan.Paid = true;
            }
        }

        // If all loans are paid, the simulation is finished
        if (currentState.All(loan => loan.Paid))
        {
            simulationResult.Finished = true;
        }
    }

    private static void PrintResult(Stopwatch stopwatch, SimulationResult simulationResult, LoanState[] currentState, LoanData[] loans, PaymentDay[] paymentPlan, LoanState[] initialState)
    {
        Console.WriteLine($"Simulation finished in {stopwatch.ElapsedMilliseconds} milliseconds");
        Console.WriteLine($"Simulation was {(simulationResult.InvalidReason is null ? "valid" : "invalid")}");
        if (simulationResult.InvalidReason is not null)
        {
            Console.WriteLine($"Invalid reason: {simulationResult.InvalidReason}");
            return;
        }

        Console.WriteLine($"Cache hits: {LoanCache.Hits}");
        Console.WriteLine($"Cache misses: {LoanCache.Misses}");

        foreach (var (loanState, index) in currentState.Select((loanState, index) => (loanState, index)))
        {
            var loan = loans[index];
            Console.WriteLine($"Loan {loan.Id} - Amount: {loan.Amount:C}, Yearly interest rate: {loan.YearlyInterestRate}%, Start date: {loan.StartDate:yyyy-MM-dd}");
            var overpayments = loanState.Payments.Where(payment => payment.Type == PaymentType.Overpayment && payment.Date >= paymentPlan.First().Date).ToArray();
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
        var interestPaidAfterSimulatedOverpayments = currentState.Sum(loan => loan.Payments.Last().OverallInterest);
        var interestSaved = interestPaidWithNoOverpayments - interestPaidAfterSimulatedOverpayments;
        Console.WriteLine($"Interest paid with no overpayments: {interestPaidWithNoOverpayments:C}");
        Console.WriteLine($"Interest paid after simulated overpayments: {interestPaidAfterSimulatedOverpayments:C}");
        Console.WriteLine($"Interest saved: {interestSaved:C}");

        Console.WriteLine("Payment days:");
        for (var i = 0; i < paymentPlan.Length; i++)
        {
            var currentPaymentDay = paymentPlan[i];
            var nextPaymentDay = i + 1 < paymentPlan.Length ? paymentPlan[i + 1] : null;

            Console.WriteLine($"{currentPaymentDay.Date:yyyy-MM-dd} - Type: {currentPaymentDay.Type}, Total budget: {currentPaymentDay.TotalBudget:C}, Instalments to pay: {currentPaymentDay.InstalmentsToPay:C}, Overpayment budget: {currentPaymentDay.OverpaymentBudget:C}");
            var payments = currentState.SelectMany(loanState => loanState.Payments.Where(payment => payment.Date >= currentPaymentDay.Date && (nextPaymentDay is null || payment.Date < nextPaymentDay.Date))).ToArray();
            var instalmentSum = payments.Where(payment => payment.Type == PaymentType.Instalment).Sum(payment => payment.Amount);
            var overpaymentSum = payments.Where(payment => payment.Type == PaymentType.Overpayment).Sum(payment => payment.Amount);
            Console.WriteLine($"Instalments paid: {instalmentSum}");
            Console.WriteLine($"Overpayments paid: {overpaymentSum}");
            Console.WriteLine($"Total paid: {instalmentSum + overpaymentSum}");
        }
    }
}
