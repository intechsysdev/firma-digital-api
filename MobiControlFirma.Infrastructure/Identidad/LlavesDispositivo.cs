using System.Security.Cryptography;
using System.Text;

namespace MobiControlFirma.Infrastructure.Identidad;

/// <summary>
/// Llaves que llevan instaladas los equipos de cada empresa. Se generan aquí y se guardan
/// resumidas: la base nunca contiene la llave con la que se podrían registrar actas.
/// </summary>
public static class LlavesDispositivo
{
    /// <summary>Caracteres que se ven en la consola antes de ocultar el resto.</summary>
    public const int LargoPrefijo = 8;

    /// <summary>
    /// Genera una llave nueva. 32 bytes en base64 seguro para URL: entra en una cabecera y en
    /// un atributo de MobiControl sin necesidad de escaparla.
    /// </summary>
    public static string Generar()
    {
        Span<byte> aleatorio = stackalloc byte[32];
        RandomNumberGenerator.Fill(aleatorio);

        return Convert.ToBase64String(aleatorio)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static byte[] Resumir(string llave) => SHA256.HashData(Encoding.UTF8.GetBytes(llave));

    public static string Prefijo(string llave) =>
        llave.Length <= LargoPrefijo ? llave : llave[..LargoPrefijo];
}
