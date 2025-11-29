using LoanOptimizer.Input;

namespace LoanOptimizer.Models;

public class LoanCache
{
    public static int Hits { get; private set; }
    public static int Misses { get; private set; }

    private readonly Dictionary<decimal, decimal> _cache = new();
    private readonly LoanState _loanState;
    private readonly LoanData _loan;
    private readonly DateTime _overpaymentDate;

    public LoanCache(LoanState loanState, LoanData loan, DateTime overpaymentDate)
    {
        _loanState = loanState;
        _loan = loan;
        _overpaymentDate = overpaymentDate;

        _cache[0] = _loanState.Payments.Last().OverallInterest;
    }

    public decimal GetOrCalculate(decimal overpayment)
    {
        if (!_cache.TryGetValue(overpayment, out var result))
        {
            result = _loanState.ComputeStateAfterOverpayments(_loan, new OverpaymentInput { Date = _overpaymentDate, Amount = overpayment });
            _cache[overpayment] = result;
            Misses++;
        }
        else
        {
            Hits++;
        }
        return result;
    }
}
