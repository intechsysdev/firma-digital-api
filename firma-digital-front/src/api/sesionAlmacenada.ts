const CLAVE = "firma.sesion";

export interface Sesion {
  accessToken: string;
  refreshToken: string;
  correo: string;
}

export function leerSesion(): Sesion | null {
  try {
    const crudo = localStorage.getItem(CLAVE);
    return crudo ? (JSON.parse(crudo) as Sesion) : null;
  } catch {
    return null;
  }
}

export function guardarSesion(sesion: Sesion) {
  localStorage.setItem(CLAVE, JSON.stringify(sesion));
}

export function borrarSesion() {
  localStorage.removeItem(CLAVE);
}
