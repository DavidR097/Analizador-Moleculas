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

        private string _molConversion = "0%";
        public string MolConversion
        {
            get => _molConversion;
            set { _molConversion = value; OnPropertyChanged(nameof(MolConversion)); }
        }

        private int _molLaboratorios;
        public int MolLaboratorios
        {
            get => _molLaboratorios;
            set { _molLaboratorios = value; OnPropertyChanged(nameof(MolLaboratorios)); }
        }

        // ======================================================================
        // AGREGADAS: Propiedades específicas para el análisis de la Molécula
        // ======================================================================
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

        // ── Gráficos ──────────────────────────────────────────────────────────

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

        // ── Tabla detalle ─────────────────────────────────────────────────────

        private DataTable _molDetalleView;
        public DataTable MolDetalleView
        {
            get => _molDetalleView;
            set { _molDetalleView = value; OnPropertyChanged(nameof(MolDetalleView)); }
        }

        // ── Inicialización ────────────────────────────────────────────────────

        private void InicializarMolecula()
        {
            CargarMoleculasCommand = new RelayCommand(CargarMoleculas);
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

            // KPIs Base
            double totalPx = registros.Sum(x => x.TotalRecetado);
            double totalUn = registros.Sum(x => x.TotalVendido);
            MolTotalPx = totalPx;
            MolTotalUn = totalUn;
            MolConversion = totalPx > 0 ? $"{(totalUn / totalPx) * 100:N1}%" : "0%";

            var ti = CultureInfo.GetCultureInfo("es-CO").TextInfo;
            string TitleCase(string s) => ti.ToTitleCase((s ?? "").ToLower());

            // Agrupación por laboratorio
            var porLab = registros
                .GroupBy(x => x.Laboratorio ?? "No indicado")
                .Select(g => new
                {
                    Lab = TitleCase(g.Key),
                    Px = g.Sum(x => x.TotalRecetado),
                    Un = g.Sum(x => x.TotalVendido)
                })
                .OrderByDescending(x => x.Px)
                .ToList();

            MolLaboratorios = porLab.Count;

            // CORREGIDO: Guardar en la propiedad correcta de la Molécula
            MolLaboratorioLider = porLab.Any() ? porLab.First().Lab : "—";

            var top10 = porLab.Take(10).ToList();
            var top8Pie = porLab.Take(8).ToList();

            // Cálculo del pastel (Limitado al top 8)
            double totalPxPie = porLab.Sum(x => x.Px);
            double sumaTop8 = top8Pie.Sum(x => x.Px);
            double porcentajeTop = totalPxPie > 0 ? (sumaTop8 / totalPxPie) * 100 : 0;

            // CORREGIDO: Guardar en la propiedad correcta de la Molécula
            MolPorcentajeTop = $"{porcentajeTop:F1}%";

            // Gráfico de Barras (Top 10 Labs)
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

            // Gráfico de Pie (Top 8 Labs)
            MolSeriesPie = top8Pie.Select(lab =>
            {
                double share = totalPxPie > 0 ? lab.Px / totalPxPie : 0;
                return (ISeries)new PieSeries<double>
                {
                    Name = lab.Lab,
                    Values = new[] { lab.Px },
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsSize = 13,
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = _ => $"{share:P1}",
                    ToolTipLabelFormatter = _ => $"{lab.Lab}\nPX: {lab.Px:N0} ({share:P1})"
                };
            }).ToArray();

            // Tabla detalle completa (todos los laboratorios)
            var dt = new DataTable();
            dt.Columns.Add("Laboratorio", typeof(string));
            dt.Columns.Add("Recetado (PX)", typeof(string));
            dt.Columns.Add("Vendido (UN)", typeof(string));
            dt.Columns.Add("Conversión %", typeof(string));

            foreach (var r in porLab)
            {
                string conv = r.Px > 0 ? $"{(r.Un / r.Px) * 100:N1}%" : "—";
                dt.Rows.Add(
                    r.Lab,
                    r.Px.ToString("N0", _culturaColombia),
                    r.Un.ToString("N0", _culturaColombia),
                    conv);
            }
            MolDetalleView = dt;

            // Actualización del mensaje de estado del sistema
            StatusMessage = $"✓ {_moleculaSeleccionada} · {porLab.Count} labs · Líder: {MolLaboratorioLider} · Representación Top 8: {MolPorcentajeTop}";
        }
    }
}