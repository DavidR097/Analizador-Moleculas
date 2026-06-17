using AnalizadorMoleculas.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AnalizadorMoleculas.Services
{
    public class ExcelService
    {
        private const int MaxCols = 9;
        private const int PreviewRows = 200;

        static ExcelService()
        {
            ExcelPackage.License.SetNonCommercialPersonal("AnalizadorMoleculas");
        }

        public List<string> ObtenerNombresHojas(string ruta)
        {
            using var package = new ExcelPackage(new FileInfo(ruta));
            return package.Workbook.Worksheets.Select(ws => ws.Name).ToList();
        }

        public ExcelLoadResult LeerResumenDosHojas(string ruta, int idxForm, int idxVent)
        {
            using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var package = new ExcelPackage(fs);

            var formuladas = LeerHojaAgregada(package.Workbook.Worksheets[idxForm]);
            var ventas = LeerHojaAgregada(package.Workbook.Worksheets[idxVent]);

            var todasLasLlaves = new List<(string atc, string lab, string prod)>();
            var llavesVistas = new HashSet<(string atc, string lab, string prod)>();
            AgregarLlavesEnOrden(formuladas.Registros.Keys, todasLasLlaves, llavesVistas);
            AgregarLlavesEnOrden(ventas.Registros.Keys, todasLasLlaves, llavesVistas);

            double totalPx = 0;
            double totalUn = 0;
            var items = new List<EspejoComparativo>();

            foreach (var llave in todasLasLlaves)
            {
                formuladas.Registros.TryGetValue(llave, out var registrosPx);
                ventas.Registros.TryGetValue(llave, out var registrosUn);

                registrosPx ??= new List<RegistroHoja>();
                registrosUn ??= new List<RegistroHoja>();

                var mapaPx = registrosPx.GroupBy(r => ObtenerClavePack(r.Pack))
                                        .ToDictionary(g => g.Key, g => g.ToList());
                var mapaUn = registrosUn.GroupBy(r => ObtenerClavePack(r.Pack))
                                        .ToDictionary(g => g.Key, g => g.ToList());

                var clavesPack = new List<string>();
                var vistasPack = new HashSet<string>();
                foreach (var r in registrosPx)
                    if (vistasPack.Add(ObtenerClavePack(r.Pack))) clavesPack.Add(ObtenerClavePack(r.Pack));
                foreach (var r in registrosUn)
                    if (vistasPack.Add(ObtenerClavePack(r.Pack))) clavesPack.Add(ObtenerClavePack(r.Pack));

                foreach (var clavePack in clavesPack)
                {
                    var listaPx = mapaPx.TryGetValue(clavePack, out var lpx) ? lpx : new List<RegistroHoja>();
                    var listaUn = mapaUn.TryGetValue(clavePack, out var lun) ? lun : new List<RegistroHoja>();

                    int filas = Math.Max(listaPx.Count, listaUn.Count);
                    for (int i = 0; i < filas; i++)
                    {
                        var px = i < listaPx.Count ? listaPx[i] : null;
                        var un = i < listaUn.Count ? listaUn[i] : null;

                        totalPx += px?.Valor ?? 0;
                        totalUn += un?.Valor ?? 0;

                        items.Add(new EspejoComparativo
                        {
                            Atc = llave.atc,
                            Laboratorio = llave.lab,
                            Producto = llave.prod,
                            PackPx = px?.Pack,
                            PackUn = un?.Pack,
                            TotalRecetado = px?.Valor ?? 0,
                            TotalVendido = un?.Valor ?? 0
                        });
                    }
                }
            }

            return new ExcelLoadResult
            {
                Items = items,
                TotalPx = totalPx,
                TotalUn = totalUn,
                FilasFormuladas = formuladas.FilasConDatos,
                FilasVentas = ventas.FilasConDatos,
                PreviewFormuladas = formuladas.Preview,
                PreviewVentas = ventas.Preview,
                IndicesFormuladas = formuladas.Indices,
                IndicesVentas = ventas.Indices
            };
        }

        public void ExportarDataTable(DataTable dt, string ruta)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Resultado");
            ws.Cells["A1"].LoadFromDataTable(dt, true);
            package.SaveAs(new FileInfo(ruta));
        }

        private HojaAgregada LeerHojaAgregada(ExcelWorksheet ws)
        {
            if (ws == null) throw new Exception("Hoja no encontrada");

            int colCount = Math.Min(ws.Dimension?.End.Column ?? 0, MaxCols);
            int rowCount = ws.Dimension?.End.Row ?? 0;
            var headers = LeerEncabezados(ws, colCount);
            var indices = DetectarIndices(headers);
            var preview = CrearPreview(headers);
            int columnaValor = Math.Min(MaxCols, colCount);
            var registros = new Dictionary<(string atc, string lab, string prod), List<RegistroHoja>>();
            int filasConDatos = 0;

            preview.BeginLoadData();
            for (int row = 2; row <= rowCount; row++)
            {
                var previewValues = filasConDatos < PreviewRows ? new object[colCount] : null;
                bool vacia = true;

                for (int col = 1; col <= colCount; col++)
                {
                    string text = CellToString(ws.Cells[row, col].Value);
                    if (text.Length > 0) vacia = false;
                    if (previewValues != null) previewValues[col - 1] = text;
                }

                if (vacia) continue;
                filasConDatos++;
                if (previewValues != null) preview.Rows.Add(previewValues);

                string atcRaw = CellToString(ws.Cells[row, indices.IdxAtc + 1].Value).ToUpperInvariant();
                string atc = atcRaw.Length >= 4 ? atcRaw[..4] : atcRaw;
                if (string.IsNullOrWhiteSpace(atc)) atc = "SIN ATC";

                string lab = NormalizarTextoAbsoluto(CellToString(ws.Cells[row, indices.IdxLab + 1].Value));
                string prod = ObtenerPrefijoProducto(CellToString(ws.Cells[row, indices.IdxProd + 1].Value));
                string pack = CellToString(ws.Cells[row, indices.IdxPack + 1].Value);
                if (string.IsNullOrWhiteSpace(pack)) pack = "SIN PACK";

                var llave = (atc, lab, prod);
                double valor = CellToDouble(ws.Cells[row, columnaValor].Value);

                if (!registros.TryGetValue(llave, out var lista))
                {
                    lista = new List<RegistroHoja>();
                    registros[llave] = lista;
                }

                lista.Add(new RegistroHoja
                {
                    Pack = pack,
                    Valor = valor
                });
            }
            preview.EndLoadData();

            return new HojaAgregada
            {
                Registros = registros,
                Preview = preview,
                FilasConDatos = filasConDatos,
                Indices = indices
            };
        }

        private static string[] LeerEncabezados(ExcelWorksheet ws, int colCount)
        {
            var headers = new string[colCount];
            for (int col = 1; col <= colCount; col++)
            {
                string header = ws.Cells[1, col].Text?.Trim();
                headers[col - 1] = string.IsNullOrWhiteSpace(header) ? $"Col_{col}" : header;
            }
            return headers;
        }

        private static DataTable CrearPreview(string[] headers)
        {
            var dt = new DataTable();
            foreach (string header in headers)
            {
                string safe = header;
                int dup = 1;
                while (dt.Columns.Contains(safe)) safe = $"{header}_{dup++}";
                dt.Columns.Add(safe);
            }
            return dt;
        }

        private static HojaIndices DetectarIndices(string[] headers)
        {
            int idxAtc = -1, idxLab = -1, idxProd = -1, idxPack = -1, idxValor = -1;
            int anioMaximo = 0;

            for (int i = 0; i < headers.Length; i++)
            {
                string n = headers[i].ToUpperInvariant();
                if (n.Contains("ATC") && idxAtc == -1) idxAtc = i;
                else if ((n.Contains("LABORATORY") || n.Contains("LAB")) && idxLab == -1) idxLab = i;
                else if ((n.Contains("PRODUCT") || n.Contains("PRODUCTO")) && idxProd == -1) idxProd = i;
                else if (n.Contains("PACK") || n.Contains("PRESENTACI")) idxPack = i;

                if (n.Contains("MAR") || n.Contains("MAT") || n.Contains("VALUE"))
                {
                    var match = Regex.Match(n, @"\d{2}");
                    if (match.Success && int.TryParse(match.Value, out int anio) && anio > anioMaximo)
                    {
                        anioMaximo = anio;
                        idxValor = i;
                    }
                }
            }

            return new HojaIndices
            {
                IdxAtc = idxAtc == -1 ? 0 : idxAtc,
                IdxLab = idxLab == -1 ? Math.Min(1, headers.Length - 1) : idxLab,
                IdxProd = idxProd == -1 ? Math.Min(2, headers.Length - 1) : idxProd,
                IdxPack = idxPack == -1 ? Math.Min(3, headers.Length - 1) : idxPack,
                IdxValor = idxValor == -1 ? headers.Length - 1 : idxValor
            };
        }

        private static void AgregarLlavesEnOrden(
            IEnumerable<(string atc, string lab, string prod)> origen,
            List<(string atc, string lab, string prod)> destino,
            HashSet<(string atc, string lab, string prod)> vistas)
        {
            foreach (var llave in origen)
            {
                if (vistas.Add(llave))
                    destino.Add(llave);
            }
        }

        private static string CellToString(object value)
        {
            if (value == null) return "";
            return value switch
            {
                double d => d.ToString(CultureInfo.InvariantCulture).Trim(),
                float f => f.ToString(CultureInfo.InvariantCulture).Trim(),
                decimal m => m.ToString(CultureInfo.InvariantCulture).Trim(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? "",
                _ => value.ToString()?.Trim() ?? ""
            };
        }

        private static double CellToDouble(object value)
        {
            if (value == null) return 0;
            if (value is double d) return d;
            if (value is float f) return f;
            if (value is decimal m) return (double)m;
            if (value is int i) return i;
            if (value is long l) return l;
            return ConvertirADouble(CellToString(value));
        }

        private static double ConvertirADouble(string valorStr)
        {
            if (string.IsNullOrWhiteSpace(valorStr)) return 0;
            valorStr = valorStr.Trim().Replace(" ", "");

            if (double.TryParse(valorStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double invariant))
                return invariant;

            valorStr = valorStr.Replace(".", "");
            valorStr = valorStr.Replace(",", ".");
            if (double.TryParse(valorStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double resultado))
                return resultado;
            return 0;
        }

        private static string NormalizarTextoAbsoluto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "SIN_DATO";

            string resultado = texto.ToUpperInvariant().Trim();
            resultado = resultado.Replace("\u00A0", " ");

            string textoNormalizado = resultado.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in textoNormalizado)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            resultado = sb.ToString().Normalize(NormalizationForm.FormC);

            resultado = resultado.Replace(".", "").Replace(",", "").Replace("-", " ");
            resultado = resultado.Replace(" S A S ", " ")
                                 .Replace(" SAS ", " ")
                                 .Replace(" S A ", " ")
                                 .Replace(" SA ", " ")
                                 .Replace(" S A V ", " ")
                                 .Replace(" SAV ", " ")
                                 .Replace(" LTDA ", " ")
                                 .Replace(" LIMITADA ", " ")
                                 .Replace(" LABORATORIOS ", " ")
                                 .Replace(" LABORATORIO ", " ")
                                 .Replace(" LABS ", " ")
                                 .Replace(" LAB ", " ");

            return Regex.Replace(resultado, @"\s+", " ").Trim();
        }

        private static string NormalizarProducto(string texto)
        {
            string resultado = NormalizarTextoAbsoluto(texto);
            resultado = Regex.Replace(resultado, @"\b0+\b", " ");
            resultado = Regex.Replace(resultado, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(resultado) ? "SIN_DATO" : resultado;
        }

        private sealed class HojaAgregada
        {
            public Dictionary<(string atc, string lab, string prod), List<RegistroHoja>> Registros { get; set; }
            public DataTable Preview { get; set; }
            public int FilasConDatos { get; set; }
            public HojaIndices Indices { get; set; }
        }

        private sealed class RegistroHoja
        {
            public string Pack { get; set; }
            public double Valor { get; set; }
        }

        private static string ObtenerPrefijoProducto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "SIN_DATO";

            string limpio = NormalizarTextoAbsoluto(texto);
            var palabras = limpio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return palabras.Length > 0 ? palabras[0] : "SIN_DATO";
        }

        private static string ObtenerClavePack(string pack)
        {
            if (string.IsNullOrWhiteSpace(pack) || pack == "SIN PACK")
                return "SIN_PACK";

            string limpio = NormalizarTextoAbsoluto(pack);
            var palabras = limpio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return palabras.Length > 0 ? palabras[0] : "SIN_PACK";   
        }
    }

    public sealed class ExcelLoadResult
    {
        public List<EspejoComparativo> Items { get; set; }
        public double TotalPx { get; set; }
        public double TotalUn { get; set; }
        public int FilasFormuladas { get; set; }
        public int FilasVentas { get; set; }
        public DataTable PreviewFormuladas { get; set; }
        public DataTable PreviewVentas { get; set; }
        public HojaIndices IndicesFormuladas { get; set; }
        public HojaIndices IndicesVentas { get; set; }
    }

    public sealed class HojaIndices
    {
        public int IdxPack { get; set; }
        public int IdxLab { get; set; }
        public int IdxValor { get; set; }
        public int IdxAtc { get; set; }
        public int IdxProd { get; set; }
    }
}