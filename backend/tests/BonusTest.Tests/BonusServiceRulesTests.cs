using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using Moq;
using Xunit;

namespace BonusTest.Tests;

/// <summary>
/// Pruebas de las reglas de negocio de registro de bonos, con el repositorio simulado (Moq).
/// No tocan base de datos: solo verifican que el servicio aplique las validaciones
/// del enunciado en el orden correcto y con los mensajes esperados.
/// </summary>
public class BonusServiceRulesTests
{
    private readonly Mock<IBonusRepository> _repo = new();
    private readonly BonusService _service;

    public BonusServiceRulesTests()
    {
        _service = new BonusService(_repo.Object);
    }

    /// <summary>Empleado activo estándar para los casos felices.</summary>
    private static EmployeeLookupDto ActiveEmployee() =>
        new("D0001", "Área 1", 100, "Juan Pérez", new DateTime(2020, 1, 1), 10000m, 10m, EmployeeStates.Active);

    private static RegisterBonusRequest Request(
        decimal? amount = 1000m,
        decimal? percentage = null,
        int bonusTypeId = 1,
        string? period = null) => new(
            "D0001", 100, period ?? CurrentPeriod(), bonusTypeId, amount, percentage, "R0001", "TEST");

    /// <summary>Periodo actual en formato YYYY-MM (válido por definición).</summary>
    private static string CurrentPeriod() =>
        DateTime.UtcNow.ToString("yyyy-MM");

    /// <summary>Configura el repo simulado para un caso de registro exitoso.</summary>
    private void SetupHappyPath()
    {
        _repo.Setup(r => r.GetEmployeeAsync("D0001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveEmployee());
        _repo.Setup(r => r.HasActiveBonusAsync("D0001", 100, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.ReasonExistsActiveAsync("R0001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repo.Setup(r => r.AddAsync(It.IsAny<Bonus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        // El callback recibe el id pasado por el servicio para devolver el DTO con ese mismo id.
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new BonusListItemDto(
                id, BonusStates.Registered, "Registrado", CurrentPeriod(),
                "D0001", "Área 1", 100, "Juan Pérez", 1, "Monto fijo",
                1000m, null, 1000m, 100m, 900m, "Cumpleaños"));
    }

    /// <summary>Camino feliz: el bono se registra en estado Registrado con el cálculo correcto.</summary>
    [Fact]
    public async Task Register_success_creates_bonus_in_registered_state_with_calculated_amounts()
    {
        SetupHappyPath();


        var result = await _service.RegisterAsync(Request(), CancellationToken.None);

        Assert.Equal(BonusStates.Registered, result.BonusStateCode);
        Assert.Equal(1000m, result.GrossAmount);
        Assert.Equal(100m, result.TaxAmount);   // TaxRate 10% de 1000
        Assert.Equal(900m, result.NetAmount);
    }

    /// <summary>Regla 1: no se puede registrar un bono a un empleado Cesado.</summary>
    [Fact]
    public async Task Register_rejects_inactive_employee()
    {
        _repo.Setup(r => r.GetEmployeeAsync("D0001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveEmployee() with { EmployeeStateCode = EmployeeStates.Terminated });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RegisterAsync(Request(), CancellationToken.None));

        Assert.Equal("EMPLOYEE_INACTIVE", ex.Code);
    }

    /// <summary>Regla 2: periodo posterior al mes actual se rechaza.</summary>
    [Fact]
    public async Task Register_rejects_future_period()
    {
        _repo.Setup(r => r.GetEmployeeAsync("D0001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveEmployee());

        var future = DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM");
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RegisterAsync(Request(period: future), CancellationToken.None));

        Assert.Equal("INVALID_PERIOD", ex.Code);
    }

    /// <summary>Regla 3: un empleado no puede tener más de un bono activo en el mismo periodo.</summary>
    [Fact]
    public async Task Register_rejects_duplicate_active_bonus_in_same_period()
    {
        _repo.Setup(r => r.GetEmployeeAsync("D0001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveEmployee());
        _repo.Setup(r => r.HasActiveBonusAsync("D0001", 100, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RegisterAsync(Request(), CancellationToken.None));

        Assert.Equal("DUPLICATE_BONUS", ex.Code);
    }

    /// <summary>Regla 4: solo se aceptan motivos activos.</summary>
    [Fact]
    public async Task Register_rejects_inactive_reason()
    {
        _repo.Setup(r => r.GetEmployeeAsync("D0001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveEmployee());
        _repo.Setup(r => r.HasActiveBonusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.ReasonExistsActiveAsync("R0001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RegisterAsync(Request(), CancellationToken.None));

        Assert.Equal("INVALID_REASON", ex.Code);
    }

    /// <summary>Regla 5a: el monto fijo no puede ser mayor al sueldo mensual.</summary>
    [Fact]
    public async Task Register_rejects_amount_greater_than_salary()
    {
        _repo.Setup(r => r.GetEmployeeAsync("D0001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveEmployee());
        _repo.Setup(r => r.HasActiveBonusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.ReasonExistsActiveAsync("R0001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RegisterAsync(Request(amount: 15000m), CancellationToken.None));

        Assert.Equal("INVALID_AMOUNT", ex.Code);
    }

    /// <summary>Regla 5b: el monto fijo debe ser mayor a 0.</summary>
    [Fact]
    public async Task Register_rejects_zero_amount()
    {
        _repo.Setup(r => r.GetEmployeeAsync("D0001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveEmployee());
        _repo.Setup(r => r.HasActiveBonusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.ReasonExistsActiveAsync("R0001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RegisterAsync(Request(amount: 0m), CancellationToken.None));

        Assert.Equal("INVALID_AMOUNT", ex.Code);
    }

    /// <summary>Regla 5c: el porcentaje debe ser mayor a 0 y hasta 100.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Register_rejects_invalid_percentage(decimal percentage)
    {
        _repo.Setup(r => r.GetEmployeeAsync("D0001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveEmployee());
        _repo.Setup(r => r.HasActiveBonusAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.ReasonExistsActiveAsync("R0001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.RegisterAsync(Request(bonusTypeId: 2, amount: null, percentage: percentage), CancellationToken.None));

        Assert.Equal("INVALID_PERCENTAGE", ex.Code);
    }

    /// <summary>Un bono en estado Pagado no puede volver a pagarse.</summary>
    [Fact]
    public async Task Pay_rejects_bonus_already_paid()
    {
        var bonus = new Bonus
        {
            BonusId = 1,
            BonusStateCode = BonusStates.Paid,
            NetAmount = 900m,
            IsDeleted = false,
        };
        _repo.Setup(r => r.GetEntityAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(bonus);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PayAsync(new PayBonusRequest(1, [new BonusPaymentDetailItem("Proyecto A", 10, 900m)], "TEST"),
                CancellationToken.None));

        Assert.Equal("INVALID_STATE", ex.Code);
    }

    /// <summary>Punto extra 1: la suma de los montos debe ser exactamente el neto del bono.</summary>
    [Fact]
    public async Task Pay_rejects_amount_mismatch()
    {
        var bonus = new Bonus
        {
            BonusId = 1,
            BonusStateCode = BonusStates.Registered,
            NetAmount = 900m,
            IsDeleted = false,
        };
        _repo.Setup(r => r.GetEntityAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(bonus);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.PayAsync(new PayBonusRequest(1, [new BonusPaymentDetailItem("Proyecto A", 10, 800m)], "TEST"),
                CancellationToken.None));

        Assert.Equal("AMOUNT_MISMATCH", ex.Code);
    }

    /// <summary>Pago exitoso: cambia el estado a Pagado y persiste la distribución.</summary>
    [Fact]
    public async Task Pay_success_marks_bonus_paid_and_saves_details()
    {
        var bonus = new Bonus
        {
            BonusId = 1,
            BonusStateCode = BonusStates.Registered,
            NetAmount = 900m,
            IsDeleted = false,
        };
        _repo.Setup(r => r.GetEntityAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(bonus);
        _repo.Setup(r => r.MarkPaidAsync(It.IsAny<Bonus>(), It.IsAny<List<BonusPaymentDetail>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.GetPaymentDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BonusPaymentDetail { BonusId = 1, ProjectName = "Proyecto A", Hours = 10, AllocatedAmount = 900m }]);
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BonusListItemDto(1, BonusStates.Paid, "Pagado", "2026-01", "D0001", "Área 1", 100,
                "Juan Pérez", 1, "Monto fijo", 1000m, null, 1000m, 100m, 900m, "Cumpleaños"));

        var result = await _service.PayAsync(
            new PayBonusRequest(1, [new BonusPaymentDetailItem("Proyecto A", 10, 900m)], "TEST"),
            CancellationToken.None);

        // El estado cambió a Pagado y la auditoría quedó registrada.
        Assert.Equal(BonusStates.Paid, bonus.BonusStateCode);
        Assert.Equal("TEST", bonus.ModifiedBy);
        Assert.Single(result.Details);
        Assert.Equal("Proyecto A", result.Details[0].ProjectName);
    }
}
