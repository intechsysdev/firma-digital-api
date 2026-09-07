import { useState } from "react";
import type { FormEvent } from "react";
import { useSesion } from "../sesion/SesionContexto";
import { Aviso } from "../componentes/Cargando";

export function Login() {
  const { entrar } = useSesion();
  const [correo, setCorreo] = useState("");
  const [clave, setClave] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    setError(null);
    setEnviando(true);

    try {
      await entrar(correo.trim(), clave);
    } catch (e) {
      setError(e instanceof Error ? e.message : "No se pudo iniciar sesión.");
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="login">
      <form className="login-caja" onSubmit={enviar}>
        <h1>Actas de entrega</h1>
        <p className="login-sub">Consola de administración</p>

        {error && <Aviso tipo="error">{error}</Aviso>}

        <label htmlFor="correo">Correo</label>
        <input
          id="correo"
          type="email"
          autoComplete="username"
          required
          value={correo}
          onChange={(e) => setCorreo(e.target.value)}
        />

        <label htmlFor="clave">Contraseña</label>
        <input
          id="clave"
          type="password"
          autoComplete="current-password"
          required
          value={clave}
          onChange={(e) => setClave(e.target.value)}
        />

        <button type="submit" className="boton boton-primario" disabled={enviando}>
          {enviando ? "Entrando…" : "Entrar"}
        </button>
      </form>
    </div>
  );
}
