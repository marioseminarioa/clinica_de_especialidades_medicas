namespace BonusTest.Domain.Entities;

/// <summary>
/// Línea de distribución del pago de un bono por proyecto (punto extra 1).
/// Cada bono pagado puede repartirse entre uno o más proyectos,
/// guardando horas dedicadas y monto asignado a cada uno.
/// </summary>
public class BonusPaymentDetail
{
    public int BonusPaymentDetailId { get; set; }

    /// <summary>Bono al que pertenece la distribución.</summary>
    public int BonusId { get; set; }

    /// <summary>Nombre del proyecto (texto libre, validado en el servicio).</summary>
    public string ProjectName { get; set; } = null!;

    /// <summary>Horas dedicadas al proyecto; deben ser positivas.</summary>
    public decimal Hours { get; set; }

    /// <summary>Monto del neto asignado a este proyecto.</summary>
    public decimal AllocatedAmount { get; set; }

    // Navegación al bono dueño de la distribución.
    public Bonus Bonus { get; set; } = null!;
}
