using BonusTest.Application.Common;

namespace BonusTest.Application.Bonuses;

/// <summary>Filtros + paginación para el listado de bonos. Todos los filtros son opcionales.</summary>
public class BonusSearchRequest : PaginationRequest
{
    /// <summary>Código de área, ej: TECH1. Vacío = todas.</summary>
    public string? DepartmentCode { get; set; }

    /// <summary>Número de empleado. Null = todos.</summary>
    public int? EmployeeNumber { get; set; }

    /// <summary>Periodo en formato 'YYYY-MM'. Vacío = todos.</summary>
    public string? Period { get; set; }

    /// <summary>Código de estado de bono (0201/0202/0203). Vacío = todos.</summary>
    public string? BonusStateCode { get; set; }
}

/// <summary>Fila del listado con las descripciones listas para mostrar.</summary>
public record BonusListItemDto(
    int BonusId,
    string BonusStateCode,
    string StateDescription,
    string Period,
    string DepartmentCode,
    string DepartmentName,
    int EmployeeNumber,
    string EmployeeFullName,
    int BonusTypeId,
    string BonusTypeDescription,
    decimal? Amount,
    decimal? AmountPercentage,
    decimal GrossAmount,
    decimal TaxAmount,
    decimal NetAmount,
    string ReasonDescription
);

/// <summary>Datos del empleado para el modal de registro.</summary>
public record EmployeeLookupDto(
    string DepartmentCode,
    string DepartmentName,
    int EmployeeNumber,
    string FullName,
    DateTime HireDate,
    decimal MonthlySalary,
    decimal TaxRate,
    /// <summary>0101 = Activo, 0102 = Cesado. El servicio valida que sea Activo.</summary>
    string EmployeeStateCode
);

/// <summary>Datos que ingresa el usuario para registrar un bono.</summary>
public record RegisterBonusRequest(
    string DepartmentCode,
    int EmployeeNumber,
    string Period,
    int BonusTypeId,
    decimal? Amount,
    decimal? AmountPercentage,
    string ReasonCode,
    string UserCode
);
