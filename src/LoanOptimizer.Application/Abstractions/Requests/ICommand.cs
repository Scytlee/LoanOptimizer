using LoanOptimizer.SharedKernel.Result;
using MediatR;

namespace LoanOptimizer.Application.Abstractions.Requests;

public interface ICommand : IRequest<Result>, IBaseCommand
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand
{
}

public interface IBaseCommand
{
}
