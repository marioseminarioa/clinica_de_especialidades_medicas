using Microsoft.Extensions.DependencyInjection;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Catalogs;

namespace BonusTest.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CatalogService>();

        // Servicio del módulo de bonos (contiene las reglas de negocio).
        services.AddScoped<Bonuses.BonusService>();

        return services;
    }
}
