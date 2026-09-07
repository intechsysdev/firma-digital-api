import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { descargarFirma, descargarPdf, obtenerEntrega, reintentarSincronizacion } from "../api/entregas";
import type { Entrega, ResultadoSincronizacion } from "../api/tipos";
import { Aviso, Cargando } from "../componentes/Cargando";
import { EtiquetaEstado, fecha } from "../componentes/Etiquetas";

export function DetalleEntrega() {
  const { uid = "" } = useParams();

  const [entrega, setEntrega] = useState<Entrega | null>(null);
  const [pdf, setPdf] = useState<string | null>(null);
  const [firma, setFirma] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);
  const [sincronizando, setSincronizando] = useState(false);
  const [resultados, setResultados] = useState<ResultadoSincronizacion[] | null>(null);

  const cargar = useCallback(async () => {
    setCargando(true);
    setError(null);

    try {
      setEntrega(await obtenerEntrega(uid));
    } catch (e) {
      setError(e instanceof Error ? e.message : "No se pudo cargar el acta.");
      setCargando(false);
      return;
    }

    // El acta y la firma se piden por separado y sin tumbar la vista: una entrega vieja puede
    // haber perdido el archivo y aun así interesa ver sus datos.
    try {
      setPdf(await descargarPdf(uid));
    } catch {
      setPdf(null);
    }

    try {
      setFirma(await descargarFirma(uid));
    } catch {
      setFirma(null);
    }

    setCargando(false);
  }, [uid]);

  useEffect(() => {
    void cargar();
  }, [cargar]);

  // Las URL de objeto ocupan memoria hasta que se revocan: sin esto, pasear por el listado
  // abriendo actas va dejando PDFs retenidos en la pestaña.
  useEffect(() => () => { if (pdf) URL.revokeObjectURL(pdf); }, [pdf]);
  useEffect(() => () => { if (firma) URL.revokeObjectURL(firma); }, [firma]);

  async function sincronizar() {
    setSincronizando(true);
    setResultados(null);

    try {
      setResultados(await reintentarSincronizacion(uid));
      setEntrega(await obtenerEntrega(uid));
    } catch (e) {
      setError(e instanceof Error ? e.message : "No se pudo reintentar la sincronización.");
    } finally {
      setSincronizando(false);
    }
  }

  if (cargando) return <Cargando texto="Cargando el acta…" />;
  if (error && !entrega) return <Aviso tipo="error">{error}</Aviso>;
  if (!entrega) return null;

  return (
    <section>
      <header className="cabecera-seccion">
        <div>
          <Link className="volver" to="/entregas">
            ← Volver al listado
          </Link>
          <h1>{entrega.nombreAsociadoFirmante || entrega.nombreCompleto}</h1>
        </div>
        <EtiquetaEstado estado={entrega.estadoProceso} />
      </header>

      {error && <Aviso tipo="error">{error}</Aviso>}

      <div className="detalle">
        <div className="ficha">
          <h2>Datos del acta</h2>
          <dl>
            <dt>Acta</dt>
            <dd className="mono">{entrega.entregaUid.slice(0, 8).toUpperCase()}</dd>
            <dt>Fecha de firma</dt>
            <dd>{fecha(entrega.fechaFirma)}</dd>
            <dt>Cédula</dt>
            <dd className="mono">{entrega.cedula}</dd>
            <dt>Equipo</dt>
            <dd>{[entrega.fabricante, entrega.modelo].filter(Boolean).join(" ") || "—"}</dd>
            <dt>IMEI</dt>
            <dd className="mono">{entrega.imei ?? "—"}</dd>
            <dt>Device ID</dt>
            <dd className="mono">{entrega.deviceId}</dd>
            <dt>Estado del equipo</dt>
            <dd>{entrega.estado ?? "—"}</dd>
            <dt>Canal</dt>
            <dd>{entrega.canal ?? "—"}</dd>
            <dt>Distrito</dt>
            <dd>{entrega.distrito ?? "—"}</dd>
          </dl>

          <h2>Firma</h2>
          {firma ? (
            <img className="firma" src={firma} alt="Firma del asociado" />
          ) : (
            <Aviso tipo="info">La imagen de la firma no está disponible.</Aviso>
          )}

          {entrega.estadoProceso !== "SINCRONIZADO" && (
            <>
              <h2>MobiControl</h2>
              <button
                type="button"
                className="boton boton-primario"
                onClick={sincronizar}
                disabled={sincronizando}
              >
                {sincronizando ? "Sincronizando…" : "Reintentar sincronización"}
              </button>
            </>
          )}

          {resultados && (
            <ul className="resultados">
              {resultados.map((r, i) => (
                <li key={i} className={r.exitoso ? "ok" : "falla"}>
                  <strong>{r.accion}</strong>
                  {r.codigoHttp ? ` · ${r.codigoHttp}` : ""}
                  {r.detalle ? ` · ${r.detalle}` : ""}
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="visor">
          <div className="visor-barra">
            <h2>Acta en PDF</h2>
            {pdf && (
              <a className="boton boton-secundario boton-chico" href={pdf} download={`acta-${entrega.entregaUid.slice(0, 8)}.pdf`}>
                Descargar
              </a>
            )}
          </div>

          {pdf ? (
            <iframe className="marco-pdf" src={pdf} title="Acta en PDF" />
          ) : (
            <Aviso tipo="info">
              El PDF de esta acta no está disponible en el servidor. Las actas firmadas antes del
              3 de septiembre de 2026 se perdieron al desplegar, porque se guardaban dentro de la
              carpeta que el despliegue vacía.
            </Aviso>
          )}
        </div>
      </div>
    </section>
  );
}
