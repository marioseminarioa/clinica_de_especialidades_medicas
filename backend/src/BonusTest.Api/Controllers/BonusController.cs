using Microsoft.AspNetCore.Mvc;
using BonusTest.Domain.Exceptions;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BonusTest.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class BonusController(BonusService bonusService) : ControllerBase
{
    /// <summary>
    /// Listado paginado de bonos con filtros opcionales (área, número de empleado, periodo y estado).
    /// La paginación se hace en la base de datos.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<BonusListItemDto>>> GetList(
        [FromQuery] BonusSearchRequest request, CancellationToken cancellationToken)
        => Ok(await bonusService.SearchAsync(request, cancellationToken));

    /// <summary>
    /// Busca un empleado por área + número de empleado para el modal de registro.
    /// Solo acepta empleados Activos; un Cesado devuelve 409 con { code, description }.
    /// </summary>
    [HttpGet("employee/{departmentCode}/{employeeNumber:int}")]
    public async Task<ActionResult<EmployeeLookupDto>> GetEmployee(
        string departmentCode, int employeeNumber, CancellationToken cancellationToken)
        => Ok(await bonusService.GetEmployeeAsync(departmentCode, employeeNumber, cancellationToken));

    /// <summary>Registra un bono (monto fijo o porcentaje del sueldo) aplicando las reglas de negocio.</summary>
    /// <param name="userCode">Usuario que opera, enviado en el header UserCode.</param>
    [HttpPost]
    public async Task<ActionResult<BonusListItemDto>> Create(
        [FromBody] RegisterBonusRequest request,
        [FromHeader(Name = "UserCode")] string? userCode,
        CancellationToken cancellationToken)
    {
        // Si el header UserCode no viene, es una petición mal formada: se devuelve 409 con código claro
        // en lugar de un 400 genérico de model binding.
        if (string.IsNullOrWhiteSpace(userCode))
            throw new BusinessException("MISSING_USER_CODE", "El header UserCode es obligatorio.");

        var created = await bonusService.RegisterAsync(request with { UserCode = userCode }, cancellationToken);
        // 201 con la ubicación del recurso creado.
        return CreatedAtAction(nameof(GetList), new { bonusId = created.BonusId }, created);
    }

    /// <summary>Anula un bono. Solo permitido desde el estado Registrado (0201).</summary>
    /// <param name="userCode">Usuario que opera, enviado en el header UserCode.</param>
    [HttpPut("{bonusId:int}/cancel")]
    public async Task<ActionResult<BonusListItemDto>> Cancel(
        int bonusId,
        [FromHeader(Name = "UserCode")] string? userCode,
        CancellationToken cancellationToken)
    {
        // El header UserCode es obligatorio para la auditoría de modificación.
        if (string.IsNullOrWhiteSpace(userCode))
            throw new BusinessException("MISSING_USER_CODE", "El header UserCode es obligatorio.");

        return Ok(await bonusService.CancelAsync(bonusId, userCode, cancellationToken));
    }

    /// <summary>Elimina de forma lógica (baja lógica). Solo permitido desde el estado Registrado (0201).</summary>
    /// <param name="userCode">Usuario que opera, enviado en el header UserCode.</param>
    [HttpDelete("{bonusId:int}")]
    public async Task<IActionResult> Delete(
        int bonusId,
        [FromHeader(Name = "UserCode")] string? userCode,
        CancellationToken cancellationToken)
    {
        // El header UserCode es obligatorio para la auditoría de modificación.
        if (string.IsNullOrWhiteSpace(userCode))
            throw new BusinessException("MISSING_USER_CODE", "El header UserCode es obligatorio.");

        await bonusService.DeleteAsync(bonusId, userCode, cancellationToken);
        return NoContent();
    }
}
