/* =====================================================================
   Prueba técnica - Bonos de desempeño
   Script 2: tabla Bonus (diseño del candidato) e índices.
   ===================================================================== */
USE BonusTest;
GO
/* Tabla principal del módulo.
   Diseño:
   - BonusId INT IDENTITY: PK artificial para referenciar un bono
     (cancelar, eliminar, pagar) sin exponer la clave compuesta del empleado.
   - FK compuesta (DepartmentCode, EmployeeNumber) hacia Employee,
     tal como pide el enunciado.
   - Period CHAR(7) en formato 'YYYY-MM': permite comparar, filtrar y agrupar
     por periodo sin depender de una fecha completa.
   - BonusType INT FK a BonusType (1 = monto fijo, 2 = % del sueldo).
   - Amount DECIMAL(16,2): guarda el monto fijo cuando aplica; NULL si el
     tipo es porcentaje. Para porcentaje guardo el valor en AmountValue
     DECIMAL(5,2) (0-100, como TaxRate).
   - GrossAmount / TaxAmount / NetAmount: resultado del cálculo, persistido
     para que el listado no tenga que recalcular y quede trazabilidad
     de lo que efectivamente se pagó.
   - BonusStateCode FK a State (0201 Registrado, 0202 Pagado, 0203 Anulado).
   - ReasonCode FK a BonusReason (solo motivos activos, validado en el servicio).
   - IsDeleted BIT: baja lógica (regla 5). No se borra físicamente.
   - Auditoría (regla 6): quién y cuándo creó y modificó.
*/
IF OBJECT_ID('dbo.Bonus') IS NULL
BEGIN
    CREATE TABLE dbo.Bonus (
        BonusId            INT IDENTITY(1,1) NOT NULL,
        DepartmentCode     CHAR(5)          NOT NULL,
        EmployeeNumber     INT              NOT NULL,
        Period             CHAR(7)          NOT NULL,          -- 'YYYY-MM'
        BonusTypeId        INT              NOT NULL,
        Amount             DECIMAL(16,2)    NULL,              -- monto fijo
        AmountPercentage   DECIMAL(5,2)     NULL,              -- % del sueldo
        GrossAmount        DECIMAL(16,2)    NOT NULL,
        TaxAmount          DECIMAL(16,2)    NOT NULL,
        NetAmount          DECIMAL(16,2)    NOT NULL,
        BonusStateCode     CHAR(4)          NOT NULL,
        ReasonCode         CHAR(6)          NOT NULL,
        IsDeleted          BIT              NOT NULL CONSTRAINT DF_Bonus_IsDeleted DEFAULT (0),
        CreatedBy          VARCHAR(50)      NOT NULL,
        CreatedAt          DATETIME2(0)     NOT NULL,
        ModifiedBy         VARCHAR(50)      NULL,
        ModifiedAt         DATETIME2(0)     NULL,
        CONSTRAINT PK_Bonus PRIMARY KEY (BonusId),
        CONSTRAINT FK_Bonus_Employee FOREIGN KEY (DepartmentCode, EmployeeNumber)
            REFERENCES dbo.Employee (DepartmentCode, EmployeeNumber),
        CONSTRAINT FK_Bonus_BonusType FOREIGN KEY (BonusTypeId)
            REFERENCES dbo.BonusType (BonusTypeId),
        CONSTRAINT FK_Bonus_State FOREIGN KEY (BonusStateCode)
            REFERENCES dbo.State (StateCode),
        CONSTRAINT FK_Bonus_Reason FOREIGN KEY (ReasonCode)
            REFERENCES dbo.BonusReason (ReasonCode),
        -- El tipo determina qué campo de valor se usa.
        CONSTRAINT CK_Bonus_TypeAmount CHECK (
            (BonusTypeId = 1 AND Amount IS NOT NULL AND AmountPercentage IS NULL)
         OR (BonusTypeId = 2 AND Amount IS NULL AND AmountPercentage IS NOT NULL)
        )
    );
END
GO
/* Regla 3: un empleado no puede tener más de un bono activo por periodo.
   Se garantiza a nivel de BD (además de en el servicio) con un índice único
   que solo considera registros vigentes: no eliminados y no anulados.
   Se usa un índice único filtrado porque SQL Server no permite FK con filtro
   ni constraints únicos con condición.
*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Bonus_ActivePeriod'
               AND object_id = OBJECT_ID('dbo.Bonus'))
    CREATE UNIQUE INDEX UX_Bonus_ActivePeriod ON dbo.Bonus
        (DepartmentCode, EmployeeNumber, Period)
        WHERE IsDeleted = 0 AND BonusStateCode <> '0203';
GO
/* Índices para el listado paginado con filtros:
   - Por periodo + estado: el filtro más común (Buscar por periodo/estado),
     incluye columnas de montos para que el COUNT y la página se lean
     sin ir a la tabla base (covering index).
*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bonus_Period_State'
               AND object_id = OBJECT_ID('dbo.Bonus'))
    CREATE INDEX IX_Bonus_Period_State ON dbo.Bonus
        (Period, BonusStateCode) INCLUDE (IsDeleted, GrossAmount, TaxAmount, NetAmount);
GO
/* - Por empleado: para los filtros por área + número de empleado y para
     validar reglas del empleado. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bonus_Employee'
               AND object_id = OBJECT_ID('dbo.Bonus'))
    CREATE INDEX IX_Bonus_Employee ON dbo.Bonus
        (DepartmentCode, EmployeeNumber) INCLUDE (Period, BonusStateCode, IsDeleted);
GO
/* Tabla de detalle para el punto extra 1 (pago distribuido por proyecto).
   Se crea aquí para tener el esquema completo desde el inicio. */
IF OBJECT_ID('dbo.BonusPaymentDetail') IS NULL
BEGIN
    CREATE TABLE dbo.BonusPaymentDetail (
        BonusPaymentDetailId INT IDENTITY(1,1) NOT NULL,
        BonusId              INT           NOT NULL,
        ProjectName          VARCHAR(100)  NOT NULL,
        Hours                DECIMAL(8,2)  NOT NULL,
        AllocatedAmount      DECIMAL(16,2) NOT NULL,
        CONSTRAINT PK_BonusPaymentDetail PRIMARY KEY (BonusPaymentDetailId),
        CONSTRAINT FK_BonusPaymentDetail_Bonus FOREIGN KEY (BonusId)
            REFERENCES dbo.Bonus (BonusId)
    );
    CREATE INDEX IX_BonusPaymentDetail_Bonus ON dbo.BonusPaymentDetail (BonusId);
END
GO
