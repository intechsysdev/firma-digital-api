namespace MobiControlFirma.Domain.Entities;

/// <summary>Zona geográfica/comercial a la que pertenece el asociado.</summary>
public class Distrito
{
    public int DistritoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

/// <summary>Canal comercial del asociado (moto, tienda, call center, etc.).</summary>
public class Canal
{
    public int CanalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

/// <summary>Condición física del equipo al entregarlo: Nuevo, Usado, Reacondicionado…</summary>
public class EstadoDispositivo
{
    public int EstadoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
