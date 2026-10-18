using BonusTest.Application.Bonuses;
using Xunit;

namespace BonusTest.Tests;

/// <summary>
/// Pruebas unitarias del cálculo del bono (regla principal del enunciado):
///   Bruto      = monto fijo, o sueldo × porcentaje / 100
///   Retención  = Bruto × TaxRate / 100
///   Neto       = Bruto − Retención
/// con redondeo a 2 decimales, mitad hacia arriba.
/// El método Calculate es estático y puro, así que se puede probar sin BD ni mocks.
/// </summary>
public class BonusCalculationTests
{
    private const int FixedAmount = 1;
    private const int SalaryPercentage = 2;

    // Empleado de prueba con TaxRate conocido para verificar la retención.
    private const decimal Salary = 10000m;
    private const decimal TaxRate = 10m;

    /// <summary>Bono de monto fijo: bruto = monto, retención = 10% del bruto.</summary>
    [Fact]
    public void Fixed_amount_gross_equals_amount_and_tax_applies_rate()
    {
        var (gross, tax, net) = BonusService.Calculate(Salary, FixedAmount, 1000m, null, TaxRate);

        Assert.Equal(1000m, gross);
        Assert.Equal(100m, tax);   // 10% de 1000
        Assert.Equal(900m, net);   // 1000 − 100
    }

    /// <summary>Bono por porcentaje: bruto = sueldo × %/100.</summary>
    [Fact]
    public void Percentage_gross_is_salary_times_percentage()
    {
        var (gross, tax, net) = BonusService.Calculate(Salary, SalaryPercentage, null, 5m, TaxRate);

        Assert.Equal(500m, gross);  // 10000 × 5% = 500
        Assert.Equal(50m, tax);     // 10% de 500
        Assert.Equal(450m, net);
    }

    /// <summary>El porcentaje 100% del sueldo da un bruto igual al sueldo.</summary>
    [Fact]
    public void Percentage_100_equals_full_salary()
    {
        var (gross, tax, net) = BonusService.Calculate(Salary, SalaryPercentage, null, 100m, TaxRate);

        Assert.Equal(10000m, gross);
        Assert.Equal(1000m, tax);
        Assert.Equal(9000m, net);
    }

    /// <summary>
    /// Caso de redondeo clave del enunciado: 0.125 debe redondear a 0.13
    /// (half-up), no a 0.12 como haría el redondeo bancario del doble binario.
    /// </summary>
    [Theory]
    [InlineData(0.125, 0.13)]
    [InlineData(0.135, 0.14)]
    [InlineData(123.005, 123.01)]
    [InlineData(1.004, 1.00)] // menor a la mitad del centavo se redondea hacia abajo
    public void Rounding_is_half_up_to_two_decimals(decimal raw, decimal expected)
    {
        Assert.Equal(expected, BonusService.RoundHalfUp(raw));
    }

    /// <summary>Porcentaje con decimales: 7.5% de 3333.33 = 250.00 (redondeado).</summary>
    [Fact]
    public void Percentage_with_decimals_rounds_half_up()
    {
        var (gross, tax, net) = BonusService.Calculate(3333.33m, SalaryPercentage, null, 7.5m, TaxRate);

        Assert.Equal(250.00m, gross);      // 3333.33 × 7.5% = 249.99975 → 250.00
        Assert.Equal(25.00m, tax);         // 250 × 10% = 25
        Assert.Equal(225.00m, net);
    }

    /// <summary>TaxRate con decimales: retención de 16.5% sobre bruto de 1000 = 165.</summary>
    [Fact]
    public void Tax_rate_with_decimals_rounds_half_up()
    {
        var (gross, tax, net) = BonusService.Calculate(Salary, FixedAmount, 1000m, null, 16.5m);

        Assert.Equal(1000m, gross);
        Assert.Equal(165m, tax);
        Assert.Equal(835m, net);
    }
}
