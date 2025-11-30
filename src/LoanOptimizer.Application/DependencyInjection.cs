using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace LoanOptimizer.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Assembly marker for reflection-based registrations.
    /// </summary>
    public static Assembly ApplicationAssembly { get; } = typeof(DependencyInjection).Assembly;

    /// <summary>
    /// Registers Application layer services including MediatR.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(ApplicationAssembly);
        });

        return services;
    }
}
