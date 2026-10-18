using Microsoft.EntityFrameworkCore;
using BonusTest.Domain.Entities;

namespace BonusTest.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<State> States => Set<State>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeProjectHours> EmployeeProjectHours => Set<EmployeeProjectHours>();
    public DbSet<BonusType> BonusTypes => Set<BonusType>();
    public DbSet<BonusReason> BonusReasons => Set<BonusReason>();

    // DbSet del módulo de bonos (creado por el script 02_bonus_schema.sql).
    public DbSet<Bonus> Bonuses => Set<Bonus>();

    // Detalle de pago por proyecto de un bono pagado (punto extra 1).
    public DbSet<BonusPaymentDetail> BonusPaymentDetails => Set<BonusPaymentDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
