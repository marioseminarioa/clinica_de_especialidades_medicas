import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Notification } from '../services/notification';

/**
 * Agrega el header UserCode y muestra los errores de la API:
 * 409 = regla de negocio ({ code, description }), cualquier otro = error inesperado.
 * Las peticiones marcadas con 'X-Handle-Error-Inline' gestionan sus errores
 * dentro del componente (por ejemplo, el modal), así no se duplican en la barra global.
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const notification = inject(Notification);
  const request = req.clone({ setHeaders: { UserCode: environment.userCode } });

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      // Si el componente maneja el error en su propia UI, no mostrar el aviso global.
      const handledInline = req.headers.has('X-Handle-Error-Inline');
      if (error.status === 409) {
        if (!handledInline)
          notification.show('warning', error.error?.description ?? 'La operación no es válida.');
      } else if (!handledInline) {
        notification.show('error', 'El servicio no está disponible o respondió con un error. Intenta nuevamente.');
      }
      return throwError(() => error);
    }),
  );
};
