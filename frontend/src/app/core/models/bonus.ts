import { PagedResponse } from './paged-response';

/** Fila del listado de bonos: ya viene con las descripciones listas para mostrar. */
export interface BonusListItem {
  bonusId: number;
  bonusStateCode: string;
  bonusStateDescription: string;
  period: string;
  departmentCode: string;
  departmentName: string;
  employeeNumber: number;
  employeeFullName: string;
  bonusTypeId: number;
  bonusTypeDescription: string;
  amount: number | null;
  amountPercentage: number | null;
  reasonCode: string;
  reasonDescription: string;
  grossAmount: number;
  taxAmount: number;
  netAmount: number;
}

/** Filtros del listado; todos opcionales. */
export interface BonusSearchFilters {
  departmentCode?: string;
  employeeNumber?: number;
  period?: string;
  bonusStateCode?: string;
}

/** Empleado devuelto por la búsqueda del modal. */
export interface EmployeeLookup {
  departmentCode: string;
  departmentName: string;
  employeeNumber: number;
  fullName: string;
  hireDate: string;
  monthlySalary: number;
  taxRate: number;
  employeeStateCode: string;
}

/** Cuerpo del registro de un bono. */
export interface RegisterBonus {
  departmentCode: string;
  employeeNumber: number;
  period: string;
  bonusTypeId: number;
  amount?: number | null;
  amountPercentage?: number | null;
  reasonCode: string;
}

/* ===== Punto extra 1: pagos con distribución por proyectos ===== */

/** Línea de distribución del pago: proyecto, horas y monto asignado. */
export interface PayBonusDetail {
  projectName: string;
  hours: number;
  amount: number;
}

/** Cuerpo del pago de un bono (solo en estado Registrado). */
export interface PayBonusRequest {
  details: PayBonusDetail[];
}

/** Respuesta del pago: el bono queda en estado Pagado con sus montos. */
export interface PayBonusResult {
  bonusId: number;
  bonusStateDescription: string;
  grossAmount: number;
  taxAmount: number;
  netAmount: number;
}

/** Detalle de distribución por proyectos guardado para un bono pagado. */
export interface BonusPaymentDetail {
  bonusPaymentDetailId: number;
  bonusId: number;
  projectName: string;
  hours: number;
  allocatedAmount: number;
}
