namespace LoanOptimizer.UnitTests;

public class CalculationTests
{
    [Theory]
    [InlineData(0, 100, 100, 0)] // overpaymentStep is 0
    [InlineData(50, 0, 100, 0)] // overpaymentBudget is 0
    [InlineData(50, 100, 0, 0)] // amountToPay is 0
    [InlineData(50, 100, 25, 25)] // amountToPay < overpaymentStep
    [InlineData(50, 100, 50, 50)] // amountToPay = overpaymentStep
    [InlineData(50, 100, 75, 50)] // amountToPay > overpaymentStep
    [InlineData(50, 100, 100, 50)] // amountToPay > overpaymentStep
    [InlineData(50, 25, 100, 25)] // overpaymentBudget < overpaymentStep
    [InlineData(50, 50, 100, 50)] // overpaymentBudget = overpaymentStep
    [InlineData(50, 75, 100, 50)] // overpaymentBudget > overpaymentStep
    public void CalculateOverpayment_Test(decimal overpaymentStep, decimal overpaymentBudget, decimal amountToPay, decimal expected)
    {
        // Arrange
        // (No arrangement necessary as the method under test is static)

        // Act
        var result = Simulator.CalculateOverpayment(overpaymentStep, overpaymentBudget, amountToPay);

        // Assert
        Assert.Equal(expected, result);
    }
}