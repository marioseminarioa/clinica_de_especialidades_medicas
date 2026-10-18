using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;

namespace BonusTest.Application.Bonuses;

/// <summary>
/// Reglas de negocio del módulo de bonos. No hay reglas en el controlador:
/// todo pasa por aquí y la persistencia se delega en IBonusRepository.
/// </summary>
public class BonusService(IBonusRepository repository)
{
    /// <summary>Listado paginado con filtros (área, número de empleado, periodo, estado).</summary>
    public Task<PagedResponse<BonusListItemDto>> SearchAsync(BonusSearchRequest request, CancellationToken cancellationToken) =>
        repository.SearchAsync(request, cancellationToken);

    /// <summary>
    /// Búsqueda del empleado por área + número (la regla de estado activo ya se
    /// aplica en <see cref="GetEmployeeActiveAsync"/>, que también usa el registro).
    /// </summary>
    public async Task<EmployeeLookupDto> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        await GetEmployeeActiveAsync(departmentCode, employeeNumber, cancellationToken);

    /// <summary>Registra un bono aplicando todas las validaciones de las reglas de negocio.</summary>
    public async Task<BonusListItemDto> RegisterAsync(RegisterBonusRequest request, CancellationToken cancellationToken)
    {
        // 1. El empleado debe existir y estar Activo (0101); un Cesado devuelve error.
        var employee = await GetEmployeeActiveAsync(request.DepartmentCode, request.EmployeeNumber, cancellationToken);

        // 2. El periodo no puede ser posterior al mes actual.
        ValidatePeriod(request.Period);

        // 3. Un empleado no puede tener más de un bono activo en el mismo periodo.
        if (await repository.HasActiveBonusAsync(request.DepartmentCode, request.EmployeeNumber, request.Period, cancellationToken))
            throw new BusinessException("DUPLICATE_BONUS", $"El empleado {request.DepartmentCode}-{request.EmployeeNumber} ya tiene un bono activo para el periodo {request.Period}.");

        // 4. Motivo: solo motivos activos (se valida por catálogo, no aquí).
        await ValidateReasonAsync(request.ReasonCode, cancellationToken);

        // 5. Monto fijo: mayor a 0 y no mayor al sueldo mensual. Porcentaje: mayor a 0 y hasta 100.
        var (amount, percentage) = ValidateValue(request.BonusTypeId, request.Amount, request.AmountPercentage, employee.MonthlySalary);

        // Cálculo del bono (redondeo half-up a 2 decimales).
        var (gross, tax, net) = Calculate(employee.MonthlySalary, request.BonusTypeId, amount, percentage, employee.TaxRate);

        // 6. Auditoría: quién y cuándo creó el registro.
        var bonus = new Domain.Entities.Bonus
        {
            DepartmentCode = request.DepartmentCode,
            EmployeeNumber = request.EmployeeNumber,
            Period = request.Period,
            BonusTypeId = request.BonusTypeId,
            Amount = amount,
            AmountPercentage = percentage,
            GrossAmount = gross,
            TaxAmount = tax,
            NetAmount = net,
            // Todo bono nace en Registrado (0201).
            BonusStateCode = BonusStates.Registered,
            ReasonCode = request.ReasonCode,
            IsDeleted = false,
            // Auditoría: el UserCode siempre viene del header (lo colocó el controlador), nunca del JSON.
            CreatedBy = request.UserCode!,
            CreatedAt = DateTime.UtcNow,
        };

        var bonusId = await repository.AddAsync(bonus, cancellationToken);
        // Se devuelve la fila completa lista para insertarse en el listado sin reconsultar.
        return (await repository.GetByIdAsync(bonusId, cancellationToken))!;
    }

    /// <summary>
    /// Punto extra 1: registra el pago de un bono con distribución por proyectos.
    /// Reglas:
    ///   - Solo un bono en estado Registrado (0201) puede pasar a Pagado (0202).
    ///   - La distribución requiere al menos un proyecto.
    ///   - Cada línea debe tener nombre, horas > 0 y monto > 0.
    ///   - La suma de los montos debe ser exactamente igual al neto del bono.
    ///   - El estado pasa a Pagado y la distribución queda persistida.
    /// </summary>
    public async Task<PayBonusResultDto> PayAsync(PayBonusRequest request, CancellationToken cancellationToken)
    {
        // El bono debe existir y no estar eliminado lógicamente.
        var bonus = await GetActiveBonusAsync(request.BonusId, cancellationToken);

        // Solo un bono en estado Registrado puede pasar a Pagado.
        if (bonus.BonusStateCode != BonusStates.Registered)
            throw new BusinessException("INVALID_STATE", "Solo un bono en estado Registrado puede ser pagado.");

        // Debe existir al menos una línea de distribución por proyecto.
        if (request.Details is null || request.Details.Count == 0)
            throw new BusinessException("EMPTY_DETAILS", "El pago requiere al menos un proyecto con horas y monto.");

        // Cada línea debe ser válida: nombre, horas positivas y monto positivo.
        foreach (var detail in request.Details)
        {
            if (string.IsNullOrWhiteSpace(detail.ProjectName))
                throw new BusinessException("INVALID_PROJECT", "El nombre del proyecto es obligatorio.");
            if (detail.Hours <= 0)
                throw new BusinessException("INVALID_HOURS", $"Las horas del proyecto '{detail.ProjectName}' deben ser mayores a 0.");
            if (detail.Amount <= 0)
                throw new BusinessException("INVALID_AMOUNT", $"El monto del proyecto '{detail.ProjectName}' debe ser mayor a 0.");
        }

        // La suma de los montos debe ser exactamente igual al neto del bono.
        var total = request.Details.Sum(d => d.Amount);
        if (total != bonus.NetAmount)
            throw new BusinessException("AMOUNT_MISMATCH", $"La suma de los montos ({total}) debe ser exactamente igual al neto del bono ({bonus.NetAmount}).");

        // Se marcan el estado, la auditoría y se guardan las líneas en una transacción.
        bonus.BonusStateCode = BonusStates.Paid;
        TouchAudit(bonus, request.UserCode!);
        var details = request.Details
            .Select(d => new Domain.Entities.BonusPaymentDetail
            {
                BonusId = bonus.BonusId,
                ProjectName = d.ProjectName.Trim(),
                Hours = d.Hours,
                AllocatedAmount = d.Amount,
            })
            .ToList();

        await repository.MarkPaidAsync(bonus, details, cancellationToken);

        // Se recupera la distribución guardada para devolverla al cliente.
        var saved = await repository.GetPaymentDetailsAsync(bonus.BonusId, cancellationToken);
        return new PayBonusResultDto(
            (await repository.GetByIdAsync(bonus.BonusId, cancellationToken))!,
            saved.Select(d => new BonusPaymentDetailDto(d.BonusPaymentDetailId, d.ProjectName, d.Hours, d.AllocatedAmount)).ToList()
        );
    }

    /// <summary>Devuelve la distribución por proyectos con la que fue pagado un bono (punto extra 1).</summary>
    public async Task<IReadOnlyList<BonusPaymentDetailDto>> GetPaymentDetailsAsync(int bonusId, CancellationToken cancellationToken)
    {
        var details = await repository.GetPaymentDetailsAsync(bonusId, cancellationToken);
        return details.Select(d => new BonusPaymentDetailDto(d.BonusPaymentDetailId, d.ProjectName, d.Hours, d.AllocatedAmount)).ToList();
    }

    /// <summary>Anula un bono. Regla: solo en estado Registrado (0201) se puede anular → 0203.</summary>
    public async Task<BonusListItemDto> CancelAsync(int bonusId, string userCode, CancellationToken cancellationToken)
    {
        var bonus = await GetActiveBonusAsync(bonusId, cancellationToken);

        // Solo en estado Registrado se puede anular.
        if (bonus.BonusStateCode != BonusStates.Registered)
            throw new BusinessException("INVALID_STATE", "Solo un bono en estado Registrado puede ser anulado.");

        bonus.BonusStateCode = BonusStates.Cancelled;
        TouchAudit(bonus, userCode);
        await repository.UpdateAsync(bonus, cancellationToken);
        return (await repository.GetByIdAsync(bonusId, cancellationToken))!;
    }

    /// <summary>Elimina de forma lógica. Regla: solo en estado Registrado se puede eliminar.</summary>
    public async Task DeleteAsync(int bonusId, string userCode, CancellationToken cancellationToken)
    {
        var bonus = await GetActiveBonusAsync(bonusId, cancellationToken);

        // Solo en estado Registrado se puede eliminar.
        if (bonus.BonusStateCode != BonusStates.Registered)
            throw new BusinessException("INVALID_STATE", "Solo un bono en estado Registrado puede ser eliminado.");

        // Eliminar es lógico: no se borra de la tabla, deja de aparecer en el listado.
        bonus.IsDeleted = true;
        TouchAudit(bonus, userCode);
        await repository.UpdateAsync(bonus, cancellationToken);
    }

    /// <summary>Carga el bono por id; si no existe o está eliminado lógicamente, no es operativo.</summary>
    private async Task<Domain.Entities.Bonus> GetActiveBonusAsync(int bonusId, CancellationToken cancellationToken) =>
        await repository.GetEntityAsync(bonusId, cancellationToken)
            ?? throw new BusinessException("BONUS_NOT_FOUND", "El bono no existe o ya fue eliminado.");

    /// <summary>Carga el empleado y valida que esté Activo (0101); un Cesado devuelve error de validación.</summary>
    /// <remarks>Reutiliza la búsqueda del empleado que usa el modal; así las reglas de estado se aplican en un solo lugar.</remarks>
    private async Task<EmployeeLookupDto> GetEmployeeActiveAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeAsync(departmentCode, employeeNumber, cancellationToken)
            ?? throw new BusinessException("EMPLOYEE_NOT_FOUND", "No existe un empleado con el área y número indicados.");

        if (employee.EmployeeStateCode != EmployeeStates.Active)
            throw new BusinessException("EMPLOYEE_INACTIVE", "Solo se pueden registrar bonos a empleados en estado Activo.");

        return employee;
    }

    /// <summary>El periodo viene en formato 'YYYY-MM' y no puede ser posterior al mes actual.</summary>
    private static void ValidatePeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period) || period.Length != 7 || period[4] != '-')
            throw new BusinessException("INVALID_PERIOD", "El periodo debe tener el formato YYYY-MM.");

        if (!DateOnly.TryParseExact($"{period}-01", "yyyy-MM-dd", out var periodDate))
            throw new BusinessException("INVALID_PERIOD", "El periodo no es una fecha válida.");

        // El periodo no puede ser posterior al mes actual.
        var currentMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        if (periodDate > currentMonth)
            throw new BusinessException("INVALID_PERIOD", "El periodo no puede ser posterior al mes actual.");
    }

    /// <summary>Solo se aceptan motivos activos (State = 1) según el catálogo.</summary>
    private async Task ValidateReasonAsync(string reasonCode, CancellationToken cancellationToken)
    {
        if (!await repository.ReasonExistsActiveAsync(reasonCode, cancellationToken))
            throw new BusinessException("INVALID_REASON", "El motivo indicado no existe o está inactivo.");
    }

    /// <summary>
    /// Valida el valor según el tipo: monto fijo (0, sueldo mensual] o porcentaje (0, 100].
    /// Devuelve el valor normalizado en el campo correspondiente.
    /// </summary>
    private static (decimal? amount, decimal? percentage) ValidateValue(int bonusTypeId, decimal? amount, decimal? percentage, decimal monthlySalary)
    {
        if (bonusTypeId == BonusTypes.FixedAmount)
        {
            if (amount is not > 0)
                throw new BusinessException("INVALID_AMOUNT", "El monto del bono debe ser mayor a 0.");
            if (amount > monthlySalary)
                throw new BusinessException("INVALID_AMOUNT", "El monto del bono no puede ser mayor al sueldo mensual.");
            return (amount, null);
        }

        if (bonusTypeId == BonusTypes.SalaryPercentage)
        {
            if (percentage is not > 0)
                throw new BusinessException("INVALID_PERCENTAGE", "El porcentaje del bono debe ser mayor a 0.");
            if (percentage > 100)
                throw new BusinessException("INVALID_PERCENTAGE", "El porcentaje del bono no puede ser mayor a 100.");
            return (null, percentage);
        }

        throw new BusinessException("INVALID_BONUS_TYPE", "El tipo de bono indicado no es válido.");
    }

    /// <summary>
    /// Cálculo del bono con redondeo a 2 decimales, mitad hacia arriba (0.125 → 0.13):
    ///   Bruto      = monto fijo, o sueldo × porcentaje / 100
    ///   Retención  = Bruto × TaxRate / 100
    ///   Neto       = Bruto − Retención
    /// </summary>
    public static (decimal gross, decimal tax, decimal net) Calculate(decimal monthlySalary, int bonusTypeId, decimal? amount, decimal? percentage, decimal taxRate)
    {
        // Bruto: monto fijo o proporción del sueldo, redondeado half-up.
        var gross = bonusTypeId == BonusTypes.FixedAmount
            ? RoundHalfUp(amount!.Value)
            : RoundHalfUp(monthlySalary * percentage!.Value / 100m);

        // Retención sobre el bruto según el % del empleado.
        var tax = RoundHalfUp(gross * taxRate / 100m);

        // Neto = bruto − retención (la resta de dos decimales ya es exacta).
        var net = gross - tax;
        return (gross, tax, net);
    }

    /// <summary>Redondeo a 2 decimales con el medio hacia arriba (MidpointRounding.AwayFromZero).</summary>
    public static decimal RoundHalfUp(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Actualiza la auditoría de modificación (quién y cuándo).</summary>
    private static void TouchAudit(Domain.Entities.Bonus bonus, string userCode)
    {
        bonus.ModifiedBy = userCode;
        bonus.ModifiedAt = DateTime.UtcNow;
    }
}
