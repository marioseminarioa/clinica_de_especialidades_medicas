using BonusTest.Domain.Entities;

namespace BonusTest.Application.Bonuses;

/// <summary>
/// Petición del pago de un bono con distribución por proyectos (punto extra 1).
/// Cada detalle lleva el nombre del proyecto, las horas y el monto asignado;
/// la suma de los montos debe ser exactamente igual al neto del bono.
/// </summary>
public record PayBonusRequest(
    int BonusId,
    IReadOnlyList<BonusPaymentDetailItem> Details,
    string? UserCode = null
);

/// <summary>Línea de distribución del pago: proyecto + horas + monto.</summary>
public record BonusPaymentDetailItem(
    string ProjectName,
    decimal Hours,
    decimal Amount
);

/// <summary>Resultado del pago: el bono actualizado y sus líneas de distribución.</summary>
public record PayBonusResultDto(
    BonusListItemDto Bonus,
    IReadOnlyList<BonusPaymentDetailDto> Details
);

/// <summary>Línea de distribución guardada, con el monto y las horas por proyecto.</summary>
public record BonusPaymentDetailDto(
    int BonusPaymentDetailId,
    string ProjectName,
    decimal Hours,
    decimal AllocatedAmount
);
