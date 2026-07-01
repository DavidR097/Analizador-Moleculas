using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;

namespace AnalizadorMoleculas.ViewModels
{
    public partial class MainViewModel
    {
        // Limitar filas mostradas en la vista de brechas para optimizar memoria
        private const int MaxRowsBrechas = 1000;

        private ISeries[] _brechasSeriesParticipacion = Array.Empty<ISeries>();
        public ISeries[] BrechasSeriesParticipacion
        {
            get => _brechasSeriesParticipacion;
            set { _brechasSeriesParticipacion = value; OnPropertyChanged(nameof(BrechasSeriesParticipacion)); }
        }

        private ISeries[] _brechasSeriesParticipacion100 = Array.Empty<ISeries>();
        public ISeries[] BrechasSeriesParticipacion100
        {
            get => _brechasSeriesParticipacion100;
            set { _brechasSeriesParticipacion100 = value; OnPropertyChanged(nameof(BrechasSeriesParticipacion100)); }
        }

        private ISeries[] _brechasSeriesTendencia = Array.Empty<ISeries>();
        public ISeries[] BrechasSeriesTendencia
        {
            get => _brechasSeriesTendencia;
            set { _brechasSeriesTendencia = value; OnPropertyChanged(nameof(BrechasSeriesTendencia)); }
        }

        private Axis[] _brechasXAxes = Array.Empty<Axis>();
        public Axis[] BrechasXAxes
        {
            get => _brechasXAxes;
            set { _brechasXAxes = value; OnPropertyChanged(nameof(BrechasXAxes)); }
        }

        private Axis[] _brechasYAxes = Array.Empty<Axis>();
        public Axis[] BrechasYAxes
        {
            get => _brechasYAxes;
            set { _brechasYAxes = value; OnPropertyChanged(nameof(BrechasYAxes)); }
        }
        private Axis[] _brechasXAxesEvolucion = Array.Empty<Axis>();
        public Axis[] BrechasXAxesEvolucion
        {
            get => _brechasXAxesEvolucion;
            set { _brechasXAxesEvolucion = value; OnPropertyChanged(nameof(BrechasXAxesEvolucion)); }
        }

        private Axis[] _brechasYAxesPorcentaje = Array.Empty<Axis>();
        public Axis[] BrechasYAxesPorcentaje
        {
            get => _brechasYAxesPorcentaje;
            set { _brechasYAxesPorcentaje = value; OnPropertyChanged(nameof(BrechasYAxesPorcentaje)); }
        }
        private ObservableCollection<BrechaMoleculaModel> _listaBrechas;
        public ObservableCollection<BrechaMoleculaModel> ListaBrechas
        {
            get => _listaBrechas;
            set { _listaBrechas = value; OnPropertyChanged(nameof(ListaBrechas)); }
        }

        private bool _isCargandoBrechas;
        public bool IsCargandoBrechas
        {
            get => _isCargandoBrechas;
            set { _isCargandoBrechas = value; OnPropertyChanged(nameof(IsCargandoBrechas)); }
        }

        private string _totalBrechaUnidades = "0 Unidades";
        public string TotalBrechaUnidades
        {
            get => _totalBrechaUnidades;
            set { _totalBrechaUnidades = value; OnPropertyChanged(nameof(TotalBrechaUnidades)); }
        }

        private string _moleculaMayorDesviacion = "Ninguna";
        public string MoleculaMayorDesviacion
        {
            get => _moleculaMayorDesviacion;
            set { _moleculaMayorDesviacion = value; OnPropertyChanged(nameof(MoleculaMayorDesviacion)); }
        }

        private string _porcentajeSimetriaGlobal = "100%";
        public string PorcentajeSimetriaGlobal
        {
            get => _porcentajeSimetriaGlobal;
            set { _porcentajeSimetriaGlobal = value; OnPropertyChanged(nameof(PorcentajeSimetriaGlobal)); }
        }

        private ICommand _calcularBrechasCommand;
        public ICommand CalcularBrechasCommand => _calcularBrechasCommand ??= new RelayCommand(async () => await CalcularAnalisisBrechasAsync());
        public async Task CalcularAnalisisBrechasAsync()
        {
            if (_hojaFormuladas == null || _hojaVentas == null)
            {
                MostrarMensaje("Por favor, cargue primero las bases de datos de Excel para realizar el análisis de brechas.",
                               "Datos Faltantes", MessageBoxImage.Warning);
                return;
            }

            IsCargandoBrechas = true;

            await Task.Run(() =>
            {
                try
                {
                    if (_idxAtcForm == -1 || _idxValorForm == -1 || _idxAtcVent == -1 || _idxValorVent == -1)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DetectarIndicesEnHoja(_hojaFormuladas, true);
                            DetectarIndicesEnHoja(_hojaVentas, false);
                        });
                    }

                    var dicFormulados = AgruparBrechasSeguro(_hojaFormuladas, _idxAtcForm, _idxValorForm);
                    var dicVentas = AgruparBrechasSeguro(_hojaVentas, _idxAtcVent, _idxValorVent);

                    if (dicFormulados.Count == 0 && dicVentas.Count == 0)
                    {
                        MostrarMensaje("No se encontraron registros válidos para comparar. Verifique las columnas del Excel.",
                                       "Cruce Vacío", MessageBoxImage.Information);
                        return;
                    }

                    var todasLasMoleculas = dicFormulados.Keys.Union(dicVentas.Keys).ToList();
                    var listaResultado = new List<BrechaMoleculaModel>();

                    double totalDiferenciaAbsoluta = 0;
                    double totalUnidadesGlobales = 0;

                    foreach (var mol in todasLasMoleculas)
                    {
                        dicFormulados.TryGetValue(mol, out double demanadInstitucional);
                        dicVentas.TryGetValue(mol, out double ventaComercial);

                        double brechaAbsoluta = ventaComercial - demanadInstitucional;
                        double brechaValorAbsoluto = Math.Abs(brechaAbsoluta);

                        double maxUnidades = Math.Max(demanadInstitucional, ventaComercial);
                        double porcentajeCoincidencia = maxUnidades > 0
                            ? (1.0 - (brechaValorAbsoluto / maxUnidades)) * 100.0
                            : 100.0;

                        // Normalizar a rango [0,100]
                        porcentajeCoincidencia = Math.Max(0.0, Math.Min(100.0, porcentajeCoincidencia));

                        totalDiferenciaAbsoluta += brechaValorAbsoluto;
                        totalUnidadesGlobales += maxUnidades;

                        listaResultado.Add(new BrechaMoleculaModel
                        {
                            Molecula = mol,
                            UnidadesInstitucional = demanadInstitucional,
                            UnidadesComercial = ventaComercial,
                            BrechaAbsoluta = brechaAbsoluta,
                            BrechaValorAbsoluto = brechaValorAbsoluto,
                            IndiceSimetria = porcentajeCoincidencia
                        });
                    }

                    var listaOrdenada = listaResultado.OrderByDescending(b => b.BrechaValorAbsoluto).ToList();

                    double simetriaGlobal = totalUnidadesGlobales > 0
                        ? (1.0 - (totalDiferenciaAbsoluta / totalUnidadesGlobales)) * 100.0
                        : 100.0;

                    simetriaGlobal = Math.Max(0.0, Math.Min(100.0, simetriaGlobal));

                    var liderDesviacion = listaOrdenada.FirstOrDefault();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var toShow = listaOrdenada.Take(MaxRowsBrechas).ToList();
                        ListaBrechas = new ObservableCollection<BrechaMoleculaModel>(toShow);

                        TotalBrechaUnidades = $"{totalDiferenciaAbsoluta:N0} UN";
                        PorcentajeSimetriaGlobal = $"{simetriaGlobal:F1}%";

                        MoleculaMayorDesviacion = liderDesviacion != null
                            ? $"{liderDesviacion.Molecula} ({liderDesviacion.BrechaAbsoluta:+0;-0;0} UN)"
                            : "Ninguna";

                        if (listaOrdenada.Count > MaxRowsBrechas)
                        {
                            StatusMessage = $"✓ Análisis de brechas completado · Mostrando primeros {MaxRowsBrechas} moléculas (de {listaOrdenada.Count}).";
                        }
                        else
                        {
                            StatusMessage = "✓ Análisis de brechas completado.";
                        }

                        // Construir gráfica de barras para las principales brechas (top 8)
                        var topChart = listaOrdenada.Take(8).ToList();
                        if (topChart.Any())
                        {
                            BrechasSeriesParticipacion = new ISeries[]
                            {
                                new ColumnSeries<double>
                                {
                                    Name = "Brecha Absoluta",
                                    Values = topChart.Select(t => Math.Round(t.BrechaValorAbsoluto, 0)).ToArray(),
                                    Fill = new SolidColorPaint(new SKColor(247,37,133,220)),
                                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                                    DataLabelsSize = 10,
                                    DataLabelsFormatter = p => ((double)p.Coordinate.PrimaryValue).ToString("N0", _culturaColombia)
                                }
                            };

                            BrechasXAxes = new Axis[]
                            {
                                new Axis
                                {
                                    Labels = topChart.Select(t => t.Molecula).ToList(),
                                    LabelsRotation = -35,
                                    TextSize = 10
                                }
                            };

                            BrechasYAxes = new Axis[]
                            {
                                new Axis
                                {
                                    TextSize = 11,
                                    Labeler = val => val.ToString("#,##0", _culturaColombia)
                                }
                            };

                            // Construir gráfica Participación (100%) - barras apiladas normalizadas
                            var denomList = topChart.Select(t => t.UnidadesInstitucional + t.UnidadesComercial).ToList();
                            var instPct = topChart.Select((t, i) => denomList[i] > 0 ? (t.UnidadesInstitucional / denomList[i]) * 100.0 : 0.0).ToArray();
                            var comPct = topChart.Select((t, i) => denomList[i] > 0 ? (t.UnidadesComercial / denomList[i]) * 100.0 : 0.0).ToArray();

                            BrechasSeriesParticipacion100 = new ISeries[]
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

                            BrechasXAxesEvolucion = new Axis[]
                            {
                                new Axis
                                {
                                    Labels = topChart.Select(t => t.Molecula).ToList(),
                                    LabelsRotation = -35,
                                    TextSize = 10
                                }
                            };

                            BrechasYAxesPorcentaje = new Axis[]
                            {
                                new Axis
                                {
                                    TextSize = 11,
                                    Labeler = val => $"{val:0}%"
                                }
                            };

                            // Construir gráfica de Tendencia (línea con sombreado) usando Ventas (UN) como ejemplo
                            var ventas = topChart.Select(t => Math.Round(t.UnidadesComercial, 0)).ToArray();
                            BrechasSeriesTendencia = new ISeries[]
                            {
                                new LineSeries<double>
                                {
                                    Name = "Ventas (UN)",
                                    Values = ventas,
                                    Stroke = new SolidColorPaint(new SKColor(76,201,240,255)) { StrokeThickness = 2 },
                                    Fill = new SolidColorPaint(new SKColor(76,201,240,80)),
                                    GeometrySize = 6,
                                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                                    DataLabelsSize = 10
                                }
                            };

                            // Ejes para tendencia
                            // Reusar BrechasXAxesEvolucion y BrechasYAxesPorcentaje (pero ajustar labeler para valores absolutos)
                            // Para la tendencia usamos un eje Y numérico
                            // Nota: BrechasYAxesPorcentaje se mantiene para la vista 100%; MolXAxesEvolucion usado por ambos
                        }
                        else
                        {
                            BrechasSeriesParticipacion = Array.Empty<ISeries>();
                            BrechasXAxes = Array.Empty<Axis>();
                            BrechasYAxes = Array.Empty<Axis>();
                        }
                    });
                }
                catch (Exception ex)
                {
                    MostrarMensaje($"Error analizando brechas: {ex.Message}", "Error Interno", MessageBoxImage.Error);
                }
                finally
                {
                    Application.Current.Dispatcher.Invoke(() => IsCargandoBrechas = false);
                }
            });
        }

        private Dictionary<string, double> AgruparBrechasSeguro(DataTable hoja, int colAtc, int colValorInicial)
        {
            var diccionario = new Dictionary<string, double>();
            int totalColumnas = hoja.Columns.Count;

            if (colAtc < 0 || colAtc >= totalColumnas) return diccionario;

            int inicioValores = (colValorInicial >= 0 && colValorInicial < totalColumnas) ? colValorInicial : totalColumnas - 1;

            foreach (DataRow row in hoja.Rows)
            {
                string moleculaCruda = row[colAtc]?.ToString().Trim().ToUpper() ?? "";
                if (string.IsNullOrEmpty(moleculaCruda)) continue;

                string molecula = moleculaCruda.Length > 4 ? moleculaCruda.Substring(0, 4) : moleculaCruda;

                double sumaFila = 0;
                for (int i = inicioValores; i < totalColumnas; i++)
                {
                    sumaFila += ConvertirADouble(row[i]?.ToString());
                }

                if (diccionario.ContainsKey(molecula))
                    diccionario[molecula] += sumaFila;
                else
                    diccionario[molecula] = sumaFila;
            }

            return diccionario;
        }

        private void MostrarMensaje(string mensaje, string titulo = "Información", MessageBoxImage icono = MessageBoxImage.Information)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, icono);
            });
        }
    }

    public class BrechaMoleculaModel
    {
        public string Molecula { get; set; }
        public double UnidadesInstitucional { get; set; }
        public double UnidadesComercial { get; set; }
        public double BrechaAbsoluta { get; set; } 
        public double BrechaValorAbsoluto { get; set; } 
        public double IndiceSimetria { get; set; } 

        // Formatted properties for UI
        public string UnidadesInstitucionalFmt => UnidadesInstitucional.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        public string UnidadesComercialFmt => UnidadesComercial.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        public string BrechaAbsolutaFmt => BrechaAbsoluta.ToString("+#,##0;-#,##0;0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        public string BrechaValorAbsolutoFmt => BrechaValorAbsoluto.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        public string IndiceSimetriaFmt => IndiceSimetria.ToString("F1", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));

        public string AlertaEstado => IndiceSimetria >= 85 ? "✓ Simétrico" : IndiceSimetria >= 60 ? "⚠ Desviación Moderada" : "⚡ Brecha Crítica";
        public string ColorAlerta => IndiceSimetria >= 85 ? "#3DDC84" : IndiceSimetria >= 60 ? "#FFB703" : "#F72585";
    }
}