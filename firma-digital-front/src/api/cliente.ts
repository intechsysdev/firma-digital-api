import { borrarSesion, guardarSesion, leerSesion } from "./sesionAlmacenada";

/**
 * En desarrollo el front corre en su propio puerto y Vite hace de proxy hacia el API. En
 * producción ambos se sirven desde el mismo App Service, así que la ruta relativa basta.
 */
const BASE = import.meta.env.VITE_API_URL ?? "";

export class ErrorApi extends Error {
  readonly estado: number;

  constructor(estado: number, mensaje: string) {
    super(mensaje);
    this.estado = estado;
  }
}

/** Se dispara cuando la sesión deja de servir, para que la app vuelva al login. */
let alPerderSesion: (() => void) | null = null;
export function avisarAlPerderSesion(accion: () => void) {
  alPerderSesion = accion;
}

async function mensajeDeError(respuesta: Response): Promise<string> {
  try {
    const cuerpo = await respuesta.json();
    return cuerpo?.message ?? cuerpo?.title ?? `El servidor respondió ${respuesta.status}.`;
  } catch {
    return `El servidor respondió ${respuesta.status}.`;
  }
}

/**
 * El token de acceso de Identity dura poco a propósito. Antes de dar una sesión por perdida se
 * intenta una vez con el de refresco: sin esto, la consola echaría al usuario cada hora.
 */
async function renovar(): Promise<boolean> {
  const sesion = leerSesion();
  if (!sesion?.refreshToken) return false;

  const respuesta = await fetch(`${BASE}/api/v1/cuenta/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: sesion.refreshToken }),
  });

  if (!respuesta.ok) return false;

  const datos = await respuesta.json();
  guardarSesion({ ...sesion, accessToken: datos.accessToken, refreshToken: datos.refreshToken });
  return true;
}

async function pedir(ruta: string, opciones: RequestInit, reintentar = true): Promise<Response> {
  const sesion = leerSesion();
  const cabeceras = new Headers(opciones.headers);
  if (sesion?.accessToken) cabeceras.set("Authorization", `Bearer ${sesion.accessToken}`);

  const respuesta = await fetch(`${BASE}${ruta}`, { ...opciones, headers: cabeceras });

  if (respuesta.status === 401 && reintentar) {
    if (await renovar()) return pedir(ruta, opciones, false);

    borrarSesion();
    alPerderSesion?.();
  }

  return respuesta;
}

export async function obtenerJson<T>(ruta: string): Promise<T> {
  const respuesta = await pedir(ruta, { method: "GET" });
  if (!respuesta.ok) throw new ErrorApi(respuesta.status, await mensajeDeError(respuesta));
  return (await respuesta.json()) as T;
}

export async function enviarJson<T>(ruta: string, cuerpo?: unknown): Promise<T> {
  const respuesta = await pedir(ruta, {
    method: "POST",
    headers: cuerpo === undefined ? {} : { "Content-Type": "application/json" },
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });

  if (!respuesta.ok) throw new ErrorApi(respuesta.status, await mensajeDeError(respuesta));
  return (await respuesta.json()) as T;
}

/**
 * Descarga un archivo protegido y lo entrega como URL de objeto. Se hace así y no con un
 * <img src> o un enlace directo porque el token viaja en la cabecera: en la URL quedaría
 * escrito en el historial del navegador y en los registros del servidor.
 */
export async function obtenerArchivo(ruta: string): Promise<string> {
  const respuesta = await pedir(ruta, { method: "GET" });
  if (!respuesta.ok) throw new ErrorApi(respuesta.status, await mensajeDeError(respuesta));
  return URL.createObjectURL(await respuesta.blob());
}

export async function iniciarSesion(correo: string, clave: string): Promise<void> {
  const respuesta = await fetch(`${BASE}/api/v1/cuenta/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email: correo, password: clave }),
  });

  if (!respuesta.ok) {
    throw new ErrorApi(
      respuesta.status,
      respuesta.status === 401 ? "Correo o contraseña incorrectos." : await mensajeDeError(respuesta),
    );
  }

  const datos = await respuesta.json();
  guardarSesion({ accessToken: datos.accessToken, refreshToken: datos.refreshToken, correo });
}
