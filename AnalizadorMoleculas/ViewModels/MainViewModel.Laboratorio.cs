using AnalizadorMoleculas.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace AnalizadorMoleculas.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<LaboratorioInfo> LaboratoriosDisponibles { get; set; } = new();

        private LaboratorioInfo _laboratorioSeleccionado;
        public LaboratorioInfo LaboratorioSeleccionado
        {
            get => _laboratorioSeleccionado;
            set
            {
                _laboratorioSeleccionado = value;
                OnPropertyChanged(nameof(LaboratorioSeleccionado));
                if (value != null) CargarAnalisisLaboratorio(value);
            }
        }

        private static string GetLabKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var normalized = name.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            var noDiacritics = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
            noDiacritics = System.Text.RegularExpressions.Regex.Replace(noDiacritics, "\\s+", " ").Trim();
            noDiacritics = System.Text.RegularExpressions.Regex.Replace(noDiacritics, "[^\\w\\s]", "");
            return noDiacritics.ToUpperInvariant();
        }

        private static string GetKey(string value) => GetLabKey(value);

        private static string GetCompositeKey(Models.EspejoComparativo e)
        {
            if (e == null) return string.Empty;
            var atc = GetKey(e.Atc ?? string.Empty);
            var lab = GetKey(e.Laboratorio ?? string.Empty);
            var prod = GetKey(e.Producto ?? string.Empty);
            return string.Join("|", new[] { atc, lab, prod });
        }

        private int _productosCount;
        public int ProductosCount { get => _productosCount; set { _productosCount = value; OnPropertyChanged(nameof(ProductosCount)); } }

        private double _totalPX;
        public double TotalPX { get => _totalPX; set { _totalPX = value; OnPropertyChanged(nameof(TotalPX)); } }

        private double _totalUN;
        public double TotalUN { get => _totalUN; set { _totalUN = value; OnPropertyChanged(nameof(TotalUN)); } }

        private string _moleculaEstrella = "Sin datos";
        public string MoleculaEstrella { get => _moleculaEstrella; set { _moleculaEstrella = value; OnPropertyChanged(nameof(MoleculaEstrella)); } }

        private double _participacionMercadoLab;
        public double ParticipacionMercadoLab
        {
            get => _participacionMercadoLab;
            set { _participacionMercadoLab = value; OnPropertyChanged(nameof(ParticipacionMercadoLab)); }
        }

        private string _conversionLab = "—";
        public string ConversionLab { get => _conversionLab; set { _conversionLab = value; OnPropertyChanged(nameof(ConversionLab)); } }

        public ObservableCollection<ISeries> SeriesLab { get; set; } = new();
        public ObservableCollection<Axis> XAxesLab { get; set; } = new();
        public ObservableCollection<Axis> YAxesLab { get; set; } = new();

        public ObservableCollection<ProductoLabInfo> RankingProductosLab { get; set; } = new();

        public ObservableCollection<CompetidorLabInfo> CompetidoresLab { get; set; } = new();
        public ISeries[] SeriesCompetidoresLab { get; set; } = Array.Empty<ISeries>();

        private void InicializarLaboratorio()
        {
            CargarListaLaboratorios();

            SeriesLab = new ObservableCollection<ISeries>();
            XAxesLab = new ObservableCollection<Axis> { new Axis() };
            YAxesLab = new ObservableCollection<Axis> { new Axis() };
        }

        private void CargarListaLaboratorios()
        {
            var labSeleccionadoPrevioKey = _laboratorioSeleccionado?.Key;

            LaboratoriosDisponibles.Clear();

            if (ListaComparacionEspejo == null || ListaComparacionEspejo.TotalCount == 0)
            {
                LimpiarAnalisisLaboratorio();
                return;
            }

            var grupos = ListaComparacionEspejo
                .GroupBy(x => GetLabKey(x.Laboratorio))
                .Select(g =>
                {
                    var origName = g.Select(x => x.Laboratorio).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key;
                    double px = g.Sum(x => x.TotalRecetado);
                    double un = g.Sum(x => x.TotalVendido);
                    double partPx = TotalPxCalculado > 0 ? Math.Round(px / TotalPxCalculado * 100, 2) : 0;
                    double partUn = TotalUnCalculado > 0 ? Math.Round(un / TotalUnCalculado * 100, 2) : 0;
                    return new LaboratorioInfo
                    {
                        Key = g.Key,
                        Nombre = origName,
                        Productos = g.Select(x => x.Producto).Distinct().Count(),
                        Participacion = partPx,
                        ParticipacionUn = partUn,
                        NombreDisplay = $"{origName}  (PX {partPx:N1}% · UN {partUn:N1}%)"
                    };
                })
                .OrderByDescending(x => x.Participacion + x.ParticipacionUn)
                .ToList();

            foreach (var lab in grupos)
                LaboratoriosDisponibles.Add(lab);

            if (!LaboratoriosDisponibles.Any())
                return;

            var mantener = !string.IsNullOrEmpty(labSeleccionadoPrevioKey)
                ? LaboratoriosDisponibles.FirstOrDefault(l => (l.Key ?? "").Equals(labSeleccionadoPrevioKey, StringComparison.OrdinalIgnoreCase))
                : null;

            LaboratorioSeleccionado = mantener ?? LaboratoriosDisponibles.First();
        }

        private void LimpiarAnalisisLaboratorio()
        {
            ProductosCount = 0;
            TotalPX = 0;
            TotalUN = 0;
            ParticipacionMercadoLab = 0;
            ConversionLab = "—";
            SeriesLab.Clear();
            RankingProductosLab.Clear();
            CompetidoresLab.Clear();
            SeriesCompetidoresLab = Array.Empty<ISeries>();
            OnPropertyChanged(nameof(SeriesCompetidoresLab));
        }

        private void CargarAnalisisLaboratorio(LaboratorioInfo lab)
        {
            if (lab == null) return;

            var labKey = GetLabKey(lab.Nombre);
            var itemsDelLab = ListaComparacionEspejo?
                .Where(x => GetLabKey(x.Laboratorio) == labKey)
                .ToList() ?? new System.Collections.Generic.List<EspejoComparativo>();

            if (!itemsDelLab.Any())
            {
                LimpiarAnalisisLaboratorio();
                return;
            }

            ProductosCount = itemsDelLab.Select(x => x.Producto).Distinct().Count();
            TotalPX = itemsDelLab.Sum(x => x.TotalRecetado);
            TotalUN = itemsDelLab.Sum(x => x.TotalVendido);

            ParticipacionMercadoLab = TotalPxCalculado > 0
                ? Math.Round(TotalPX / TotalPxCalculado * 100, 2)
                : 0;

            ConversionLab = TotalPX > 0
                ? $"{(TotalUN / TotalPX) * 100:N1}%"
                : (TotalUN > 0 ? "Solo venta (sin PX)" : "—");

            bool labTienePx = TotalPX > 0;
            string moleculaEstrellaKey = (labTienePx
                    ? itemsDelLab.GroupBy(x => x.Atc).OrderByDescending(g => g.Sum(x => x.TotalRecetado))
                    : itemsDelLab.GroupBy(x => x.Atc).OrderByDescending(g => g.Sum(x => x.TotalVendido)))
                .FirstOrDefault()?.Key ?? "Sin datos";
            MoleculaEstrella = moleculaEstrellaKey;

            var rankingCompleto = itemsDelLab
                .GroupBy(x => GetCompositeKey(x))
                .Select(g => new
                {
                    Key = g.Key,
                    Producto = g.Select(x => x.Producto).FirstOrDefault(),
                    Atc = g.Select(x => x.Atc).FirstOrDefault(),
                    PX = g.Sum(x => x.TotalRecetado),
                    UN = g.Sum(x => x.TotalVendido)
                })
                .OrderByDescending(x => x.PX)
                .ThenByDescending(x => x.UN)
                .ToList();

            RankingProductosLab.Clear();
            foreach (var p in rankingCompleto)
            {
                RankingProductosLab.Add(new ProductoLabInfo
                {
                    Key = p.Key,
                    Producto = p.Producto,
                    Atc = p.Atc,
                    PX = p.PX,
                    UN = p.UN,
                    ParticipacionPX = TotalPX > 0 ? Math.Round(p.PX / TotalPX * 100, 1) : 0,
                    ParticipacionUN = TotalUN > 0 ? Math.Round(p.UN / TotalUN * 100, 1) : 0,
                    ConversionDisplay = p.PX > 0
                        ? $"{(p.UN / p.PX) * 100:N1}%"
                        : (p.UN > 0 ? "Solo venta" : "—")
                });
            }

            var top8 = rankingCompleto.Take(8).ToList();

            SeriesLab.Clear();
            SeriesLab.Add(new ColumnSeries<double>
            {
                Name = "PX (Recetado)",
                Values = top8.Select(x => x.PX).ToArray(),
                Fill = new SolidColorPaint(new SKColor(181, 23, 158, 220)), 
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 10,
                DataLabelsFormatter = p => ((double)p.Coordinate.PrimaryValue).ToString("N0")
            });

            SeriesLab.Add(new LineSeries<double>
            {
                Name = "UN (Vendido)",
                Values = top8.Select(x => x.UN).ToArray(),
                Stroke = new SolidColorPaint(new SKColor(76, 201, 240), 3), 
                GeometryFill = new SolidColorPaint(new SKColor(76, 201, 240)),
                GeometryStroke = new SolidColorPaint(new SKColor(76, 201, 240)),
                Fill = null
            });

            XAxesLab.Clear();
            XAxesLab.Add(new Axis
            {
                Labels = top8.Select(x => x.Producto).ToList(),
                LabelsRotation = -25,
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200))
            });

            YAxesLab.Clear();
            YAxesLab.Add(new Axis
            {
                Labeler = val => val.ToString("N0"),
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200))
            });

            CargarCompetidoresLab(lab.Nombre, moleculaEstrellaKey, usarUnComoBase: !labTienePx);
        }

        private void CargarCompetidoresLab(string nombreLab, string atc, bool usarUnComoBase)
        {
            CompetidoresLab.Clear();

            if (string.IsNullOrWhiteSpace(atc) || atc == "Sin datos" || ListaComparacionEspejo == null)
            {
                SeriesCompetidoresLab = Array.Empty<ISeries>();
                OnPropertyChanged(nameof(SeriesCompetidoresLab));
                return;
            }

            var itemsAtc = ListaComparacionEspejo
                .Where(x => (x.Atc ?? "") == atc)
                .ToList();

            double totalPxAtc = itemsAtc.Sum(x => x.TotalRecetado);
            double totalUnAtc = itemsAtc.Sum(x => x.TotalVendido);

            bool baseEsUn = usarUnComoBase || totalPxAtc <= 0;
            double totalBase = baseEsUn ? totalUnAtc : totalPxAtc;

            var porLab = itemsAtc
                .GroupBy(x => GetLabKey(x.Laboratorio))
                .Select(g => new
                {
                    Key = g.Key,
                    Nombre = g.Select(x => x.Laboratorio).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key,
                    PX = g.Sum(x => x.TotalRecetado),
                    UN = g.Sum(x => x.TotalVendido)
                })
                .OrderByDescending(x => baseEsUn ? x.UN : x.PX)
                .ToList();

            var top8 = porLab.Take(8).ToList();

            foreach (var c in top8)
            {
                double valorBase = baseEsUn ? c.UN : c.PX;
                CompetidoresLab.Add(new CompetidorLabInfo
                {
                    Nombre = c.Nombre,
                    Key = c.Key,
                    PX = c.PX,
                    UN = c.UN,
                    Participacion = totalBase > 0 ? Math.Round(valorBase / totalBase * 100, 1) : 0,
                    EsSeleccionado = (c.Key ?? "").Equals(GetLabKey(nombreLab), StringComparison.OrdinalIgnoreCase)
                });
            }

            var paleta = new[]
            {
                new SKColor(76, 201, 240, 210),   
                new SKColor(255, 183, 77, 210),   
                new SKColor(129, 199, 132, 210),  
                new SKColor(186, 104, 200, 210),  
                new SKColor(229, 115, 115, 210),  
                new SKColor(100, 181, 246, 210),  
                new SKColor(255, 138, 101, 210),  
            };

            SeriesCompetidoresLab = top8.Select((c, i) =>
            {
                double valorBase = baseEsUn ? c.UN : c.PX;
                double share = totalBase > 0 ? valorBase / totalBase : 0;
                bool esSeleccionado = (c.Nombre ?? "").Equals(nombreLab, StringComparison.OrdinalIgnoreCase);

                return (ISeries)new PieSeries<double>
                {
                    Name = c.Nombre,
                    Values = new[] { valorBase },
                    Fill = esSeleccionado
                        ? new SolidColorPaint(new SKColor(181, 23, 158, 235)) 
                        : new SolidColorPaint(paleta[i % paleta.Length]),
                    Stroke = esSeleccionado
                        ? new SolidColorPaint(SKColors.White, 2)
                        : null,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = _ => $"{share:P1}",
                    ToolTipLabelFormatter = _ => $"{c.Nombre}: {valorBase:N0} {(baseEsUn ? "UN" : "PX")} ({share:P1})"
                };
            }).ToArray();

            OnPropertyChanged(nameof(SeriesCompetidoresLab));
        }

        public class LaboratorioInfo
        {
            public string Key { get; set; }
            public string Nombre { get; set; }
            public int Productos { get; set; }
            public double Participacion { get; set; }
            public double ParticipacionUn { get; set; }
            public string NombreDisplay { get; set; }
        }

        public class ProductoLabInfo
        {
            public string Key { get; set; }
            public string Producto { get; set; }
            public string Atc { get; set; }
            public double PX { get; set; }
            public double UN { get; set; }
            public double ParticipacionPX { get; set; }
            public double ParticipacionUN { get; set; }
            public string ConversionDisplay { get; set; }
        }

        public class CompetidorLabInfo
        {
            public string Key { get; set; }
            public string Nombre { get; set; }
            public double PX { get; set; }
            public double UN { get; set; }
            public double Participacion { get; set; }
            public bool EsSeleccionado { get; set; }
        }
    }
}