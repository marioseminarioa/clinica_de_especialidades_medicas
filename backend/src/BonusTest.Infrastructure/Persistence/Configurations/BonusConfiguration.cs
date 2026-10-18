using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Constants;

namespace BonusTest.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de Bonus a la tabla creada en database/02_bonus_schema.sql:
/// misma estructura, precisiones y FK compuesta hacia Employee.
/// </summary>
public class BonusConfiguration : IEntityTypeConfiguration<Bonus>
{
    public void Configure(EntityTypeBuilder<Bonus> builder)
    {
        builder.ToTable("Bonus").HasKey(x => x.BonusId);

        builder.Property(x => x.BonusId).ValueGeneratedOnAdd();
        builder.Property(x => x.Period).HasMaxLength(7).IsRequired();
        builder.Property(x => x.BonusTypeId).IsRequired();

        // Dinero siempre con 2 decimales, igual que en SQL Server.
        builder.Property(x => x.Amount).HasPrecision(16, 2);
        builder.Property(x => x.AmountPercentage).HasPrecision(5, 2);
        builder.Property(x => x.GrossAmount).HasPrecision(16, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(16, 2);
        builder.Property(x => x.NetAmount).HasPrecision(16, 2);

        // Estado de bono: 0201 Registrado, 0202 Pagado, 0203 Anulado.
        builder.Property(x => x.BonusStateCode).HasMaxLength(4).IsRequired()
            .HasDefaultValueSql("'0201'"); // todo bono nace en Registrado
        builder.HasOne(x => x.State)
            .WithMany()
            .HasForeignKey(x => x.BonusStateCode);

        // Motivo del bono (solo se registran motivos activos).
        builder.Property(x => x.ReasonCode).HasMaxLength(6).IsRequired();
        builder.HasOne(x => x.BonusReason)
            .WithMany()
            .HasForeignKey(x => x.ReasonCode);

        // Tipo de bono: 1 monto fijo, 2 porcentaje del sueldo.
        builder.HasOne(x => x.BonusType)
            .WithMany()
            .HasForeignKey(x => x.BonusTypeId);

        // FK compuesta hacia Employee (DepartmentCode + EmployeeNumber), como exige el enunciado.
        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => new { x.DepartmentCode, x.EmployeeNumber })
            .OnDelete(DeleteBehavior.Restrict);

        // Auditoría.
        builder.Property(x => x.CreatedBy).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ModifiedBy).HasMaxLength(50);

        // Índices para la paginación filtrada: estado + periodo es el combo más consultado,
        // y área + número resuelve los filtros por empleado.
        builder.HasIndex(x => new { x.BonusStateCode, x.Period }).HasDatabaseName("IX_Bonus_State_Period");
        builder.HasIndex(x => new { x.DepartmentCode, x.EmployeeNumber }).HasDatabaseName("IX_Bonus_Employee");
        // El chequeo de unicidad usa IsDeleted (estado != Anulado && no eliminado) → ver servicio.
        builder.HasIndex(x => x.IsDeleted).HasDatabaseName("IX_Bonus_IsDeleted");
    }
}

/// <summary>
/// Mapeo del detalle de pago por proyecto (punto extra 1), tabla BonusPaymentDetail
/// del script 02_bonus_schema.sql.
/// </summary>
public class BonusPaymentDetailConfiguration : IEntityTypeConfiguration<BonusPaymentDetail>
{
    public void Configure(EntityTypeBuilder<BonusPaymentDetail> builder)
    {
        builder.ToTable("BonusPaymentDetail").HasKey(x => x.BonusPaymentDetailId);

        builder.Property(x => x.BonusPaymentDetailId).ValueGeneratedOnAdd();

        // Nombre del proyecto: texto obligatorio de hasta 100 caracteres.
        builder.Property(x => x.ProjectName).HasMaxLength(100).IsRequired();

        // Horas y monto con las mismas precisiones que en SQL Server.
        builder.Property(x => x.Hours).HasPrecision(8, 2);
        builder.Property(x => x.AllocatedAmount).HasPrecision(16, 2);

        // Cada línea pertenece a un bono; al borrar el bono se borran sus líneas.
        builder.HasOne(x => x.Bonus)
            .WithMany()
            .HasForeignKey(x => x.BonusId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice para recuperar la distribución de un bono.
        builder.HasIndex(x => x.BonusId).HasDatabaseName("IX_BonusPaymentDetail_Bonus");
    }
}
