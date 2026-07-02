using AnalizadorMoleculas.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Defaults;
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
        private const int MaxRowsMolDetalle = 200;

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

        private ISeries[] _molSeriesMatriz = Array.Empty<ISeries>();
        public ISeries[] MolSeriesMatriz
        {
            get => _molSeriesMatriz;
            set { _molSeriesMatriz = value; OnPropertyChanged(nameof(MolSeriesMatriz)); }
        }

        private ISeries[] _molSeriesParticipacion = Array.Empty<ISeries>();
        public ISeries[] MolSeriesParticipacion
        {
            get => _molSeriesParticipacion;
            set { _molSeriesParticipacion = value; OnPropertyChanged(nameof(MolSeriesParticipacion)); }
        }

        private ISeries[] _molSeriesTendencia = Array.Empty<ISeries>();
        public ISeries[] MolSeriesTendencia
        {
            get => _molSeriesTendencia;
            set { _molSeriesTendencia = value; OnPropertyChanged(nameof(MolSeriesTendencia)); }
        }

        private Axis[] _molXAxesEvolucion = Array.Empty<Axis>();
        public Axis[] MolXAxesEvolucion
        {
            get => _molXAxesEvolucion;
            set { _molXAxesEvolucion = value; OnPropertyChanged(nameof(MolXAxesEvolucion)); }
        }

        private Axis[] _molYAxesPorcentaje = Array.Empty<Axis>();
        public Axis[] MolYAxesPorcentaje
        {
            get => _molYAxesPorcentaje;
            set { _molYAxesPorcentaje = value; OnPropertyChanged(nameof(MolYAxesPorcentaje)); }
        }

        private Axis[] _molXAxesMatriz = Array.Empty<Axis>();
        public Axis[] MolXAxesMatriz
        {
            get => _molXAxesMatriz;
            set { _molXAxesMatriz = value; OnPropertyChanged(nameof(MolXAxesMatriz)); }
        }

        private Axis[] _molYAxesMatriz = Array.Empty<Axis>();
        public Axis[] MolYAxesMatriz
        {
            get => _molYAxesMatriz;
            set { _molYAxesMatriz = value; OnPropertyChanged(nameof(MolYAxesMatriz)); }
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

        // Ranking pagination (mostrar máximo 20 registros por página)
        private List<(string Lab, string Pack, double Value)> _rankingPxFull = new();
        private List<(string Lab, string Pack, double Value)> _rankingUnFull = new();
        private const int RankingPageSize = 20;
        private int _rankingPagePx = 1;
        private int _rankingPageUn = 1;
        private int _rankingTotalPagesPx = 1;
        private int _rankingTotalPagesUn = 1;
        public int RankingTotalPagesPx { get => _rankingTotalPagesPx; set { _rankingTotalPagesPx = value; OnPropertyChanged(nameof(RankingTotalPagesPx)); } }
        public int RankingTotalPagesUn { get => _rankingTotalPagesUn; set { _rankingTotalPagesUn = value; OnPropertyChanged(nameof(RankingTotalPagesUn)); } }
        public int RankingPagePx
        {
            get => _rankingPagePx;
            set { _rankingPagePx = Math.Max(1, value); OnPropertyChanged(nameof(RankingPagePx)); UpdateRankingPages(); }
        }
        public int RankingPageUn
        {
            get => _rankingPageUn;
            set { _rankingPageUn = Math.Max(1, value); OnPropertyChanged(nameof(RankingPageUn)); UpdateRankingPages(); }
        }

        public ICommand PrevRankingPxCommand => _prevRankingPxCommand ??= new RelayCommand(() => { if (RankingPagePx > 1) RankingPagePx--; });
        private ICommand _prevRankingPxCommand;
        public ICommand NextRankingPxCommand => _nextRankingPxCommand ??= new RelayCommand(() => { RankingPagePx++; });
        private ICommand _nextRankingPxCommand;

        public ICommand PrevRankingUnCommand => _prevRankingUnCommand ??= new RelayCommand(() => { if (RankingPageUn > 1) RankingPageUn--; });
        private ICommand _prevRankingUnCommand;
        public ICommand NextRankingUnCommand => _nextRankingUnCommand ??= new RelayCommand(() => { RankingPageUn++; });
        private ICommand _nextRankingUnCommand;

        private void UpdateRankingPages()
        {
            try
            {
                var dtPx = new DataTable();
                dtPx.Columns.Add("Laboratorio", typeof(string));
                dtPx.Columns.Add("⭐ Pack Top", typeof(string));
                dtPx.Columns.Add("Recetado (PX)", typeof(string));
                RankingTotalPagesPx = Math.Max(1, (int)Math.Ceiling(_rankingPxFull.Count / (double)RankingPageSize));
                if (RankingPagePx > RankingTotalPagesPx) RankingPagePx = RankingTotalPagesPx;
                var skipPx = (RankingPagePx - 1) * RankingPageSize;
                foreach (var r in _rankingPxFull.Skip(skipPx).Take(RankingPageSize))
                    dtPx.Rows.Add(r.Lab, r.Pack, r.Value.ToString("N0", _culturaColombia));
                MolDetallePx = dtPx;

                var dtUn = new DataTable();
                dtUn.Columns.Add("Laboratorio", typeof(string));
                dtUn.Columns.Add("⭐ Pack Top", typeof(string));
                dtUn.Columns.Add("Vendido (UN)", typeof(string));

                RankingTotalPagesUn = Math.Max(1, (int)Math.Ceiling(_rankingUnFull.Count / (double)RankingPageSize));
                if (RankingPageUn > RankingTotalPagesUn) RankingPageUn = RankingTotalPagesUn;
                var skipUn = (RankingPageUn - 1) * RankingPageSize;
                foreach (var r in _rankingUnFull.Skip(skipUn).Take(RankingPageSize))
                    dtUn.Rows.Add(r.Lab, r.Pack, r.Value.ToString("N0", _culturaColombia));
                MolDetalleUn = dtUn;
            }
            catch { }
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

            if (top8Pie.Any())
            {
                MolSeriesMatriz = top8Pie.Select(lab =>
                    (ISeries)new ScatterSeries<ObservablePoint>
                    {
                        Name = lab.Lab,
                        Values = new[] { new ObservablePoint(Math.Round(lab.Px, 0), Math.Round(lab.Un, 0)) },
                        Fill = new SolidColorPaint(new SKColor(183, 37, 158, 220)),
                        GeometrySize = 10
                    }
                ).ToArray();

                MolXAxesMatriz = new Axis[]
                {
                    new Axis
                    {
                        Name = "Recetado (PX)",
                        TextSize = 11,
                        Labeler = val => val.ToString("#,##0", _culturaColombia)
                    }
                };

                MolYAxesMatriz = new Axis[]
                {
                    new Axis
                    {
                        Name = "Vendido (UN)",
                        TextSize = 11,
                        Labeler = val => val.ToString("#,##0", _culturaColombia)
                    }
                };
            }
            else
            {
                MolSeriesMatriz = Array.Empty<ISeries>();
                MolXAxesMatriz = Array.Empty<Axis>();
                MolYAxesMatriz = Array.Empty<Axis>();
            }

            if (top8Pie.Any())
            {
                var denomList = top8Pie.Select(t => t.Px + t.Un).ToList();
                var instPct = top8Pie.Select((t, i) => denomList[i] > 0 ? (t.Px / denomList[i]) * 100.0 : 0.0).ToArray();
                var comPct = top8Pie.Select((t, i) => denomList[i] > 0 ? (t.Un / denomList[i]) * 100.0 : 0.0).ToArray();

                MolSeriesParticipacion = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Name = "Institucional",
                        Values = instPct,
                        Fill = new SolidColorPaint(new SKColor(181,23,158,220)),
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsSize = 10,
                        DataLabelsFormatter = p => ((double)p.Coordinate.PrimaryValue).ToString("F1") + "%"
                    },
                    new ColumnSeries<double>
                    {
                        Name = "Comercial",
                        Values = comPct,
                        Fill = new SolidColorPaint(new SKColor(76,201,240,220)),
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsSize = 10,
                        DataLabelsFormatter = p => ((double)p.Coordinate.PrimaryValue).ToString("F1") + "%"
                    }
                };

                MolXAxesEvolucion = new Axis[]
                {
                    new Axis
                    {
                        Labels = top8Pie.Select(t => t.Lab).ToList(),
                        LabelsRotation = -35,
                        TextSize = 10
                    }
                };

                MolYAxesPorcentaje = new Axis[]
                {
                    new Axis
                    {
                        TextSize = 11,
                        Labeler = val => $"{val:0}%"
                    }
                };

                var ventasTop = top8Pie.Select(t => Math.Round(t.Un, 0)).ToArray();
                MolSeriesTendencia = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Name = "Ventas (UN)",
                        Values = ventasTop,
                        Stroke = new SolidColorPaint(new SKColor(76,201,240,255)) { StrokeThickness = 2 },
                        Fill = new SolidColorPaint(new SKColor(76,201,240,80)),
                        GeometrySize = 6,
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsSize = 10
                    }
                };

                MolXAxesEvolucion = MolXAxesEvolucion;
            }
            else
            {
                MolSeriesParticipacion = Array.Empty<ISeries>();
                MolSeriesTendencia = Array.Empty<ISeries>();
                MolXAxesEvolucion = Array.Empty<Axis>();
                MolYAxesPorcentaje = Array.Empty<Axis>();
            }

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
                    Px = g.Sum(x => x.TotalRecetado) 
                })
                .OrderByDescending(x => x.Px)
                .ToList();

            dtPx.Rows.Clear(); 
            _rankingPxFull = rankingPx.Select(r => (r.Lab, r.Pack, r.Px)).ToList();
            RankingPagePx = 1; 

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

            _rankingUnFull = rankingUn.Select(r => (r.Lab, r.Pack, r.Un)).ToList();
            RankingPageUn = 1; 
            StatusMessage = $"✓ {_moleculaSeleccionada} · {porLab.Count} labs · Líder: {MolLaboratorioLider} · Representación Top 8: {MolPorcentajeTop}";
        }
    }
}