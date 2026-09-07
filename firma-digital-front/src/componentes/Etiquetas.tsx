import type { EstadoProceso } from "../api/tipos";

const TEXTOS: Record<EstadoProceso, string> = {
  FIRMADO: "Firmado",
  SINCRONIZADO: "Sincronizado",
  ERROR_SINCRONIZACION: "Error de sincronización",
};

export function EtiquetaEstado({ estado }: { estado: EstadoProceso }) {
  return <span className={`etiqueta etiqueta-${estado.toLowerCase()}`}>{TEXTOS[estado] ?? estado}</span>;
}

export function fecha(valor: string): string {
  const d = new Date(valor);
  return Number.isNaN(d.getTime())
    ? valor
    : d.toLocaleString("es-CO", { dateStyle: "medium", timeStyle: "short" });
}
