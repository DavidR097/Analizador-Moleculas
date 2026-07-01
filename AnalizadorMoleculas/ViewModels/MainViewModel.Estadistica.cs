using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AnalizadorMoleculas.ViewModels
{
    public partial class MainViewModel
    {
        private DataTable _topAtcView;
        public DataTable _estadisticasView;
        public DataTable _TopProductosView;

        public DataTable TopAtcView
        {
            get => _topAtcView;
            set { _topAtcView = value; OnPropertyChanged(nameof(TopAtcView)); }
        }

        public DataTable EstadisticasView
        {
            get => _estadisticasView;
            set { _estadisticasView = value; OnPropertyChanged(nameof(EstadisticasView)); }
        }

        public DataTable TopProductosView
        {
            get => _TopProductosView;
            set { _TopProductosView = value; OnPropertyChanged(nameof(TopProductosView)); }
        }

        private double _totalValorFinal;
        public double TotalValorFinal
        {
            get => _totalValorFinal;
            set { _totalValorFinal = value; OnPropertyChanged(nameof(TotalValorFinal)); }
        }

        private int _totalProductos;
        public int TotalProductos
        {
            get => _totalProductos;
            set { _totalProductos = value; OnPropertyChanged(nameof(TotalProductos)); }
        }

        private string _laboratorioLider;
        public string LaboratorioLider
        {
            get => _laboratorioLider;
            set { _laboratorioLider = value; OnPropertyChanged(nameof(LaboratorioLider)); }
        }

        private string _porcentajeTopLaboratorios;
        public string PorcentajeTopLaboratorios
        {
            get => _porcentajeTopLaboratorios;
            set { _porcentajeTopLaboratorios = value; OnPropertyChanged(nameof(PorcentajeTopLaboratorios)); }
        }

        private string _topAtcCodigo;
        public string TopAtcCodigo
        {
            get => _topAtcCodigo;
            set { _topAtcCodigo = value; OnPropertyChanged(nameof(TopAtcCodigo)); }
        }

        private List<string> _topProductosLabels;
        public List<string> TopProductosLabels
        {
            get => _topProductosLabels;
            set { _topProductosLabels = value; OnPropertyChanged(nameof(TopProductosLabels)); }
        }

        public ISeries[] TopProductosSeries { get; set; }
        public ISeries[] ParticipacionLaboratorioSeries { get; set; }

        private void CalcularEstadisticas()
        {
            if (_listaComparacionEspejo == null || _listaComparacionEspejo.TotalCount == 0)
            {
                MessageBox.Show("Primero carga y cruza los archivos.",
                                "Sin datos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool esVentas = HojaAnalisisSeleccionada == HojaSeleccionadaVentas;

            var ti = CultureInfo.GetCultureInfo("es-CO").TextInfo;
            string TitleCase(string s) => ti.ToTitleCase((s ?? "").ToLower());

            var datos = _listaComparacionEspejo
                .Select(i => new
                {
                    Atc = string.IsNullOrWhiteSpace(i.Atc) ? "SIN ATC" : i.Atc,
                    Lab = string.IsNullOrWhiteSpace(i.Laboratorio) ? "No indicado" : i.Laboratorio,
                    Pack = esVentas ? i.PackUn : i.PackPx,
                    Valor = esVentas ? i.TotalVendido : i.TotalRecetado
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Pack) && x.Valor != 0)
                .ToList();

            if (datos.Count == 0)
            {
                MessageBox.Show("No hay valores para el lado seleccionado.");
                return;
            }

            var valores = datos.Select(x => x.Valor).ToList();
            var dtEst = new DataTable();
            dtEst.Columns.Add("Columna", typeof(string));
            dtEst.Columns.Add("Suma", typeof(string));
            dtEst.Columns.Add("Promedio", typeof(string));
            dtEst.Columns.Add("Máximo", typeof(string));
            dtEst.Columns.Add("Mínimo", typeof(string));
            dtEst.Columns.Add("Conteo", typeof(string));
            dtEst.Rows.Add(
                esVentas ? "Vendido (UN)" : "Recetado (PX)",
                valores.Sum().ToString("N2", _culturaColombia),
                valores.Average().ToString("N2", _culturaColombia),
                valores.Max().ToString("N2", _culturaColombia),
                valores.Min().ToString("N2", _culturaColombia),
                valores.Count.ToString("N0"));
            EstadisticasView = dtEst;

            var porPack = datos
                .GroupBy(x => x.Pack)
                .Select(g => new
                {
                    Pack = g.Key,
                    Suma = g.Sum(x => x.Valor),
                    LabPrincipal = g.GroupBy(x => x.Lab)
                                    .OrderByDescending(gl => gl.Sum(x => x.Valor))
                                    .First().Key
                })
                .OrderByDescending(x => x.Suma)
                .Take(10)
                .ToList();

            var dtProd = new DataTable();
            dtProd.Columns.Add("Producto (Pack)", typeof(string));
            dtProd.Columns.Add("Laboratorio principal", typeof(string));
            dtProd.Columns.Add("Valor final total", typeof(string));
            foreach (var item in porPack)
                dtProd.Rows.Add(item.Pack, TitleCase(item.LabPrincipal),
                                item.Suma.ToString("N2", _culturaColombia));
            TopProductosView = dtProd;

            TopProductosLabels = porPack.Select(x => x.Pack).ToList();
            OnPropertyChanged(nameof(TopProductosLabels));
            TopProductosSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name = "Valor final",
                    Values = porPack.Select(x => Math.Round(x.Suma, 2)).ToArray(),
                    DataLabelsSize = 11,
                    DataLabelsPaint = new SolidColorPaint(new SKColor(232, 232, 240)),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = point => ((double)point.Coordinate.PrimaryValue).ToString("N2", _culturaColombia)
                }
            };
            OnPropertyChanged(nameof(TopProductosSeries));

            var porAtc = datos
                .GroupBy(x => x.Atc)
                .Select(g => new { Atc = g.Key, Suma = g.Sum(x => x.Valor) })
                .OrderByDescending(x => x.Suma)
                .ToList();
            var dtAtc = new DataTable();
            dtAtc.Columns.Add("Código ATC (4 dígitos)", typeof(string));
            dtAtc.Columns.Add("Valor final total", typeof(string));
            foreach (var item in porAtc.Take(10))
                dtAtc.Rows.Add(item.Atc, item.Suma.ToString("N2", _culturaColombia));
            TopAtcView = dtAtc;

            var porLab = datos
                .GroupBy(x => TitleCase(x.Lab))
                .Select(g => new { Lab = g.Key, Suma = g.Sum(x => x.Valor) })
                .OrderByDescending(x => x.Suma)
                .ToList();

            double totalGeneral = porLab.Sum(x => x.Suma);
            var topLabs = porLab.Take(8).ToList();
            ParticipacionLaboratorioSeries = topLabs.Select(lab =>
            {
                double share = totalGeneral > 0 ? lab.Suma / totalGeneral : 0;
                return new PieSeries<double>
                {
                    Name = lab.Lab,
                    Values = new[] { lab.Suma },
                    DataLabelsSize = 15,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => $"{share:P1}",
                    ToolTipLabelFormatter = point => $"{lab.Suma:N0} ({share:P1})"
                };
            }).ToArray();
            OnPropertyChanged(nameof(ParticipacionLaboratorioSeries));

            TotalValorFinal = Math.Round(totalGeneral, 2);
            // TotalProductos should count distinct packs in the full cruzado, not only those with non-zero Valor
            var datosAll = _listaComparacionEspejo
                .Select(i => new
                {
                    Pack = esVentas ? i.PackUn : i.PackPx
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Pack))
                .ToList();

            TotalProductos = datosAll.Select(x => x.Pack).Distinct().Count();
            LaboratorioLider = topLabs.Any() ? topLabs.First().Lab : "—";
            TopAtcCodigo = porAtc.Any() ? porAtc.First().Atc : "—";
            double porcentajeTop = totalGeneral > 0 ? topLabs.Sum(l => l.Suma) / totalGeneral * 100 : 0;
            PorcentajeTopLaboratorios = $"{porcentajeTop:F1}%";

            StatusMessage = $"✅ Estadísticas ({(esVentas ? HojaSeleccionadaVentas : HojaSeleccionadaFormuladas)}) " +
                            $"sobre {datos.Count:N0} registros del cruce completo.";
        }
        private void ExportarEstadisticas()
        {
            if (_estadisticasView == null)
            {
                MessageBox.Show("Primero calcula las estadísticas.");
                return;
            }
            var sfd = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Estadisticas.xlsx" };
            if (sfd.ShowDialog() == true)
            {
                _excelService.ExportarDataTable(_estadisticasView, sfd.FileName);
                StatusMessage = $"Estadísticas exportadas a {sfd.FileName}";
            }
        }
    }
}