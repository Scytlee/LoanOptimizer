using LoanOptimizer.Application.Abstractions.Requests;
using LoanOptimizer.SharedKernel.Result;

namespace LoanOptimizer.Application.UseCases.RunSimulation;

internal sealed class RunSimulationCommandHandler : ICommandHandler<RunSimulationCommand, RunSimulationResult>
{
    public Task<Result<RunSimulationResult>> Handle(RunSimulationCommand command, CancellationToken cancellationToken)
    {
        var initialState = command.Loans.Select(loan =>
        {
            var payments = loan.CalculateInstalments(
                loan.NumberOfInstalments,
                loan.FirstInstalmentDate).ToArray();

            var loanState = new LoanState { Payments = payments };
            loanState.ApplyOverpayments(loan, loan.Overpayments);

            return loanState;
        }).ToArray();

        var simulationResult = Simulator.RunSimulation(
            command.Loans,
            initialState,
            command.PaymentPlan);

        var result = new RunSimulationResult(
            simulationResult,
            command.Loans,
            simulationResult.FinalState,
            command.PaymentPlan);

        return Task.FromResult(Result<RunSimulationResult>.Success(result));
    }
}
