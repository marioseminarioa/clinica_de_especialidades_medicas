import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResponse } from '../models/paged-response';
import {
  BonusListItem,
  BonusSearchFilters,
  EmployeeLookup,
  RegisterBonus,
  PayBonusRequest,
  PayBonusResult,
  BonusPaymentDetail,
} from '../models/bonus';

/**
 * Cliente HTTP del módulo de bonos.
 * Los errores 409 de regla de negocio los muestra el interceptor global;
 * aquí no se repite ese manejo.
 */
@Injectable({ providedIn: 'root' })
export class BonusApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/Bonus`;

  /** Listado paginado desde el servidor con filtros opcionales. */
  search(
    pageIndex: number,
    pageSize: number,
    filters: BonusSearchFilters,
  ): Observable<PagedResponse<BonusListItem>> {
    // Solo se envían los filtros que tienen valor.
    let params = new HttpParams().set('pageIndex', pageIndex).set('pageSize', pageSize);
    if (filters.departmentCode) params = params.set('departmentCode', filters.departmentCode);
    if (filters.employeeNumber) params = params.set('employeeNumber', filters.employeeNumber);
    if (filters.period) params = params.set('period', filters.period);
    if (filters.bonusStateCode) params = params.set('bonusStateCode', filters.bonusStateCode);
    return this.http.get<PagedResponse<BonusListItem>>(this.baseUrl, { params });
  }

  /** Búsqueda del empleado por área + número (solo activos).
   *  El header 'X-Handle-Error-Inline' indica al interceptor que este error se
   *  muestra dentro del modal, por lo que no debe salir en la barra global. */
  getEmployee(departmentCode: string, employeeNumber: number): Observable<EmployeeLookup> {
    return this.http.get<EmployeeLookup>(
      `${this.baseUrl}/employee/${departmentCode}/${employeeNumber}`,
      { headers: { 'X-Handle-Error-Inline': 'true' } },
    );
  }

  /** Registra un bono y devuelve la fila creada. */
  register(bonus: RegisterBonus): Observable<BonusListItem> {
    return this.http.post<BonusListItem>(this.baseUrl, bonus);
  }

  /** Anula un bono en estado Registrado. */
  cancel(bonusId: number): Observable<BonusListItem> {
    return this.http.put<BonusListItem>(`${this.baseUrl}/${bonusId}/cancel`, {});
  }

  /** Elimina de forma lógica un bono en estado Registrado. */
  remove(bonusId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${bonusId}`);
  }

  /** Punto extra 1: registra el pago con distribución por proyectos. */
  pay(bonusId: number, request: PayBonusRequest): Observable<PayBonusResult> {
    return this.http.post<PayBonusResult>(`${this.baseUrl}/${bonusId}/pay`, request);
  }

  /** Punto extra 1: distribución por proyectos con la que fue pagado un bono. */
  getPayments(bonusId: number): Observable<BonusPaymentDetail[]> {
    return this.http.get<BonusPaymentDetail[]>(`${this.baseUrl}/${bonusId}/payments`);
  }
}
