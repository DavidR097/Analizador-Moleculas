using AnalizadorMoleculas.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace AnalizadorMoleculas.ViewModels
{
    public partial class MainViewModel
    {
        private ObservableCollection<string> _moleculasDisponibles = new();
        public ObservableCollection<string> MoleculasDisponibles
        {
            get => _moleculasDisponibles;
            set { _moleculasDisponibles = value; OnPropertyChanged(nameof(MoleculasDisponibles)); }
        }

        private string _moleculaSeleccionada;
        public string MoleculaSeleccionada
        {
            get => _moleculaSeleccionada;
            set
            {
                _moleculaSeleccionada = value;
                OnPropertyChanged(nameof(MoleculaSeleccionada));
                CalcularAnalisisMolecula();
            }
        }

        public ICommand CargarMoleculasCommand { get; private set; }

        private double _molTotalPx;
        public double MolTotalPx
        {
            get => _molTotalPx;
            set { _molTotalPx = value; OnPropertyChanged(nameof(MolTotalPx)); }
        }

        private double _molTotalUn;
        public double MolTotalUn
        {
            get => _molTotalUn;
            set { _molTotalUn = value; OnPropertyChanged(nameof(MolTotalUn)); }
        }

        private int _molLaboratorios;
        public int MolLaboratorios
        {
            get => _molLaboratorios;
            set { _molLaboratorios = value; OnPropertyChanged(nameof(MolLaboratorios)); }
        }

        private string _molLaboratorioLider = "—";
        public string MolLaboratorioLider
        {
            get => _molLaboratorioLider;
            set { _molLaboratorioLider = value; OnPropertyChanged(nameof(MolLaboratorioLider)); }
        }

        private string _molPorcentajeTop = "0.0%";
        public string MolPorcentajeTop
        {
            get => _molPorcentajeTop;
            set { _molPorcentajeTop = value; OnPropertyChanged(nameof(MolPorcentajeTop)); }
        }


        private ISeries[] _molSeriesBarras = Array.Empty<ISeries>();
        public ISeries[] MolSeriesBarras
        {
            get => _molSeriesBarras;
            set { _molSeriesBarras = value; OnPropertyChanged(nameof(MolSeriesBarras)); }
        }

        private ISeries[] _molSeriesPie = Array.Empty<ISeries>();
        public ISeries[] MolSeriesPie
        {
            get => _molSeriesPie;
            set { _molSeriesPie = value; OnPropertyChanged(nameof(MolSeriesPie)); }
        }

        private Axis[] _molXAxes = Array.Empty<Axis>();
        public Axis[] MolXAxes
        {
            get => _molXAxes;
            set { _molXAxes = value; OnPropertyChanged(nameof(MolXAxes)); }
        }

        private Axis[] _molYAxes = Array.Empty<Axis>();
        public Axis[] MolYAxes
        {
            get => _molYAxes;
            set { _molYAxes = value; OnPropertyChanged(nameof(MolYAxes)); }
        }

        private DataTable _molDetallePx;
        public DataTable MolDetallePx
        {
            get => _molDetallePx;
            set { _molDetallePx = value; OnPropertyChanged(nameof(MolDetallePx)); }
        }

        private DataTable _molDetalleUn;
        public DataTable MolDetalleUn
        {
            get => _molDetalleUn;
            set { _molDetalleUn = value; OnPropertyChanged(nameof(MolDetalleUn)); }
        }

        private void InicializarMolecula()
        {
            CargarMoleculasCommand = new RelayCommand(CargarMoleculas);
        }

        private string _molTipoMercadoLabel = "—";
        public string MolTipoMercadoLabel
        {
            get => _molTipoMercadoLabel;
            set { _molTipoMercadoLabel = value; OnPropertyChanged(nameof(MolTipoMercadoLabel)); }
        }

        private SolidColorBrush _molTipoMercadoBrush = new SolidColorBrush(Color.FromRgb(90, 90, 120));
        public SolidColorBrush MolTipoMercadoBrush
        {
            get => _molTipoMercadoBrush;
            set { _molTipoMercadoBrush = value; OnPropertyChanged(nameof(MolTipoMercadoBrush)); }
        }

        private string _molConcentracionLabel = "—";
        public string MolConcentracionLabel
        {
            get => _molConcentracionLabel;
            set { _molConcentracionLabel = value; OnPropertyChanged(nameof(MolConcentracionLabel)); }
        }

        private SolidColorBrush _molConcentracionBrush = new SolidColorBrush(Color.FromRgb(90, 90, 120));
        public SolidColorBrush MolConcentracionBrush
        {
            get => _molConcentracionBrush;
            set { _molConcentracionBrush = value; OnPropertyChanged(nameof(MolConcentracionBrush)); }
        }

        private string _molDiversidadLabel = "—";
        public string MolDiversidadLabel
        {
            get => _molDiversidadLabel;
            set { _molDiversidadLabel = value; OnPropertyChanged(nameof(MolDiversidadLabel)); }
        }

        private SolidColorBrush _molDiversidadBrush = new SolidColorBrush(Color.FromRgb(90, 90, 120));
        public SolidColorBrush MolDiversidadBrush
        {
            get => _molDiversidadBrush;
            set { _molDiversidadBrush = value; OnPropertyChanged(nameof(MolDiversidadBrush)); }
        }

        public void CargarMoleculas()
        {
            if (_listaComparacionEspejo == null || _listaComparacionEspejo.TotalCount == 0)
            {
                StatusMessage = "⚠ Primero carga un Excel para ver las moléculas.";
                return;
            }

            var atcs = _listaComparacionEspejo
                .Select(x => x.Atc)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            MoleculasDisponibles.Clear();
            foreach (var atc in atcs)
                MoleculasDisponibles.Add(atc);

            MoleculaSeleccionada = null;
            StatusMessage = $"✓ {atcs.Count} moléculas disponibles para análisis individual.";
        }

        private void CalcularAnalisisMolecula()
        {
            if (string.IsNullOrEmpty(_moleculaSeleccionada)) return;

            var registros = _listaComparacionEspejo
                .Where(x => x.Atc == _moleculaSeleccionada)
                .ToList();

            if (registros.Count == 0) return;

            double totalPx = registros.Sum(x => x.TotalRecetado);
            double totalUn = registros.Sum(x => x.TotalVendido);
            MolTotalPx = totalPx;
            MolTotalUn = totalUn;

            var ti = CultureInfo.GetCultureInfo("es-CO").TextInfo;
            string TitleCase(string s) => ti.ToTitleCase((s ?? "").ToLower());

            var porLab = registros
                .GroupBy(x => x.Laboratorio ?? "No indicado")
                .Select(g => new
                {
                    Lab = TitleCase(g.Key),
                    Px = g.Sum(x => x.TotalRecetado),
                    Un = g.Sum(x => x.TotalVendido)
                })
                .OrderByDescending(x => x.Un)
                .ToList();

            MolLaboratorios = porLab.Count;

            MolLaboratorioLider = porLab.Any() ? porLab.First().Lab : "—";

            var top10 = porLab.Take(10).ToList();
            var top8Pie = porLab.Take(8).ToList();

            double totalUnPie = porLab.Sum(x => x.Un);
            double sumaTop8 = top8Pie.Sum(x => x.Un);
            double porcentajeTop = totalUnPie > 0 ? (sumaTop8 / totalUnPie) * 100 : 0;

            double totalCombinado = totalPx + totalUn;
            if (totalCombinado > 0)
            {
                double pctPx = (totalPx / totalCombinado) * 100;
                if (pctPx >= 70)
                {
                    MolTipoMercadoLabel = "Predominantemente Rx";
                    MolTipoMercadoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B5179E"));
                }
                else if ((100 - pctPx) >= 70)
                {
                    MolTipoMercadoLabel = "Altamente Vendido";
                    MolTipoMercadoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CC9F0"));
                }
                else
                {
                    MolTipoMercadoLabel = "Mercado Mixto";
                    MolTipoMercadoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7209B7"));
                }
            }
            else
            {
                MolTipoMercadoLabel = "Sin datos";
                MolTipoMercadoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A5A78"));
            }
            
            double hhi = 0;
            if (totalUnPie > 0)
            {
                foreach (var lab in porLab)
                {
                    double sharePct = (lab.Un / totalUnPie) * 100;
                    hhi += sharePct * sharePct;
                }
            }

            double cuotaLider = totalUnPie > 0 && porLab.Any()
                ? (porLab.First().Un / totalUnPie) * 100
                : 0;

            if (hhi >= 4000 || cuotaLider >= 70)
            {
                MolConcentracionLabel = "Mercado Monopólico";
                MolConcentracionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B"));
            }
            else if (hhi >= 1800)
            {
                MolConcentracionLabel = "Mercado Altamente Concentrado";
                MolConcentracionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8C5A"));
            }
            else if (hhi >= 1000)
            {
                MolConcentracionLabel = "Moderadamente Concentrado";
                MolConcentracionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB347"));
            }
            else
            {
                MolConcentracionLabel = "Mercado Competitivo";
                MolConcentracionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3DDC84"));
            }

            int numLabs = porLab.Count;
            if (numLabs <= 2)
            {
                MolDiversidadLabel = "Muy baja · ≤2 labs";
                MolDiversidadBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B"));
            }
            else if (numLabs <= 5)
            {
                MolDiversidadLabel = "Baja · 3–5 labs";
                MolDiversidadBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB347"));
            }
            else if (numLabs <= 12)
            {
                MolDiversidadLabel = "Media · 6–12 labs";
                MolDiversidadBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CC9F0"));
            }
            else
            {
                MolDiversidadLabel = "Alta · +12 labs";
                MolDiversidadBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3DDC84"));
            }

            MolPorcentajeTop = $"{porcentajeTop:F1}%";

            MolSeriesBarras = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name   = "Recetado (PX)",
                    Values = top10.Select(x => Math.Round(x.Px, 0)).ToArray(),
                    Fill   = new SolidColorPaint(new SKColor(181, 23, 158, 200)),
                    DataLabelsPaint     = new SolidColorPaint(new SKColor(232, 232, 240)),
                    DataLabelsSize      = 10,
                    DataLabelsFormatter = p => ((double)p.Coordinate.PrimaryValue).ToString("N0", _culturaColombia)
                },
                new ColumnSeries<double>
                {
                    Name   = "Vendido (UN)",
                    Values = top10.Select(x => Math.Round(x.Un, 0)).ToArray(),
                    Fill   = new SolidColorPaint(new SKColor(76, 201, 240, 200)),
                    DataLabelsPaint     = new SolidColorPaint(new SKColor(232, 232, 240)),
                    DataLabelsSize      = 10,
                    DataLabelsFormatter = p => ((double)p.Coordinate.PrimaryValue).ToString("N0", _culturaColombia)
                }
            };

            MolXAxes = new Axis[]
            {
                new Axis
                {
                    Labels         = top10.Select(x => x.Lab).ToList(),
                    LabelsRotation = -35,
                    TextSize       = 10,
                    LabelsPaint    = new SolidColorPaint(new SKColor(90, 90, 120)),
                }
            };

            MolYAxes = new Axis[]
            {
                new Axis
                {
                    TextSize    = 11,
                    LabelsPaint = new SolidColorPaint(new SKColor(140, 140, 160)),
                    Labeler     = val => val.ToString("#,##0", _culturaColombia)
                }
            };

            MolSeriesPie = top8Pie.Select(lab =>
            {
                double share = totalUnPie > 0 ? lab.Un / totalUnPie : 0;
                return (ISeries)new PieSeries<double>
                {
                    Name = lab.Lab,
                    Values = new[] { lab.Un },
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsSize = 13,
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = _ => $"{share:P1}",
                    ToolTipLabelFormatter = _ => $"{lab.Un:N0} ({share:P1})"
                };
            }).ToArray();

            var dtPx = new DataTable();
            dtPx.Columns.Add("Laboratorio", typeof(string));
            dtPx.Columns.Add("⭐ Pack Top", typeof(string));
            dtPx.Columns.Add("Recetado (PX)", typeof(string));

            var rankingPx = registros
                .GroupBy(x => new
                {
                    Lab = x.Laboratorio ?? "No indicado",
                    Pack = !string.IsNullOrWhiteSpace(x.PackPx) ? x.PackPx : "—"
                })
                .Select(g => new
                {
                    Lab = TitleCase(g.Key.Lab),
                    Pack = g.Key.Pack,
                    Px = g.Sum(x => x.TotalRecetado) // Asegúrate de usar el campo de suma exacto de tu código original
                })
                .OrderByDescending(x => x.Px)
                .ToList();

            // Llenamos el DataTable de la UI con el nuevo ranking detallado
            dtPx.Rows.Clear(); // O como inicialices tu dtPx
            foreach (var r in rankingPx)
                dtPx.Rows.Add(r.Lab, r.Pack, r.Px.ToString("N0", _culturaColombia));

            MolDetallePx = dtPx;

            var porLabPx = registros
                .GroupBy(x => x.Laboratorio ?? "No indicado")
                .Select(g => new
                {
                    Lab = TitleCase(g.Key),
                    Px = g.Sum(x => x.TotalRecetado)
                })
                .OrderByDescending(x => x.Px)
                .ToList();

            var dtUn = new DataTable();
            dtUn.Columns.Add("Laboratorio", typeof(string));
            dtUn.Columns.Add("⭐ Pack Top", typeof(string));
            dtUn.Columns.Add("Vendido (UN)", typeof(string));

            var rankingUn = registros
                .GroupBy(x => new
                {
                    Lab = x.Laboratorio ?? "No indicado",
                    Pack = !string.IsNullOrWhiteSpace(x.PackUn) ? x.PackUn : "—"
                })
                .Select(g => new
                {
                    Lab = TitleCase(g.Key.Lab),
                    Pack = g.Key.Pack,
                    Un = g.Sum(x => x.TotalVendido)
                })
                .OrderByDescending(x => x.Un)
                .ToList();

            // Llenamos el DataTable de la UI con el nuevo ranking detallado
            dtUn.Rows.Clear();
            foreach (var r in rankingUn)
                dtUn.Rows.Add(r.Lab, r.Pack, r.Un.ToString("N0", _culturaColombia));

            MolDetalleUn = dtUn;

            StatusMessage = $"✓ {_moleculaSeleccionada} · {porLab.Count} labs · Líder: {MolLaboratorioLider} · Representación Top 8: {MolPorcentajeTop}";
        }
    }
}