import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { MasterStateCodes, BonusStates } from '../../core/models/catalog';
import { PagedResponse } from '../../core/models/paged-response';
import { BonusListItem, BonusSearchFilters } from '../../core/models/bonus';
import { BonusApi } from '../../core/services/bonus-api';
import { CatalogApi } from '../../core/services/catalog-api';
import { Notification } from '../../core/services/notification';
import { RegisterBonusModal } from './register-bonus-modal/register-bonus-modal';

/**
 * Pantalla de bonos de desempeño: filtros, listado paginado desde el servidor,
 * acciones por fila (anular/eliminar con confirmación) y modal de registro.
 * Los errores de negocio (409) los muestra el interceptor global; no se repiten aquí.
 */
@Component({
  selector: 'app-bonuses',
  imports: [FormsModule, DecimalPipe, RegisterBonusModal],
  templateUrl: './bonuses.html',
  styleUrl: './bonuses.css',
})
export class Bonuses {
  private readonly bonusApi = inject(BonusApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly notification = inject(Notification);

  // Catálogos para los combos de filtros (ya consumen la API).
  protected readonly departments = toSignal(this.catalogApi.getDepartments(), { initialValue: [] });
  protected readonly bonusStates = toSignal(this.catalogApi.getStates(MasterStateCodes.Bonus), { initialValue: [] });

  // Controles temporales del formulario de filtros (se aplican solo al buscar).
  protected fDepartment = '';
  protected fEmployeeNumber: number | null = null;
  protected fPeriod = '';
  protected fState = '';

  // Estado del listado: filtros aplicados, página actual y resultado paginado.
  protected readonly filters = signal<BonusSearchFilters>({});
  protected readonly pageIndex = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly result = signal<PagedResponse<BonusListItem> | null>(null);
  protected readonly loading = signal(false);

  // Visibilidad del modal de registro.
  protected readonly modalOpen = signal(false);

  // Las acciones (anular/eliminar) solo se permiten en estado Registrado (0201).
  protected canAct(row: BonusListItem): boolean {
    return row.bonusStateCode === BonusStates.Registered;
  }

  /** Ejecuta la búsqueda en el servidor. Se llama con Buscar y con cada cambio de página. */
  protected search(pageIndex = 1): void {
    this.pageIndex.set(pageIndex);
    this.loading.set(true);
    // Copia los valores de los controles: Buscar/Limpiar deciden cuándo aplicarlos.
    this.filters.set({
      departmentCode: this.fDepartment || undefined,
      employeeNumber: this.fEmployeeNumber || undefined,
      period: this.fPeriod || undefined,
      bonusStateCode: this.fState || undefined,
    });
    // La paginación se hace en la base de datos; aquí solo se consume la respuesta.
    this.bonusApi.search(pageIndex, this.pageSize(), this.filters()).subscribe({
      next: (response) => {
        this.result.set(response);
        this.loading.set(false);
      },
      // El error ya fue notificado por el interceptor; solo cortamos el estado de carga.
      error: () => this.loading.set(false),
    });
  }

  protected goToPage(page: number): void {
    const total = this.result()?.totalPages ?? 1;
    if (page >= 1 && page <= total) this.search(page);
  }

  protected prev(): void {
    this.goToPage(this.pageIndex() - 1);
  }

  protected next(): void {
    this.goToPage(this.pageIndex() + 1);
  }

  /** Limpia los filtros y vuelve a la primera página. */
  protected clear(): void {
    this.fDepartment = '';
    this.fEmployeeNumber = null;
    this.fPeriod = '';
    this.fState = '';
    this.search(1);
  }

  /** Anula el bono (solo en Registrado), con confirmación previa. */
  protected cancel(row: BonusListItem): void {
    if (!confirm(`¿Anular el bono del periodo ${row.period} de ${row.employeeFullName}?`)) return;
    this.bonusApi.cancel(row.bonusId).subscribe({
      next: () => {
        this.notification.show('success', 'El bono fue anulado.');
        this.search(this.pageIndex()); // refresca el listado
      },
      error: () => {}, // el error de negocio ya lo mostró el interceptor
    });
  }

  /** Elimina de forma lógica el bono (solo en Registrado), con confirmación previa. */
  protected remove(row: BonusListItem): void {
    if (!confirm(`¿Eliminar el bono del periodo ${row.period} de ${row.employeeFullName}?`)) return;
    this.bonusApi.remove(row.bonusId).subscribe({
      next: () => {
        this.notification.show('success', 'El bono fue eliminado.');
        this.search(this.pageIndex());
      },
      error: () => {},
    });
  }

  protected openModal(): void {
    this.modalOpen.set(true);
  }

  protected closeModal(): void {
    this.modalOpen.set(false);
  }

  /** El modal avisa que guardó con éxito: cerramos y refrescamos el listado. */
  protected onSaved(): void {
    this.modalOpen.set(false);
    this.search(this.pageIndex());
  }
}
