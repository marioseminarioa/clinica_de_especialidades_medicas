using Microsoft.EntityFrameworkCore;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Infrastructure.Persistence;

namespace BonusTest.Infrastructure.Repositories;

/// <summary>
/// Implementación EF Core de IBonusRepository. La paginación se hace en la base de datos
/// (COUNT + Skip/Take en SQL), nunca en memoria.
/// </summary>
public class BonusRepository(AppDbContext context) : IBonusRepository
{
    /// <summary>Listado paginado en la base de datos con filtros opcionales.</summary>
    public async Task<PagedResponse<BonusListItemDto>> SearchAsync(BonusSearchRequest request, CancellationToken cancellationToken)
    {
        // Filtros opcionales; se aplican en SQL, no en memoria.
        var query = context.Bonuses.AsNoTracking()
            .Where(b => !b.IsDeleted)
            .Where(b => string.IsNullOrEmpty(request.DepartmentCode) || b.DepartmentCode == request.DepartmentCode)
            .Where(b => !request.EmployeeNumber.HasValue || b.EmployeeNumber == request.EmployeeNumber)
            .Where(b => string.IsNullOrEmpty(request.Period) || b.Period == request.Period)
            .Where(b => string.IsNullOrEmpty(request.BonusStateCode) || b.BonusStateCode == request.BonusStateCode);

        // COUNT en SQL: el total de registros se calcula en la base de datos.
        var totalRecords = await query.CountAsync(cancellationToken);

        // El orden garantiza una paginación estable entre páginas.
        var ordered = query
            .OrderByDescending(b => b.Period)
            .ThenByDescending(b => b.BonusId)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize);

        // Proyección con todas las descripciones listas para mostrar.
        var data = await ordered
            .Select(b => new BonusListItemDto(
                b.BonusId,
                b.BonusStateCode,
                b.State.StateDescription,
                b.Period,
                b.DepartmentCode,
                b.Employee.Department.DepartmentName,
                b.EmployeeNumber,
                b.Employee.FullName,
                b.BonusTypeId,
                b.BonusType.BonusTypeDescription,
                b.Amount,
                b.AmountPercentage,
                b.GrossAmount,
                b.TaxAmount,
                b.NetAmount,
                b.BonusReason.ReasonDescription
            ))
            .ToListAsync(cancellationToken);

        return PagedResponse<BonusListItemDto>.Create(data, request.PageIndex, request.PageSize, totalRecords);
    }

    /// <summary>Busca un empleado por su clave compuesta (área + número).</summary>
    public async Task<EmployeeLookupDto?> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        await context.Employees.AsNoTracking()
            .Where(e => e.DepartmentCode == departmentCode && e.EmployeeNumber == employeeNumber)
            .Select(e => new EmployeeLookupDto(
                e.DepartmentCode,
                e.Department.DepartmentName,
                e.EmployeeNumber,
                e.FullName,
                e.HireDate,
                e.MonthlySalary,
                e.TaxRate,
                e.EmployeeStateCode
            ))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Bono por id con sus descripciones; null si no existe o fue eliminado lógicamente.</summary>
    public async Task<BonusListItemDto?> GetByIdAsync(int bonusId, CancellationToken cancellationToken) =>
        await context.Bonuses.AsNoTracking()
            .Where(b => b.BonusId == bonusId && !b.IsDeleted)
            .Select(b => new BonusListItemDto(
                b.BonusId,
                b.BonusStateCode,
                b.State.StateDescription,
                b.Period,
                b.DepartmentCode,
                b.Employee.Department.DepartmentName,
                b.EmployeeNumber,
                b.Employee.FullName,
                b.BonusTypeId,
                b.BonusType.BonusTypeDescription,
                b.Amount,
                b.AmountPercentage,
                b.GrossAmount,
                b.TaxAmount,
                b.NetAmount,
                b.BonusReason.ReasonDescription
            ))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Entidad rastreable por id para anular / eliminar. Null si no existe o está eliminado.</summary>
    public async Task<Bonus?> GetEntityAsync(int bonusId, CancellationToken cancellationToken) =>
        await context.Bonuses
            .FirstOrDefaultAsync(b => b.BonusId == bonusId && !b.IsDeleted, cancellationToken);

    /// <summary>Regla 3: existe un bono activo (no Anulado y no eliminado) del empleado en ese periodo.</summary>
    public async Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, string period, CancellationToken cancellationToken) =>
        await context.Bonuses.AsNoTracking()
            .AnyAsync(b => b.DepartmentCode == departmentCode
                && b.EmployeeNumber == employeeNumber
                && b.Period == period
                // Activo = cualquier estado distinto de Anulado (0203) y no eliminado lógicamente.
                && b.BonusStateCode != BonusStates.Cancelled
                && !b.IsDeleted,
            cancellationToken);

    /// <summary>Inserta el bono; el id lo genera SQL Server (IDENTITY).</summary>
    public async Task<int> AddAsync(Bonus bonus, CancellationToken cancellationToken)
    {
        context.Bonuses.Add(bonus);
        await context.SaveChangesAsync(cancellationToken);
        return bonus.BonusId;
    }

    /// <summary>Persiste cambios de estado / baja lógica / auditoría.</summary>
    public async Task UpdateAsync(Bonus bonus, CancellationToken cancellationToken)
    {
        context.Bonuses.Update(bonus);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Solo motivos activos (State = 1) son válidos para registrar.</summary>
    public async Task<bool> ReasonExistsActiveAsync(string reasonCode, CancellationToken cancellationToken) =>
        await context.BonusReasons.AsNoTracking()
            .AnyAsync(r => r.ReasonCode == reasonCode && r.State, cancellationToken);
}
