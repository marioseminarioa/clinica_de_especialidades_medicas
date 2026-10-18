# Prueba técnica Full Stack — .NET + Angular + SQL Server

Clínica de Especialidades Médicas: módulo de **bonos de desempeño** (CRUD con reglas de negocio), construido sobre la base ya provista (catálogos, empleados, auditoría) e implementado sobre ASP.NET Core (Clean Architecture), Angular 20 (signals, standalone) y SQL Server.

## Estructura

```
database/   Scripts SQL (01 schema/seed, 02 tabla Bonus)
backend/    BonusTest.Api (controllers, middleware)
            BonusTest.Application (servicios, DTOs, contratos)
            BonusTest.Domain (entidades, constantes, excepciones)
            BonusTest.Infrastructure (EF Core, repositorios)
            tests/BonusTest.Tests (xUnit + Moq, 21 pruebas)
frontend/   Angular 20 standalone (signals, toSignal)
```

## Requisitos

- .NET 9 SDK
- Node.js 20+
- SQL Server 2019+ (local o Docker)

## Configuración de la base de datos

1. Crea la BD y ejecuta los scripts en orden:
   ```bash
   sqlcmd -S localhost -U sa -P '<password>' -i database/01_schema_seed.sql
   sqlcmd -S localhost -U sa -P '<password>' -i database/02_bonus_schema.sql
   ```
2. Ajusta la cadena de conexión en `backend/src/BonusTest.Api/appsettings.json`
   (`Trusted_Connection=True` para autenticación de Windows, o `User Id/Password` para SQL auth).

## Ejecutar

Backend (https://localhost:7106 por defecto):

```bash
cd backend/src/BonusTest.Api
dotnet run
```

Frontend (http://localhost:4200, proxy hacia la API):

```bash
cd frontend
npm install
npm start
```

## Pruebas

```bash
cd backend
dotnet test
```

## Funcionalidad

- Listado de bonos con paginación **en base de datos** (Skip/Take + COUNT) y filtros opcionales: área, número de empleado, periodo (`YYYY-MM`) y estado.
- Registro de bonos con búsqueda del empleado por área + número y vista previa del cálculo (bruto, retención, neto) mientras se llena el formulario.
- Acciones solo en estado **Registrado (0201)**: anular, eliminar (baja lógica) y pagar.
- Reglas de negocio (validadas en `BonusService`, HTTP 409 si fallan):
  - El empleado debe existir y estar activo.
  - El periodo no puede ser posterior al mes en curso.
  - Un solo bono activo por empleado/periodo (índice único filtrado en BD).
  - Monto fijo > 0 y ≤ sueldo mensual; porcentaje en (0, 100).
  - Motivo activo en el catálogo.
- Cálculo: bruto = monto fijo o sueldo × %/100; retención = bruto × TaxRate/100; neto = bruto − retención (redondeo half-up a 2 decimales).
- Auditoría en todos los cambios (`CreatedBy/CreatedAt/ModifiedBy/ModifiedAt`), `UserCode` enviado en header `X-User-Code`.
- **Punto extra 1:** pago del bono con distribución del monto neto entre proyectos (por horas), guardado en `BonusPaymentDetail` dentro de una transacción.
