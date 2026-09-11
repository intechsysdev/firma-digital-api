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

    /// <summary>
    /// Empresa a la que pertenece. En null solo para el superadministrador, que no es de
    /// ninguna: es quien da de alta a las demás y por eso las ve todas.
    /// </summary>
    public int? EmpresaId { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}

/// <summary>Roles de la consola. Constantes porque viajan en claims y en atributos.</summary>
public static class Roles
{
    /// <summary>Crea empresas y usuarios de cualquier empresa. No pertenece a ninguna.</summary>
    public const string SuperAdministrador = "SuperAdministrador";

    /// <summary>Administra su propia empresa: sus usuarios y sus actas.</summary>
    public const string AdministradorEmpresa = "AdministradorEmpresa";

    public static readonly string[] Todos = [SuperAdministrador, AdministradorEmpresa];
}

/// <summary>Nombres de los claims propios que viajan en el token.</summary>
public static class ClaimsPropios
{
    public const string Empresa = "empresa";
}
