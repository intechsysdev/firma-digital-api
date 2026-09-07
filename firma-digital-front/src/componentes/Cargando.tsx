export function Cargando({ texto = "Cargando…" }: { texto?: string }) {
  return (
    <div className="cargando">
      <span className="girador" aria-hidden="true" />
      {texto}
    </div>
  );
}

export function Aviso({ tipo, children }: { tipo: "error" | "info"; children: React.ReactNode }) {
  return <div className={`aviso aviso-${tipo}`}>{children}</div>;
}
