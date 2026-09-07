/** Espejo de los DTO del API. Si cambian allá, esto se rompe en compilación y no en producción. */

export type EstadoProceso = "FIRMADO" | "SINCRONIZADO" | "ERROR_SINCRONIZACION";

export interface Entrega {
  entregaUid: string;
  entregaId: number;
  deviceId: string;
  fabricante: string | null;
  modelo: string | null;
  imei: string | null;
  cedula: string;
  nombreCompleto: string;
  nombreAsociadoFirmante: string;
  estado: string | null;
  canal: string | null;
  distrito: string | null;
  fechaFirma: string;
  estadoProceso: EstadoProceso;
  urlPdf: string;
  urlFirma: string;
}

export interface Pagina<T> {
  items: T[];
  total: number;
  pagina: number;
  tamanoPagina: number;
}

export interface FiltrosEntregas {
  busqueda?: string;
  desde?: string;
  hasta?: string;
  estadoProceso?: string;
  pagina: number;
  tamanoPagina: number;
}

export interface ResultadoSincronizacion {
  accion: string;
  exitoso: boolean;
  codigoHttp: number | null;
  detalle: string | null;
}
