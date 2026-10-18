import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { BonusListItem, PayBonusDetail } from '../../../core/models/bonus';
import { BonusApi } from '../../../core/services/bonus-api';
import { Notification } from '../../../core/services/notification';

/**
 * Modal de pago de un bono (punto extra 1).
 * Distribuye el monto NETO del bono entre uno o más proyectos por horas.
 * Reglas: la suma debe ser exactamente el neto, horas y montos > 0.
 */
@Component({
  selector: 'app-pay-bonus-modal',
  imports: [FormsModule, DecimalPipe],
  templateUrl: './pay-bonus-modal.html',
  styleUrl: './pay-bonus-modal.css',
})
export class PayBonusModal {
  private readonly bonusApi = inject(BonusApi);
  private readonly notification = inject(Notification);

  /** Bono a pagar (viene de la fila seleccionada en el listado). */
  readonly bonus = input.required<BonusListItem>();
  readonly closed = output<void>();
  readonly paid = output<void>();

  // Líneas de distribución. La primera se precarga al inicializar con el neto del bono.
  protected readonly details = signal<PayBonusDetail[]>([]);

  constructor() {
    // input.required se resuelve tras la creación; se precarga en el primer ciclo de cambio.
    effect(() => {
      this.details.set([{ projectName: '', hours: 1, amount: this.bonus().netAmount }]);
    });
  }

  // Suma actual de las líneas; se compara contra el neto del bono.
  protected readonly total = computed(() =>
    this.details().reduce((sum, d) => sum + (Number(d.amount) || 0), 0),
  );

  // Diferencia entre el neto y lo asignado (para el aviso visual).
  protected readonly difference = computed(
    () => Math.round((this.bonus().netAmount - this.total()) * 100) / 100,
  );

  protected readonly validationError = signal('');

  /** Agrega una línea vacía para otro proyecto. */
  protected addRow(): void {
    this.details.update((rows) => [...rows, { projectName: '', hours: 1, amount: 0 }]);
  }

  /** Quita una línea (siempre debe quedar al menos una). */
  protected removeRow(index: number): void {
    this.details.update((rows) => (rows.length > 1 ? rows.filter((_, i) => i !== index) : rows));
  }

  /** Valida las reglas de negocio y envía el pago al backend. */
  protected pay(): void {
    const rows = this.details();
    this.validationError.set('');

    // Cada línea necesita proyecto, horas y monto válidos.
    for (let i = 0; i < rows.length; i++) {
      const d = rows[i];
      if (!d.projectName.trim()) {
        this.validationError.set(`La línea ${i + 1} necesita un nombre de proyecto.`);
        return;
      }
      if (!d.hours || d.hours <= 0) {
        this.validationError.set(`La línea ${i + 1} necesita horas mayores a 0.`);
        return;
      }
      if (!d.amount || d.amount <= 0) {
        this.validationError.set(`La línea ${i + 1} necesita un monto mayor a 0.`);
        return;
      }
    }

    // La distribución debe cubrir exactamente el neto del bono.
    if (this.difference() !== 0) {
      this.validationError.set(
        `La suma asignada (${this.total().toFixed(2)}) debe ser igual al monto neto (${this.bonus().netAmount.toFixed(2)}).`,
      );
      return;
    }

    this.bonusApi.pay(this.bonus().bonusId, { details: rows }).subscribe({
      next: () => {
        this.notification.show('success', 'El bono fue pagado.');
        this.paid.emit();
      },
      error: () => {}, // el interceptor ya mostró el error de negocio
    });
  }

  protected close(): void {
    this.closed.emit();
  }
}
