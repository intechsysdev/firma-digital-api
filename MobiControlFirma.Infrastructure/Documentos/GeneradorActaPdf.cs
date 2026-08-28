using System.Globalization;
using MobiControlFirma.Application.Common.Interfaces;
using MobiControlFirma.Application.Entregas;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MobiControlFirma.Infrastructure.Documentos;

/// <summary>
/// Arma el acta en PDF desde el servidor.
///
/// Antes la generaba el dispositivo con html2canvas + jsPDF: el resultado era una fotografía
/// del formulario, sin texto seleccionable, con la paginación cortando párrafos a la mitad, y
/// dependía de dos librerías descargadas de un CDN que en un equipo con datos restringidos
/// simplemente no cargaban. Generarlo aquí da un PDF real, idéntico para todos los equipos.
/// </summary>
public class GeneradorActaPdf : IGeneradorActaPdf
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    private const string TextoObligaciones1 =
        "Constituyen obligación del TENEDOR utilizar como herramienta de trabajo el mencionado equipo; " +
        "por lo tanto, está obligado a mantenerlo encendido durante el tiempo que esté laborando.";

    private const string TextoObligaciones2 =
        "Devolver en buen estado el equipo en caso de retiro de HV, de acuerdo con el procedimiento " +
        "GF-GTI-1754 Administración de dispositivos móviles.";

    private const string TextoObligaciones3 =
        "El Trabajador no podrá prestar el equipo a terceras personas sin previa autorización de LA EMPRESA.";

    private const string TextoObligaciones4 =
        "En caso de robo o pérdida del equipo, procederá de acuerdo con las siguientes instrucciones:";

    private const string TextoObligaciones4A =
        "Llamar al operador celular de inmediato para solicitar la desconexión de la unidad a través del *611 " +
        "desde un celular o a través de la persona encargada en Tecnología Informática.";

    private const string TextoObligaciones4B = "Notificar al Jefe Inmediato.";

    private const string TextoObligaciones4C =
        "Colocar el denuncio y enviarlo a la persona encargada en Tecnología Informática, de acuerdo con el " +
        "procedimiento GF-GTI-1754 Administración de dispositivos móviles.";

    private const string TextoObligaciones5 =
        "Hacer uso del equipo adoptando las POLÍTICAS detalladas en el procedimiento GF.GTI-620 " +
        "SOLICITUD DE REQUERIMIENTOS A TIC.";

    private const string TextoTercera =
        "En caso de pérdida, robo, daño del equipo durante su vida útil o en los eventos que se presenten " +
        "consumos no autorizados (mensajes de texto SMS, MMS, descargas de tonos, videos e imágenes, navegación " +
        "en páginas no autorizadas), EL TENEDOR autoriza a la empresa a descontar de sus salarios, prestaciones " +
        "sociales comunes y especiales, cesantías, intereses a las cesantías, primas, indemnizaciones, pensiones, " +
        "beneficios y demás derechos generados a raíz de su vinculación laboral con la empresa, los dineros " +
        "necesarios para cubrir el valor de la sanción de acuerdo con la condición CUARTA de este documento y los " +
        "gastos de consumos no autorizados que sean facturados por la empresa operadora del servicio móvil.";

    private const string TextoCuarta =
        "Las sanciones por evento en caso de robo, pérdida o daño imputado al usuario serán las siguientes:";

    private const string TextoCuartaA = "La primera vez, la empresa asumirá el 50% del costo y el empleado el otro 50%.";
    private const string TextoCuartaB = "La segunda vez, la empresa descontará el 100% del costo del equipo.";

    public byte[] Generar(DatosActa datos, byte[] firmaPng)
    {
        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(2, Unit.Centimetre);
                pagina.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.35f).FontColor("#1F2933"));

                pagina.Header().Element(c => Encabezado(c, datos));
                pagina.Content().PaddingVertical(12).Element(c => Cuerpo(c, datos, firmaPng));
                pagina.Footer().Element(c => PiePagina(c, datos));
            });
        });

        return documento.GeneratePdf();
    }

    private static void Encabezado(IContainer contenedor, DatosActa datos)
    {
        contenedor.BorderBottom(1).BorderColor("#D9E2EC").PaddingBottom(8).Row(fila =>
        {
            fila.RelativeItem().Column(columna =>
            {
                columna.Item().Text("ACTA DE ENTREGA DE DISPOSITIVO MÓVIL")
                    .FontSize(13).SemiBold().FontColor("#102A43");
                columna.Item().Text("Documento firmado digitalmente")
                    .FontSize(8).FontColor("#627D98");
            });

            fila.ConstantItem(160).AlignRight().Column(columna =>
            {
                columna.Item().Text($"No. {datos.EntregaUid.ToString()[..8].ToUpperInvariant()}")
                    .FontSize(9).SemiBold().FontColor("#102A43");
                columna.Item().Text(datos.FechaFirma.ToString("dd/MM/yyyy HH:mm", Colombia))
                    .FontSize(8).FontColor("#627D98");
            });
        });
    }

    private static void PiePagina(IContainer contenedor, DatosActa datos)
    {
        contenedor.BorderTop(1).BorderColor("#D9E2EC").PaddingTop(6).Row(fila =>
        {
            fila.RelativeItem().Text(datos.EntregaUid.ToString()).FontSize(7).FontColor("#9FB3C8");
            fila.ConstantItem(90).AlignRight().Text(texto =>
            {
                texto.DefaultTextStyle(x => x.FontSize(7).FontColor("#9FB3C8"));
                texto.Span("Página ");
                texto.CurrentPageNumber();
                texto.Span(" de ");
                texto.TotalPages();
            });
        });
    }

    private static void Cuerpo(IContainer contenedor, DatosActa datos, byte[] firmaPng)
    {
        contenedor.Column(columna =>
        {
            columna.Spacing(10);

            columna.Item().Text(texto =>
            {
                texto.Span("El suscrito ");
                texto.Span(datos.NombreTenedor).SemiBold();
                texto.Span(", identificado con la cédula de ciudadanía No ");
                texto.Span(datos.Cedula).SemiBold();
                texto.Span(" (en adelante EL TENEDOR) y HV. (en adelante LA EMPRESA) suscribimos el presente " +
                           "documento para la entrega de un DISPOSITIVO MÓVIL, acorde a las siguientes condiciones:");
            });

            columna.Item().Text(texto =>
            {
                texto.Span("PRIMERA: OBJETO: ").SemiBold();
                texto.Span("El presente documento tiene por objeto el suministro y entrega de un dispositivo " +
                           "móvil cuyas características se detallan en el presente documento así:");
            });

            columna.Item().Element(c => TablaDatos(c, datos));

            if (!string.IsNullOrWhiteSpace(datos.Entregables))
            {
                columna.Item().Column(bloque =>
                {
                    bloque.Item().Text("Otros entregables").SemiBold().FontSize(10).FontColor("#102A43");
                    bloque.Item().PaddingTop(2).Text(datos.Entregables);
                });
            }

            columna.Item().Element(Clausulas);
            columna.Item().PaddingTop(6).Text(texto =>
            {
                texto.Span("En señal de conformidad las partes suscriben el presente documento en la ciudad de ");
                texto.Span(datos.CiudadFirma).SemiBold();
                texto.Span(" al ");
                texto.Span(datos.FechaFirma.ToString("dd 'de' MMMM 'de' yyyy", Colombia)).SemiBold();
                texto.Span(".");
            });

            columna.Item().Element(c => BloqueFirma(c, datos, firmaPng));
        });
    }

    private static void TablaDatos(IContainer contenedor, DatosActa datos)
    {
        contenedor.Background("#F7F9FC").Border(1).BorderColor("#D9E2EC").Padding(12).Column(columna =>
        {
            columna.Item().PaddingBottom(6).Text("Datos del equipo")
                .SemiBold().FontSize(10).FontColor("#102A43");

            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(95);
                    c.RelativeColumn();
                    c.ConstantColumn(95);
                    c.RelativeColumn();
                });

                Fila(tabla, "Marca", datos.Fabricante, "Modelo", datos.Modelo);
                Fila(tabla, "IMEI", datos.Imei, "SIMCARD", datos.Iccid);
                Fila(tabla, "No. Celular", datos.NumeroCelular, "Estado", datos.Estado);
                Fila(tabla, "Canal", datos.Canal, "Distrito", datos.Distrito);
                Fila(tabla, "Costo equipo", FormatearMoneda(datos.CostoEquipo), "ID dispositivo", datos.DeviceId);
            });
        });

        static void Fila(TableDescriptor tabla, string etiquetaA, string? valorA, string etiquetaB, string? valorB)
        {
            tabla.Cell().PaddingVertical(2).Text(etiquetaA).SemiBold().FontSize(8.5f).FontColor("#486581");
            tabla.Cell().PaddingVertical(2).Text(Mostrar(valorA));
            tabla.Cell().PaddingVertical(2).Text(etiquetaB).SemiBold().FontSize(8.5f).FontColor("#486581");
            tabla.Cell().PaddingVertical(2).Text(Mostrar(valorB));
        }
    }

    private static void Clausulas(IContainer contenedor)
    {
        contenedor.Column(columna =>
        {
            columna.Spacing(8);

            columna.Item().Text("SEGUNDA: OBLIGACIONES DEL TENEDOR (TRABAJADOR):")
                .SemiBold().FontSize(10).FontColor("#102A43");

            columna.Item().Column(lista =>
            {
                lista.Spacing(4);
                Numeral(lista, "1.", TextoObligaciones1);
                Numeral(lista, "2.", TextoObligaciones2);
                Numeral(lista, "3.", TextoObligaciones3);
                Numeral(lista, "4.", TextoObligaciones4);

                lista.Item().PaddingLeft(20).Column(sub =>
                {
                    sub.Spacing(3);
                    Numeral(sub, "a.", TextoObligaciones4A);
                    Numeral(sub, "b.", TextoObligaciones4B);
                    Numeral(sub, "c.", TextoObligaciones4C);
                });

                Numeral(lista, "5.", TextoObligaciones5);
            });

            columna.Item().Text("TERCERA: AUTORIZACIÓN DESCUENTOS:")
                .SemiBold().FontSize(10).FontColor("#102A43");
            columna.Item().Text(TextoTercera).Justify();

            columna.Item().Text("CUARTA: SANCIONES POR EVENTO Y DESCUENTOS:")
                .SemiBold().FontSize(10).FontColor("#102A43");
            columna.Item().Text(TextoCuarta);

            columna.Item().Column(lista =>
            {
                lista.Spacing(3);
                Numeral(lista, "•", TextoCuartaA);
                Numeral(lista, "•", TextoCuartaB);
            });
        });

        static void Numeral(ColumnDescriptor columna, string marca, string texto)
        {
            columna.Item().Row(fila =>
            {
                fila.ConstantItem(18).Text(marca).SemiBold().FontColor("#486581");
                fila.RelativeItem().Text(texto).Justify();
            });
        }
    }

    private static void BloqueFirma(IContainer contenedor, DatosActa datos, byte[] firmaPng)
    {
        // El bloque de la firma nunca debe quedar solo al final de una página ni partido: es la
        // parte del acta que vale como prueba, así que se mantiene entero.
        contenedor.ShowEntire().PaddingTop(16).Column(columna =>
        {
            columna.Item().Text("FIRMA DEL ASOCIADO").SemiBold().FontSize(10).FontColor("#102A43");

            columna.Item().PaddingTop(6).Width(260).Height(90)
                .Border(1).BorderColor("#D9E2EC").Background("#FFFFFF").Padding(6)
                .Image(firmaPng).FitArea();

            columna.Item().Width(260).BorderTop(1).BorderColor("#334E68").PaddingTop(4).Column(pie =>
            {
                pie.Item().Text(datos.NombreAsociadoFirmante).SemiBold();
                pie.Item().Text($"C.C. {datos.Cedula}").FontSize(8.5f).FontColor("#486581");
                pie.Item().Text($"Firmado el {datos.FechaFirma.ToString("dd/MM/yyyy 'a las' HH:mm", Colombia)}")
                    .FontSize(8).FontColor("#829AB1");
            });
        });
    }

    private static string Mostrar(string? valor) => string.IsNullOrWhiteSpace(valor) ? "—" : valor;

    private static string? FormatearMoneda(decimal? valor) =>
        valor is null ? null : valor.Value.ToString("C0", Colombia);
}
