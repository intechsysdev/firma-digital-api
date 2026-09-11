namespace MobiControlFirma.Application.Common.Interfaces;

/// <summary>
/// Empresa a la que pertenece la petición en curso. Se resuelve una vez por petición —de la
/// sesión del usuario o de la llave del equipo— y el contexto de datos filtra por ella sola.
///
/// Es a propósito que el aislamiento no dependa de que cada consulta recuerde filtrar: basta
/// olvidarlo una vez para que una empresa vea las actas de otra.
/// </summary>
public interface IContextoEmpresa
{
    /// <summary>Empresa de la petición, o null cuando quien llama es el superadministrador.</summary>
    int? EmpresaId { get; }

    /// <summary>El superadministrador no pertenece a ninguna empresa y las ve todas.</summary>
    bool EsSuperAdministrador { get; }

    /// <summary>
    /// Empresa de la petición, reventando si no hay ninguna. Lo usa el código que escribe: un
    /// registro sin empresa no tiene dueño y no debe llegar a la base.
    /// </summary>
    int EmpresaRequerida { get; }
}
