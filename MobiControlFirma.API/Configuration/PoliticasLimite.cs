namespace MobiControlFirma.API.Configuration;

/// <summary>
/// Nombres de las políticas del limitador. Viven aquí porque los usan tanto el arranque, que
/// las define, como los controladores que las exigen: con cadenas sueltas en cada lado, un
/// cambio de nombre dejaría un endpoint sin límite y sin aviso de compilación.
/// </summary>
public static class PoliticasLimite
{
    /// <summary>Registro de actas: cuesta un PDF y tres llamadas a MobiControl.</summary>
    public const string Firmas = "firmas";

    /// <summary>Resto del API, sobre todo lecturas de la consola.</summary>
    public const string General = "general";

    /// <summary>Ingreso y renovación de sesión.</summary>
    public const string Cuenta = "cuenta";
}
