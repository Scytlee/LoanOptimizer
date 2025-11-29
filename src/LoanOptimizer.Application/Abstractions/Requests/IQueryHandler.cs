using LoanOptimizer.SharedKernel.Result;
using MediatR;

namespace LoanOptimizer.Application.Abstractions.Requests;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
