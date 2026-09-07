namespace MobiControlFirma.API.Configuration;

/// <summary>Ajustes generales del API.</summary>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Orígenes permitidos por CORS. El formulario se instala en el dispositivo con
    /// MobiControl y el navegador lo abre desde un archivo local, así que su origen llega
    /// como "null"; ese caso se maneja aparte en Program.cs.
    /// </summary>
    public string[] CorsOrigins { get; set; } = [];
}

/// <summary>
/// Llaves de acceso al API. Se separan por rol porque el formulario del dispositivo solo
/// necesita registrar actas: si su llave se filtrara, con ella no se puede leer el histórico
/// de cédulas y firmas de toda la compañía.
/// </summary>
public class SeguridadOptions
{
    public const string SectionName = "Seguridad";

    /// <summary>Llave que usa el formulario instalado en los equipos.</summary>
    public string? ApiKeyDispositivo { get; set; }

    /// <summary>Llave para consultas administrativas y reintentos de sincronización.</summary>
    public string? ApiKeyAdministrador { get; set; }

    /// <summary>
    /// Correo del primer usuario de la consola. Se crea al arrancar solo si todavía no hay
    /// ninguno: sin él nadie podría entrar a dar de alta al resto. Si se deja vacío no se
    /// crea nada y el alta queda a cargo de la llave de administrador.
    /// </summary>
    public string? UsuarioInicial { get; set; }

    /// <summary>Contraseña del usuario inicial. Conviene cambiarla en el primer ingreso.</summary>
    public string? ClaveInicial { get; set; }
}
