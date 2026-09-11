namespace MobiControlFirma.Domain.Entities;

/// <summary>Zona geográfica/comercial a la que pertenece el asociado.</summary>
public class Distrito : IDeEmpresa
{
    public int DistritoId { get; set; }

    /// <summary>Empresa dueña del registro. El contexto filtra por aquí en cada consulta.</summary>
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

/// <summary>Canal comercial del asociado (moto, tienda, call center, etc.).</summary>
public class Canal : IDeEmpresa
{
    public int CanalId { get; set; }

    /// <summary>Empresa dueña del registro. El contexto filtra por aquí en cada consulta.</summary>
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

/// <summary>Condición física del equipo al entregarlo: Nuevo, Usado, Reacondicionado…</summary>
public class EstadoDispositivo : IDeEmpresa
{
    public int EstadoId { get; set; }

    /// <summary>Empresa dueña del registro. El contexto filtra por aquí en cada consulta.</summary>
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
}
