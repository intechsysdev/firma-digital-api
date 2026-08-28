namespace MobiControlFirma.Domain.Enums;

/// <summary>Estado del ciclo de vida de un acta de entrega.</summary>
public enum EstadoProceso
{
    /// <summary>El acta quedó guardada con su firma, pero MobiControl todavía no la conoce.</summary>
    FIRMADO,

    /// <summary>Se actualizaron los atributos personalizados del equipo y se pidió el check-in.</summary>
    SINCRONIZADO,

    /// <summary>La firma está a salvo pero MobiControl rechazó o no respondió la sincronización.</summary>
    ERROR_SINCRONIZACION,
}

/// <summary>Sistemas externos con los que conversa el API.</summary>
public enum ProveedorIntegracion
{
    MOBICONTROL,
    INFOBIP,
    GUPSHUP,
}

/// <summary>Forma en la que se autentica cada proveedor externo.</summary>
public enum TipoAutenticacion
{
    OAUTH_PASSWORD,
    API_KEY,
    BASIC,
}

/// <summary>
/// Acciones registradas en la bitácora de integraciones. Se dejan como constantes de texto
/// (y no como enum) porque la columna es libre a propósito: cada proveedor que se sume después
/// trae sus propias acciones y no debería obligar a un cambio de esquema.
/// </summary>
public static class TipoAccionIntegracion
{
    public const string ObtenerToken = "OBTENER_TOKEN";
    public const string ActualizarAtributos = "ACTUALIZAR_ATRIBUTOS";
    public const string CheckIn = "CHECKIN";
    public const string EnvioWhatsApp = "ENVIO_WHATSAPP";
    public const string EnvioSms = "ENVIO_SMS";
    public const string EnvioOtp = "ENVIO_OTP";
    public const string VerificacionOtp = "VERIFICACION_OTP";
}
