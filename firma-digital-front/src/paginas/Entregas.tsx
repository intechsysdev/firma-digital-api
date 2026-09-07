import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { listarEntregas } from "../api/entregas";
import type { Entrega, FiltrosEntregas, Pagina } from "../api/tipos";
import { Aviso, Cargando } from "../componentes/Cargando";
import { EtiquetaEstado, fecha } from "../componentes/Etiquetas";

const VACIOS: FiltrosEntregas = { pagina: 1, tamanoPagina: 25 };

export function Entregas() {
  const [filtros, setFiltros] = useState<FiltrosEntregas>(VACIOS);
  const [datos, setDatos] = useState<Pagina<Entrega> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);

  const cargar = useCallback(async (aplicar: FiltrosEntregas) => {
    setCargando(true);
    setError(null);

    try {
      setDatos(await listarEntregas(aplicar));
    } catch (e) {
      setError(e instanceof Error ? e.message : "No se pudieron cargar las actas.");
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    void cargar(filtros);
  }, [cargar, filtros]);

  function cambiar(campo: keyof FiltrosEntregas, valor: string) {
    // Cualquier filtro nuevo vuelve a la primera página: si no, se puede quedar pidiendo la
    // página 4 de un resultado que ahora tiene una sola.
    setFiltros((previos) => ({ ...previos, [campo]: valor || undefined, pagina: 1 }));
  }

  const paginas = datos ? Math.max(1, Math.ceil(datos.total / datos.tamanoPagina)) : 1;

  return (
    <section>
      <header className="cabecera-seccion">
        <h1>Actas de entrega</h1>
        {datos && <span className="conteo">{datos.total} registradas</span>}
      </header>

      <div className="filtros">
        <input
          type="search"
          placeholder="Cédula, nombre, IMEI o device id"
          defaultValue={filtros.busqueda ?? ""}
          onKeyDown={(e) => {
            if (e.key === "Enter") cambiar("busqueda", (e.target as HTMLInputElement).value);
          }}
          onBlur={(e) => cambiar("busqueda", e.target.value)}
        />
        <label>
          Desde
          <input type="date" value={filtros.desde ?? ""} onChange={(e) => cambiar("desde", e.target.value)} />
        </label>
        <label>
          Hasta
          <input type="date" value={filtros.hasta ?? ""} onChange={(e) => cambiar("hasta", e.target.value)} />
        </label>
        <label>
          Estado
          <select value={filtros.estadoProceso ?? ""} onChange={(e) => cambiar("estadoProceso", e.target.value)}>
            <option value="">Todos</option>
            <option value="FIRMADO">Firmado</option>
            <option value="SINCRONIZADO">Sincronizado</option>
            <option value="ERROR_SINCRONIZACION">Error de sincronización</option>
          </select>
        </label>
        <button type="button" className="boton boton-secundario" onClick={() => setFiltros(VACIOS)}>
          Limpiar
        </button>
      </div>

      {error && <Aviso tipo="error">{error}</Aviso>}
      {cargando && <Cargando texto="Cargando actas…" />}

      {!cargando && datos && datos.items.length === 0 && (
        <Aviso tipo="info">No hay actas que coincidan con los filtros.</Aviso>
      )}

      {!cargando && datos && datos.items.length > 0 && (
        <div className="tabla-envoltura">
          <table className="tabla">
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Asociado</th>
                <th>Cédula</th>
                <th>Equipo</th>
                <th>Canal</th>
                <th>Estado</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {datos.items.map((entrega) => (
                <tr key={entrega.entregaUid}>
                  <td>{fecha(entrega.fechaFirma)}</td>
                  <td>{entrega.nombreAsociadoFirmante || entrega.nombreCompleto}</td>
                  <td className="mono">{entrega.cedula}</td>
                  <td>
                    {[entrega.fabricante, entrega.modelo].filter(Boolean).join(" ") || entrega.deviceId}
                    <span className="sub mono">{entrega.imei ?? entrega.deviceId}</span>
                  </td>
                  <td>{entrega.canal ?? "—"}</td>
                  <td>
                    <EtiquetaEstado estado={entrega.estadoProceso} />
                  </td>
                  <td>
                    <Link className="boton boton-secundario boton-chico" to={`/entregas/${entrega.entregaUid}`}>
                      Ver acta
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {datos && paginas > 1 && (
        <nav className="paginacion">
          <button
            type="button"
            className="boton boton-secundario"
            disabled={filtros.pagina <= 1}
            onClick={() => setFiltros((p) => ({ ...p, pagina: p.pagina - 1 }))}
          >
            Anterior
          </button>
          <span>
            Página {datos.pagina} de {paginas}
          </span>
          <button
            type="button"
            className="boton boton-secundario"
            disabled={filtros.pagina >= paginas}
            onClick={() => setFiltros((p) => ({ ...p, pagina: p.pagina + 1 }))}
          >
            Siguiente
          </button>
        </nav>
      )}
    </section>
  );
}
