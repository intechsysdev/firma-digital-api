import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { avisarAlPerderSesion, iniciarSesion } from "../api/cliente";
import { borrarSesion, leerSesion } from "../api/sesionAlmacenada";

interface Contexto {
  correo: string | null;
  autenticado: boolean;
  entrar: (correo: string, clave: string) => Promise<void>;
  salir: () => void;
}

const SesionContexto = createContext<Contexto | null>(null);

export function ProveedorSesion({ children }: { children: ReactNode }) {
  const [correo, setCorreo] = useState<string | null>(() => leerSesion()?.correo ?? null);

  const salir = useCallback(() => {
    borrarSesion();
    setCorreo(null);
  }, []);

  // El cliente HTTP avisa cuando el token dejó de servir y no se pudo renovar, para que la
  // consola vuelva al login en vez de quedarse mostrando tablas vacías.
  useEffect(() => avisarAlPerderSesion(() => setCorreo(null)), []);

  const entrar = useCallback(async (nuevoCorreo: string, clave: string) => {
    await iniciarSesion(nuevoCorreo, clave);
    setCorreo(nuevoCorreo);
  }, []);

  const valor = useMemo<Contexto>(
    () => ({ correo, autenticado: correo !== null, entrar, salir }),
    [correo, entrar, salir],
  );

  return <SesionContexto.Provider value={valor}>{children}</SesionContexto.Provider>;
}

export function useSesion(): Contexto {
  const contexto = useContext(SesionContexto);
  if (!contexto) throw new Error("useSesion debe usarse dentro de ProveedorSesion.");
  return contexto;
}
