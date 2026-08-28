IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [Canales] (
        [CanalId] int NOT NULL IDENTITY,
        [Nombre] nvarchar(100) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Canales] PRIMARY KEY ([CanalId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [Distritos] (
        [DistritoId] int NOT NULL IDENTITY,
        [Nombre] nvarchar(100) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Distritos] PRIMARY KEY ([DistritoId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [EstadosDispositivo] (
        [EstadoId] int NOT NULL IDENTITY,
        [Nombre] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_EstadosDispositivo] PRIMARY KEY ([EstadoId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [IntegracionesConfiguracion] (
        [ConfiguracionId] int NOT NULL IDENTITY,
        [Proveedor] varchar(30) NOT NULL,
        [Entorno] varchar(50) NOT NULL DEFAULT 'PRODUCCION',
        [TipoAutenticacion] varchar(30) NOT NULL,
        [UrlBase] varchar(300) NOT NULL,
        [ClientIdCifrado] varbinary(max) NULL,
        [ClientSecretCifrado] varbinary(max) NULL,
        [UsuarioCifrado] varbinary(max) NULL,
        [PasswordCifrado] varbinary(max) NULL,
        [ApiKeyCifrada] varbinary(max) NULL,
        [ParametrosAdicionales] nvarchar(max) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [FechaActualizacion] datetime2 NULL,
        CONSTRAINT [PK_IntegracionesConfiguracion] PRIMARY KEY ([ConfiguracionId]),
        CONSTRAINT [CK_IntegracionesConfiguracion_ParametrosJson] CHECK ([ParametrosAdicionales] IS NULL OR ISJSON([ParametrosAdicionales]) = 1),
        CONSTRAINT [CK_IntegracionesConfiguracion_Proveedor] CHECK ([Proveedor] IN ('MOBICONTROL','INFOBIP','GUPSHUP')),
        CONSTRAINT [CK_IntegracionesConfiguracion_TipoAutenticacion] CHECK ([TipoAutenticacion] IN ('OAUTH_PASSWORD','API_KEY','BASIC'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [Empleados] (
        [EmpleadoId] int NOT NULL IDENTITY,
        [Cedula] varchar(20) NOT NULL,
        [NombreCompleto] nvarchar(200) NOT NULL,
        [CanalId] int NULL,
        [DistritoId] int NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [FechaActualizacion] datetime2 NULL,
        CONSTRAINT [PK_Empleados] PRIMARY KEY ([EmpleadoId]),
        CONSTRAINT [FK_Empleados_Canales_CanalId] FOREIGN KEY ([CanalId]) REFERENCES [Canales] ([CanalId]),
        CONSTRAINT [FK_Empleados_Distritos_DistritoId] FOREIGN KEY ([DistritoId]) REFERENCES [Distritos] ([DistritoId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [Dispositivos] (
        [DispositivoId] int NOT NULL IDENTITY,
        [MobiControlDeviceId] varchar(100) NOT NULL,
        [Fabricante] nvarchar(100) NULL,
        [Modelo] nvarchar(100) NULL,
        [IMEI] varchar(50) NULL,
        [ICCID] varchar(50) NULL,
        [NumeroCelular] varchar(30) NULL,
        [CostoEquipo] decimal(12,2) NULL,
        [EstadoActualId] int NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [FechaActualizacion] datetime2 NULL,
        CONSTRAINT [PK_Dispositivos] PRIMARY KEY ([DispositivoId]),
        CONSTRAINT [FK_Dispositivos_EstadosDispositivo_EstadoActualId] FOREIGN KEY ([EstadoActualId]) REFERENCES [EstadosDispositivo] ([EstadoId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [EntregasDispositivo] (
        [EntregaId] int NOT NULL IDENTITY,
        [EntregaUid] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [DispositivoId] int NOT NULL,
        [EmpleadoId] int NOT NULL,
        [EstadoId] int NULL,
        [CanalId] int NULL,
        [DistritoId] int NULL,
        [NombreAsociadoFirmante] nvarchar(200) NOT NULL,
        [Entregables] nvarchar(max) NULL,
        [ICCID] varchar(50) NULL,
        [NumeroCelular] varchar(30) NULL,
        [CostoEquipo] decimal(12,2) NULL,
        [CiudadFirma] varchar(100) NOT NULL DEFAULT 'Cali',
        [FechaEntregaProgramada] date NULL,
        [FechaFirma] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [EstadoProceso] varchar(30) NOT NULL DEFAULT 'FIRMADO',
        [ClaveIdempotencia] varchar(100) NULL,
        [IPOrigen] varchar(45) NULL,
        [UserAgent] nvarchar(300) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_EntregasDispositivo] PRIMARY KEY ([EntregaId]),
        CONSTRAINT [CK_EntregasDispositivo_EstadoProceso] CHECK ([EstadoProceso] IN ('FIRMADO','SINCRONIZADO','ERROR_SINCRONIZACION')),
        CONSTRAINT [FK_EntregasDispositivo_Canales_CanalId] FOREIGN KEY ([CanalId]) REFERENCES [Canales] ([CanalId]),
        CONSTRAINT [FK_EntregasDispositivo_Dispositivos_DispositivoId] FOREIGN KEY ([DispositivoId]) REFERENCES [Dispositivos] ([DispositivoId]),
        CONSTRAINT [FK_EntregasDispositivo_Distritos_DistritoId] FOREIGN KEY ([DistritoId]) REFERENCES [Distritos] ([DistritoId]),
        CONSTRAINT [FK_EntregasDispositivo_Empleados_EmpleadoId] FOREIGN KEY ([EmpleadoId]) REFERENCES [Empleados] ([EmpleadoId]),
        CONSTRAINT [FK_EntregasDispositivo_EstadosDispositivo_EstadoId] FOREIGN KEY ([EstadoId]) REFERENCES [EstadosDispositivo] ([EstadoId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [DocumentosPDF] (
        [DocumentoId] int NOT NULL IDENTITY,
        [EntregaId] int NOT NULL,
        [NombreContenedor] varchar(100) NOT NULL DEFAULT 'documentos-pdf',
        [RutaBlob] varchar(500) NOT NULL,
        [UrlBlob] varchar(1000) NULL,
        [NombreArchivo] varchar(255) NOT NULL DEFAULT 'firmaentregadispositivo.pdf',
        [TamanoBytes] int NULL,
        [HashSHA256] varbinary(32) NULL,
        [FechaGeneracion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_DocumentosPDF] PRIMARY KEY ([DocumentoId]),
        CONSTRAINT [FK_DocumentosPDF_EntregasDispositivo_EntregaId] FOREIGN KEY ([EntregaId]) REFERENCES [EntregasDispositivo] ([EntregaId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [Firmas] (
        [FirmaId] int NOT NULL IDENTITY,
        [EntregaId] int NOT NULL,
        [NombreContenedor] varchar(100) NOT NULL DEFAULT 'firmas',
        [RutaBlob] varchar(500) NOT NULL,
        [UrlBlob] varchar(1000) NULL,
        [FormatoImagen] varchar(20) NOT NULL DEFAULT 'image/png',
        [TamanoBytes] int NULL,
        [HashSHA256] varbinary(32) NULL,
        [FechaCaptura] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Firmas] PRIMARY KEY ([FirmaId]),
        CONSTRAINT [FK_Firmas_EntregasDispositivo_EntregaId] FOREIGN KEY ([EntregaId]) REFERENCES [EntregasDispositivo] ([EntregaId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE TABLE [IntegracionesSincronizaciones] (
        [SincronizacionId] int NOT NULL IDENTITY,
        [EntregaId] int NOT NULL,
        [Proveedor] varchar(30) NOT NULL,
        [TipoAccion] varchar(50) NOT NULL,
        [Exitoso] bit NOT NULL,
        [CodigoRespuestaHttp] int NULL,
        [MensajeError] nvarchar(max) NULL,
        [FechaEjecucion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_IntegracionesSincronizaciones] PRIMARY KEY ([SincronizacionId]),
        CONSTRAINT [CK_IntegracionesSincronizaciones_Proveedor] CHECK ([Proveedor] IN ('MOBICONTROL','INFOBIP','GUPSHUP')),
        CONSTRAINT [FK_IntegracionesSincronizaciones_EntregasDispositivo_EntregaId] FOREIGN KEY ([EntregaId]) REFERENCES [EntregasDispositivo] ([EntregaId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Canales_Nombre] ON [Canales] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Dispositivos_EstadoActualId] ON [Dispositivos] ([EstadoActualId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Dispositivos_IMEI] ON [Dispositivos] ([IMEI]) WHERE [IMEI] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Dispositivos_MobiControlDeviceId] ON [Dispositivos] ([MobiControlDeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Distritos_Nombre] ON [Distritos] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentosPDF_EntregaId] ON [DocumentosPDF] ([EntregaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Empleados_CanalId] ON [Empleados] ([CanalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Empleados_Cedula] ON [Empleados] ([Cedula]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_Empleados_DistritoId] ON [Empleados] ([DistritoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_EntregasDispositivo_CanalId] ON [EntregasDispositivo] ([CanalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_EntregasDispositivo_Dispositivo] ON [EntregasDispositivo] ([DispositivoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_EntregasDispositivo_DistritoId] ON [EntregasDispositivo] ([DistritoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_EntregasDispositivo_Empleado] ON [EntregasDispositivo] ([EmpleadoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EntregasDispositivo_EntregaUid] ON [EntregasDispositivo] ([EntregaUid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_EntregasDispositivo_EstadoId] ON [EntregasDispositivo] ([EstadoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_EntregasDispositivo_EstadoProceso] ON [EntregasDispositivo] ([EstadoProceso]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_EntregasDispositivo_Fecha] ON [EntregasDispositivo] ([FechaFirma]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UQ_EntregasDispositivo_Idempotencia] ON [EntregasDispositivo] ([ClaveIdempotencia]) WHERE [ClaveIdempotencia] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EstadosDispositivo_Nombre] ON [EstadosDispositivo] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Firmas_EntregaId] ON [Firmas] ([EntregaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_IntegracionesConfig_Proveedor_Entorno] ON [IntegracionesConfiguracion] ([Proveedor], [Entorno]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_IntegracionesSync_Entrega] ON [IntegracionesSincronizaciones] ([EntregaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    CREATE INDEX [IX_IntegracionesSync_Proveedor] ON [IntegracionesSincronizaciones] ([Proveedor]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234410_EsquemaInicial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827234410_EsquemaInicial', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234459_VistaEntregasCompletas'
)
BEGIN
    CREATE OR ALTER VIEW vw_EntregasCompletas AS
    SELECT
        e.EntregaId,
        e.EntregaUid,
        d.MobiControlDeviceId,
        d.Fabricante,
        d.Modelo,
        d.IMEI,
        emp.Cedula,
        emp.NombreCompleto,
        e.NombreAsociadoFirmante,
        est.Nombre  AS Estado,
        can.Nombre  AS Canal,
        dist.Nombre AS Distrito,
        e.ICCID,
        e.NumeroCelular,
        e.CostoEquipo,
        e.CiudadFirma,
        e.FechaEntregaProgramada,
        e.FechaFirma,
        e.EstadoProceso,
        f.UrlBlob   AS UrlFirma,
        pdf.UrlBlob AS UrlDocumentoPDF
    FROM EntregasDispositivo e
    JOIN Dispositivos d              ON d.DispositivoId = e.DispositivoId
    JOIN Empleados emp               ON emp.EmpleadoId = e.EmpleadoId
    LEFT JOIN EstadosDispositivo est ON est.EstadoId = e.EstadoId
    LEFT JOIN Canales can            ON can.CanalId = e.CanalId
    LEFT JOIN Distritos dist         ON dist.DistritoId = e.DistritoId
    LEFT JOIN Firmas f               ON f.EntregaId = e.EntregaId
    LEFT JOIN DocumentosPDF pdf      ON pdf.EntregaId = e.EntregaId;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827234459_VistaEntregasCompletas'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827234459_VistaEntregasCompletas', N'10.0.11');
END;

COMMIT;
GO

