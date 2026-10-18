import { Component, computed, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, DatePipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { BonusTypes } from '../../../core/models/catalog';
import { EmployeeLookup } from '../../../core/models/bonus';
import { BonusApi } from '../../../core/services/bonus-api';
import { CatalogApi } from '../../../core/services/catalog-api';
import { Notification } from '../../../core/services/notification';

/**
 * Modal de registro de bonos.
 * Pasos: buscar empleado → mostrar sus datos → llenar el formulario
 * → vista previa del cálculo → guardar (el backend revalida las reglas).
 * Los errores de negocio (409) los muestra el interceptor global.
 */
@Component({
  selector: 'app-register-bonus-modal',
  imports: [FormsModule, DecimalPipe, DatePipe],
  templateUrl: './register-bonus-modal.html',
  styleUrl: './register-bonus-modal.css',
})
export class RegisterBonusModal {
  private readonly bonusApi = inject(BonusApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly notification = inject(Notification);

  // Eventos hacia la pantalla principal: cerrar sin guardar / guardar con éxito.
  readonly closed = output<void>();
  readonly saved = output<void>();

  // Catálogos del modal (áreas, tipos de bono y motivos activos).
  protected readonly departments = toSignal(this.catalogApi.getDepartments(), { initialValue: [] });
  protected readonly bonusTypes = toSignal(this.catalogApi.getBonusTypes(), { initialValue: [] });
  protected readonly reasons = toSignal(this.catalogApi.getBonusReasons(), { initialValue: [] });

  // Referencias a las constantes de tipos para comparar en la plantilla.
  protected readonly bonusTypesEnum = BonusTypes;

  // Controles de búsqueda del empleado.
  protected lDepartment = '';
  protected lEmployeeNumber: number | null = null;
  protected readonly employee = signal<EmployeeLookup | null>(null);

  // Controles del formulario del bono.
  protected period = '';
  protected bonusTypeId: number | null = null;
  protected value: number | null = null;
  protected reasonCode = '';

  // Mensaje de validación local (el backend revalida todo igual).
  protected readonly validationError = signal('');

  /**
   * Vista previa del cálculo mientras el usuario escribe.
   * Reproduce las reglas del backend: bruto (monto fijo o sueldo × %/100),
   * retención (bruto × TaxRate/100) y neto (bruto − retención).
   */
  protected readonly preview = computed(() => {
    const emp = this.employee();
    if (!emp || this.value == null || this.value <= 0) return null;

    const gross =
      this.bonusTypeId === BonusTypes.SalaryPercentage
        ? (emp.monthlySalary * this.value) / 100 // porcentaje del sueldo
        : this.value; // monto fijo

    const tax = (gross * emp.taxRate) / 100;
    return { gross, tax, net: gross - tax };
  });

  /** Busca al empleado por área + número; valida que ambos campos estén llenos. */
  protected findEmployee(): void {
    this.validationError.set('');
    if (!this.lDepartment || !this.lEmployeeNumber) {
      this.validationError.set('Área y número de empleado son obligatorios.');
      return;
    }
    this.bonusApi.getEmployee(this.lDepartment, this.lEmployeeNumber).subscribe({
      next: (emp) => this.employee.set(emp),
      error: () => {}, // el interceptor ya mostró el motivo (no existe o no está activo)
    });
  }

  /** Al cambiar el tipo de bono se limpia el valor (monto vs porcentaje). */
  protected onTypeChange(): void {
    this.value = null;
  }

  /** Valida localmente y envía el registro; al guardar cierra el modal. */
  protected save(): void {
    const emp = this.employee();
    this.validationError.set('');

    // Validaciones de obligatoriedad en el front (el backend revalida las reglas).
    if (!emp) {
      this.validationError.set('Primero busca al empleado.');
      return;
    }
    if (!this.period) {
      this.validationError.set('El periodo es obligatorio.');
      return;
    }
    if (this.bonusTypeId == null) {
      this.validationError.set('El tipo de bono es obligatorio.');
      return;
    }
    if (this.value == null || this.value <= 0) {
      this.validationError.set('El monto o porcentaje debe ser mayor a 0.');
      return;
    }
    if (!this.reasonCode) {
      this.validationError.set('El motivo es obligatorio.');
      return;
    }

    this.bonusApi
      .register({
        departmentCode: emp.departmentCode,
        employeeNumber: emp.employeeNumber,
        period: this.period,
        bonusTypeId: this.bonusTypeId,
        // Según el tipo se envía amount (monto fijo) o amountPercentage (% del sueldo).
        ...(this.bonusTypeId === BonusTypes.SalaryPercentage
          ? { amountPercentage: this.value }
          : { amount: this.value }),
        reasonCode: this.reasonCode,
      })
      .subscribe({
        next: () => {
          this.notification.show('success', 'El bono fue registrado.');
          this.saved.emit(); // la pantalla principal refresca el listado
        },
        error: () => {}, // el interceptor ya mostró el error de negocio
      });
  }

  protected close(): void {
    this.closed.emit();
  }
}
