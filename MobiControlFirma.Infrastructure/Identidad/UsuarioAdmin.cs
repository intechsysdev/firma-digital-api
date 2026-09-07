using Microsoft.AspNetCore.Identity;

namespace MobiControlFirma.Infrastructure.Identidad;

/// <summary>
/// Usuario de la consola administrativa. Se guarda en las tablas estándar de ASP.NET Identity
/// (AspNetUsers y compañía), que conviven en la misma base que las entregas.
///
/// Es una identidad distinta de las llaves de API: esas siguen siendo para el formulario que
/// corre en el dispositivo, donde no hay nadie que pueda iniciar sesión.
/// </summary>
public class UsuarioAdmin : IdentityUser
{
    /// <summary>Nombre para mostrar en la consola. El correo queda como credencial.</summary>
    public string? NombreCompleto { get; set; }
}
