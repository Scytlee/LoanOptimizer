using LoanOptimizer.SharedKernel.Result;
using MediatR;

namespace LoanOptimizer.Application.Abstractions.Requests;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
