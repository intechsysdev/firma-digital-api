using MobiControlFirma.Domain.Enums;

namespace MobiControlFirma.Domain.Entities;

/// <summary>
/// Copia del acta pendiente de enviar. Es una bandeja de salida, no una llamada directa: el
/// asociado está esperando con el equipo en la mano cuando firma, así que el correo se encola
/// dentro de la misma transacción que guarda el acta y sale después, en segundo plano.
///
/// Así un proveedor lento o caído no retrasa la firma ni la pierde, y cada intento queda
/// registrado con su error para poder reintentar a sabiendas.
/// </summary>
public class EnvioCorreo : IDeEmpresa
{
    public int EnvioId { get; set; }

    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int EntregaId { get; set; }
    public EntregaDispositivo Entrega { get; set; } = null!;

    /// <summary>Destinatarios ya resueltos, separados por coma. Se congelan al encolar: si la
    /// empresa cambia su lista después, esta copia sigue yendo a quien correspondía entonces.</summary>
    public string Destinatarios { get; set; } = string.Empty;

    public string Asunto { get; set; } = string.Empty;

    public EstadoEnvioCorreo Estado { get; set; } = EstadoEnvioCorreo.PENDIENTE;

    public int Intentos { get; set; }
    public string? UltimoError { get; set; }

    /// <summary>Cuándo volver a intentarlo. Se espacia tras cada fallo.</summary>
    public DateTime? ProximoIntento { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
}
