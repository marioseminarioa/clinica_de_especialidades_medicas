using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;

namespace BonusTest.Application.Bonuses;

/// <summary>
/// Contrato del repositorio de bonos. El servicio de aplicación no conoce EF Core:
/// toda la persistencia (incluida la paginación en BD) vive en Infrastructure.
/// </summary>
public interface IBonusRepository
{
    /// <summary>Listado paginado en la base de datos (Skip/Take + COUNT), con descripciones resueltas por JOIN.</summary>
    Task<PagedResponse<BonusListItemDto>> SearchAsync(BonusSearchRequest request, CancellationToken cancellationToken);

    /// <summary>Busca un empleado por área + número para el modal de registro. Null si no existe.</summary>
    Task<EmployeeLookupDto?> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken);

    /// <summary>Bono por id (con sus descripciones) o null si no existe / está eliminado.</summary>
    Task<BonusListItemDto?> GetByIdAsync(int bonusId, CancellationToken cancellationToken);

    /// <summary>Indica si el empleado ya tiene un bono activo en el periodo (regla 3).</summary>
    Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, string period, CancellationToken cancellationToken);

    /// <summary>Registra el bono y devuelve el id generado.</summary>
    Task<int> AddAsync(Domain.Entities.Bonus bonus, CancellationToken cancellationToken);

    /// <summary>Guarda cambios de estado / baja lógica sobre el bono.</summary>
    Task UpdateAsync(Domain.Entities.Bonus bonus, CancellationToken cancellationToken);
}
