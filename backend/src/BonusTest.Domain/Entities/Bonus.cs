using BonusTest.Domain.Constants;

namespace BonusTest.Domain.Entities;

/// <summary>
/// Bono de desempeño de un empleado para un periodo (YYYY-MM).
/// Guarda el cálculo (bruto, retención, neto) para dejar trazabilidad
/// de lo que efectivamente se pagó, además del valor de origen (monto o %).
/// </summary>
public class Bonus
{
    public int BonusId { get; set; }

    // Clave compuesta hacia Employee (área + número de empleado).
    public string DepartmentCode { get; set; } = null!;
    public int EmployeeNumber { get; set; }

    /// <summary>Periodo en formato 'YYYY-MM'. No puede ser posterior al mes actual.</summary>
    public string Period { get; set; } = null!;

    /// <summary>1 = Monto fijo, 2 = Porcentaje del sueldo.</summary>
    public int BonusTypeId { get; set; }

    /// <summary>Monto fijo cuando BonusTypeId = 1; null si es porcentaje.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Porcentaje del sueldo cuando BonusTypeId = 2; null si es monto fijo.</summary>
    public decimal? AmountPercentage { get; set; }

    /// <summary>Bruto = monto fijo, o sueldo mensual × porcentaje / 100.</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>Retención = Bruto × TaxRate / 100 (TaxRate del empleado).</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Neto = Bruto − Retención.</summary>
    public decimal NetAmount { get; set; }

    /// <summary>Todos los bonos nacen en Registrado (0201). Pagado solo con el punto extra.</summary>
    public string BonusStateCode { get; set; } = BonusStates.Registered;

    /// <summary>Motivo del bono; solo se aceptan motivos activos.</summary>
    public string ReasonCode { get; set; } = null!;

    /// <summary>Baja lógica: el registro deja de aparecer en el listado pero no se borra.</summary>
    public bool IsDeleted { get; set; }

    // Auditoría: quién y cuándo creó y modificó cada registro.
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }

    // Navegaciones para mostrar descripciones listas en el listado.
    public Employee Employee { get; set; } = null!;
    public BonusType BonusType { get; set; } = null!;
    public BonusReason BonusReason { get; set; } = null!;
    public State State { get; set; } = null!;
}
