using LoanOptimizer.Application.Abstractions.Requests;
using LoanOptimizer.SharedKernel.Result;

namespace LoanOptimizer.Application.UseCases.RunSimulation;

internal sealed class RunSimulationCommandHandler : ICommandHandler<RunSimulationCommand, RunSimulationResult>
{
    public Task<Result<RunSimulationResult>> Handle(RunSimulationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
