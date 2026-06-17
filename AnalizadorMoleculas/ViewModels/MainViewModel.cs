using AnalizadorMoleculas.Models;
using AnalizadorMoleculas.Services;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AnalizadorMoleculas.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private readonly ExcelService _excelService = new ExcelService();
        private readonly CultureInfo _culturaColombia = new CultureInfo("es-CO");

        private DataTable _hojaFormuladas;
        private DataTable _hojaVentas;
        private DataTable _resultadoComparacion;
        private DataTable _estadisticasView;
        private DataTable _topProductosView;
        private DataTable _topAtcView;
        

        public DataTable HojaFormuladasView
        {
            get => _hojaFormuladas;
            set { _hojaFormuladas = value; OnPropertyChanged(nameof(HojaFormuladasView)); }
        }

        public DataTable HojaVentasView
        {
            get => _hojaVentas;
            set { _hojaVentas = value; OnPropertyChanged(nameof(HojaVentasView)); }
        }

        public DataTable ResultadoView
        {
            get => _resultadoComparacion;
            set { _resultadoComparacion = value; OnPropertyChanged(nameof(ResultadoView)); }
        }

        public DataTable EstadisticasView
        {
            get => _estadisticasView;
            set { _estadisticasView = value; OnPropertyChanged(nameof(EstadisticasView)); }
        }

        public DataTable TopProductosView
        {
            get => _topProductosView;
            set { _topProductosView = value; OnPropertyChanged(nameof(TopProductosView)); }
        }

        public DataTable TopAtcView
        {
            get => _topAtcView;
            set { _topAtcView = value; OnPropertyChanged(nameof(TopAtcView)); }
        }

        // Propiedades públicas
        public SolidColorPaint LegendBackgroundPaint { get; } =
            new SolidColorPaint(new SKColor(16, 16, 28)); // #10101C — igual a BgCard

        public SolidColorPaint LegendTextPaint { get; } =
            new SolidColorPaint(new SKColor(232, 232, 240));

        public ISeries[] TopProductosSeries { get; set; }
        public ISeries[] ParticipacionLaboratorioSeries { get; set; }

        private List<string> _topProductosLabels;
        public List<string> TopProductosLabels
        {
            get => _topProductosLabels;
            set { _topProductosLabels = value; OnPropertyChanged(nameof(TopProductosLabels)); }
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

        private string _topAtcCodigo;
        public string TopAtcCodigo
        {
            get => _topAtcCodigo;
            set { _topAtcCodigo = value; OnPropertyChanged(nameof(TopAtcCodigo)); }
        }

        private string _porcentajeTopLaboratorios;
        public string PorcentajeTopLaboratorios
        {
            get => _porcentajeTopLaboratorios;
            set { _porcentajeTopLaboratorios = value; OnPropertyChanged(nameof(PorcentajeTopLaboratorios)); }
        }

        private bool _cargaEnProgreso;
        public bool CargaEnProgreso
        {
            get => _cargaEnProgreso;
            set { _cargaEnProgreso = value; OnPropertyChanged(nameof(CargaEnProgreso)); }
        }

        private Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            set { _progressBarVisibility = value; OnPropertyChanged(nameof(ProgressBarVisibility)); }
        }

        private string _progresoTexto;
        public string ProgresoTexto
        {
            get => _progresoTexto;
            set { _progresoTexto = value; OnPropertyChanged(nameof(ProgresoTexto)); }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        private string _rutaActual;
        private int _idxPackForm = -1, _idxLabForm = -1, _idxValorForm = -1, _idxAtcForm = -1;
        private int _idxPackVent = -1, _idxLabVent = -1, _idxValorVent = -1, _idxAtcVent = -1;

        private ObservableCollection<string> _nombresHojas;
        public ObservableCollection<string> NombresHojas
        {
            get => _nombresHojas;
            set { _nombresHojas = value; OnPropertyChanged(nameof(NombresHojas)); }
        }

        private string _hojaSeleccionadaFormuladas;
        public string HojaSeleccionadaFormuladas
        {
            get => _hojaSeleccionadaFormuladas;
            set
            {
                _hojaSeleccionadaFormuladas = value;
                OnPropertyChanged(nameof(HojaSeleccionadaFormuladas));
                _ = CargarHojasSeleccionadasAsync();
            }
        }

        private string _hojaSeleccionadaVentas;
        public string HojaSeleccionadaVentas
        {
            get => _hojaSeleccionadaVentas;
            set
            {
                _hojaSeleccionadaVentas = value;
                OnPropertyChanged(nameof(HojaSeleccionadaVentas));
                _ = CargarHojasSeleccionadasAsync();
            }
        }

        private string _hojaAnalisisSeleccionada;
        public string HojaAnalisisSeleccionada
        {
            get => _hojaAnalisisSeleccionada;
            set { _hojaAnalisisSeleccionada = value; OnPropertyChanged(nameof(HojaAnalisisSeleccionada)); }
        }
        private string _vistaActual = "espejo";
        public string VistaActual
        {
            get => _vistaActual;
            set { _vistaActual = value; OnPropertyChanged(nameof(VistaActual)); }
        }

        public ICommand CargarCommand { get; }
        public ICommand GenerarComparacionCommand { get; }
        public ICommand ExportarCommand { get; }
        public ICommand GenerarInformeCommand { get; }
        public ICommand CalcularEstadisticasCommand { get; }
        public ICommand ExportarEstadisticasCommand { get; }
        public ICommand NavigateCommand { get; }        

        private VirtualizedObservableCollection<EspejoComparativo> _listaComparacionEspejo;
        public VirtualizedObservableCollection<EspejoComparativo> ListaComparacionEspejo
        {
            get
            {
                if (_listaComparacionEspejo == null)
                {
                    _listaComparacionEspejo = new VirtualizedObservableCollection<EspejoComparativo>(
                        (item, filter) =>
                        {
                            if (string.IsNullOrWhiteSpace(filter)) return true;
                            var f = filter.ToUpper();
                            return (item.Atc?.Contains(f) ?? false) ||
                                   (item.Laboratorio?.Contains(f) ?? false) ||
                                   (item.Producto?.Contains(f) ?? false) ||
                                   (item.PackPx?.Contains(f) ?? false) ||
                                   (item.PackUn?.Contains(f) ?? false);
                        }
                    );
                }
                return _listaComparacionEspejo;
            }
        }

        private string _filtroEspejo;
        public string FiltroEspejo
        {
            get => _filtroEspejo;
            set
            {
                _filtroEspejo = value;
                OnPropertyChanged(nameof(FiltroEspejo));
                ListaComparacionEspejo.ApplyFilter(value);
            }
        }

        private double _totalPxCalculado;
        public double TotalPxCalculado
        {
            get => _totalPxCalculado;
            set { _totalPxCalculado = value; OnPropertyChanged(nameof(TotalPxCalculado)); }
        }

        private double _totalUnCalculado;
        public double TotalUnCalculado
        {
            get => _totalUnCalculado;
            set { _totalUnCalculado = value; OnPropertyChanged(nameof(TotalUnCalculado)); }
        }

        private string _porcentajeConversion = "0%";
        public string PorcentajeConversion
        {
            get => _porcentajeConversion;
            set { _porcentajeConversion = value; OnPropertyChanged(nameof(PorcentajeConversion)); }
        }

        public async Task ProcesarComparacionEspejoRealAsync()
        {
            if (_hojaFormuladas == null || _hojaVentas == null) return;

            ProgressBarVisibility = Visibility.Visible;
            ProgresoTexto = "Analizando datos...";

            var dtForm = _hojaFormuladas;
            var dtVent = _hojaVentas;

            try
            {
                var resultado = await Task.Run(() =>
                {
                    ProgresoTexto = "Mapeando formuladas...";
                    var (valForm, packsForm) = MapearHojaConPacks(dtForm);

                    ProgresoTexto = "Mapeando ventas...";
                    var (valVent, packsVent) = MapearHojaConPacks(dtVent);

                    var todasLasLlaves = new HashSet<(string, string, string)>(valForm.Keys);
                    todasLasLlaves.UnionWith(valVent.Keys);

                    double acumPx = 0, acumUn = 0;
                    var lista = new List<EspejoComparativo>(todasLasLlaves.Count);
                    int procesadas = 0, total = todasLasLlaves.Count;

                    foreach (var llave in todasLasLlaves)
                    {
                        double px = valForm.GetValueOrDefault(llave);
                        double un = valVent.GetValueOrDefault(llave);
                        if (px == 0 && un == 0) continue;

                        acumPx += px;
                        acumUn += un;

                        string packsPx = packsForm.TryGetValue(llave, out var lp)
                            ? string.Join(" | ", lp) : null;
                        string packsUn = packsVent.TryGetValue(llave, out var lv)
                            ? string.Join(" | ", lv) : null;

                        lista.Add(new EspejoComparativo
                        {
                            Atc = llave.Item1,
                            Laboratorio = llave.Item2,
                            Producto = llave.Item3,
                            PackPx = packsPx,
                            PackUn = packsUn,
                            TotalRecetado = px,
                            TotalVendido = un
                        });

                        procesadas++;
                        if (procesadas % 1000 == 0)
                            ProgresoTexto = $"Cruzando... {procesadas:N0}/{total:N0}";
                    }

                    foreach (var v in packsForm.Values) v.Clear();
                    foreach (var v in packsVent.Values) v.Clear();
                    valForm.Clear(); valVent.Clear();
                    packsForm.Clear(); packsVent.Clear();

                    return new
                    {
                        Items = lista.OrderByDescending(x => x.TotalRecetado).ToList(),
                        TotalPx = acumPx,
                        TotalUn = acumUn
                    };
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ListaComparacionEspejo.LoadItems(resultado.Items);
                    TotalPxCalculado = resultado.TotalPx;
                    TotalUnCalculado = resultado.TotalUn;

                    PorcentajeConversion = resultado.TotalPx > 0
                        ? $"{((double)resultado.TotalUn / resultado.TotalPx) * 100:N1}%"
                        : "0%";

                    StatusMessage = $"✓ Cruce completado: {ListaComparacionEspejo.TotalCount:N0} registros";
                    ProgresoTexto = "";
                    ProgressBarVisibility = Visibility.Collapsed;
                });

                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"✗ Error: {ex.Message}";
                ProgresoTexto = "";
                ProgressBarVisibility = Visibility.Collapsed;
            }
        }

        private Dictionary<(string atc, string lab, string prod), List<string>> ExtraerPacksPorLlave(DataTable dt)
        {
            var resultado = new Dictionary<(string atc, string lab, string prod), List<string>>();

            int idxAtc = -1, idxLab = -1, idxProd = -1, idxPack = -1;

            for (int i = 0; i < dt.Columns.Count; i++)
            {
                string nombre = dt.Columns[i].ColumnName.ToLower();
                if (nombre.Contains("atc")) idxAtc = i;
                else if (nombre.Contains("laboratory") || nombre.Contains("lab")) idxLab = i;
                else if (nombre.Contains("product") || nombre.Contains("producto")) idxProd = i;
                else if (nombre.Contains("pack") || nombre.Contains("presentación")) idxPack = i;
            }

            if (idxAtc == -1) idxAtc = 0;
            if (idxLab == -1) idxLab = 1;
            if (idxProd == -1) idxProd = 2;
            if (idxPack == -1) idxPack = 3;

            if (idxAtc >= dt.Columns.Count || idxLab >= dt.Columns.Count ||
                idxProd >= dt.Columns.Count || idxPack >= dt.Columns.Count)
                return resultado;

            foreach (DataRow row in dt.Rows)
            {
                try
                {
                    string atcRaw = row[idxAtc]?.ToString() ?? "";
                    string atcLimpio = atcRaw.Trim().ToUpper();
                    string atc3 = atcLimpio.Length >= 4 ? atcLimpio.Substring(0, 4) : atcLimpio;
                    if (string.IsNullOrWhiteSpace(atc3)) atc3 = "SIN ATC";

                    string labLimpio = NormalizarTextoAbsoluto(row[idxLab]?.ToString());
                    string prodLimpio = NormalizarTextoAbsoluto(row[idxProd]?.ToString());
                    string pack = row[idxPack]?.ToString()?.Trim() ?? "SIN PACK";

                    var llave = (atc3, labLimpio, prodLimpio);

                    if (!resultado.ContainsKey(llave))
                        resultado[llave] = new List<string>();

                    if (!resultado[llave].Contains(pack))
                        resultado[llave].Add(pack);
                }
                catch
                {
                    continue;
                }
            }

            return resultado;
        }

        public MainViewModel()
        {
            CargarCommand = new RelayCommand(async () => await CargarExcelAsync());
            GenerarComparacionCommand = new RelayCommand(GenerarComparacion, () => ListaComparacionEspejo.TotalCount > 0);
            ExportarCommand = new RelayCommand(ExportarResultado, () => ListaComparacionEspejo.TotalCount > 0);
            GenerarInformeCommand = new RelayCommand(GenerarInforme, () => _resultadoComparacion != null);
            CalcularEstadisticasCommand = new RelayCommand(CalcularEstadisticas);
            ExportarEstadisticasCommand = new RelayCommand(ExportarEstadisticas, () => _estadisticasView != null);
            NavigateCommand = new RelayCommand<string>(Navegar);

            NombresHojas = new ObservableCollection<string>();
            StatusMessage = "Listo. Carga tu archivo Excel para comenzar.";
            InicializarMolecula(); 
        }

        private void Navegar(string vista)
        {
            if (!string.IsNullOrEmpty(vista))
            {
                VistaActual = vista;
            }
        }

        private async Task CargarExcelAsync()
        {
            var ofd = new OpenFileDialog { Filter = "Archivos Excel|*.xlsx;*.xls" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    _rutaActual = ofd.FileName;
                    var hojas = _excelService.ObtenerNombresHojas(_rutaActual);
                    NombresHojas.Clear();
                    foreach (var h in hojas)
                        NombresHojas.Add(h);

                    if (hojas.Count >= 2)
                    {
                        _hojaSeleccionadaFormuladas = hojas[0];
                        _hojaSeleccionadaVentas = hojas[1];

                        OnPropertyChanged(nameof(HojaSeleccionadaFormuladas));
                        OnPropertyChanged(nameof(HojaSeleccionadaVentas));

                        await CargarHojasSeleccionadasAsync();

                    }
                    else
                        StatusMessage = "⚠ El archivo debe tener al menos dos hojas.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"✗ Error: {ex.Message}";
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool _cargandoHojas = false;

        private async Task CargarHojasSeleccionadasAsync()
        {
            if (string.IsNullOrEmpty(_rutaActual)) return;
            if (string.IsNullOrEmpty(HojaSeleccionadaFormuladas) ||
                string.IsNullOrEmpty(HojaSeleccionadaVentas)) return;
            if (_cargandoHojas) return;

            _cargandoHojas = true;
            ProgressBarVisibility = Visibility.Visible;
            CargaEnProgreso = true;
            ProgresoTexto = "Iniciando carga...";

            HojaFormuladasView = null;
            HojaVentasView = null;
            ListaComparacionEspejo.Clear();
            TotalPxCalculado = 0;
            TotalUnCalculado = 0;
            PorcentajeConversion = "0%";

            try
            {
                int idxForm = NombresHojas.IndexOf(HojaSeleccionadaFormuladas);
                int idxVent = NombresHojas.IndexOf(HojaSeleccionadaVentas);
                if (idxForm < 0 || idxVent < 0) return;

                ProgresoTexto = "Leyendo y consolidando Excel...";

                var resultado = await Task.Run(() =>
                    _excelService.LeerResumenDosHojas(_rutaActual, idxForm, idxVent));

                _hojaFormuladas = resultado.PreviewFormuladas;
                _hojaVentas = resultado.PreviewVentas;
                HojaFormuladasView = resultado.PreviewFormuladas;
                HojaVentasView = resultado.PreviewVentas;
                HojaAnalisisSeleccionada = HojaSeleccionadaFormuladas;

                _idxPackForm = resultado.IndicesFormuladas.IdxPack;
                _idxLabForm = resultado.IndicesFormuladas.IdxLab;
                _idxValorForm = resultado.IndicesFormuladas.IdxValor;
                _idxAtcForm = resultado.IndicesFormuladas.IdxAtc;

                _idxPackVent = resultado.IndicesVentas.IdxPack;
                _idxLabVent = resultado.IndicesVentas.IdxLab;
                _idxValorVent = resultado.IndicesVentas.IdxValor;
                _idxAtcVent = resultado.IndicesVentas.IdxAtc;

                ListaComparacionEspejo.LoadItems(resultado.Items);
                TotalPxCalculado = resultado.TotalPx;
                TotalUnCalculado = resultado.TotalUn;
                PorcentajeConversion = resultado.TotalPx > 0
                    ? $"{(resultado.TotalUn / resultado.TotalPx) * 100:N1}%"
                    : "0%";

                StatusMessage = $"✓ Cruce completado: {ListaComparacionEspejo.TotalCount:N0} registros. " +
                                $"{HojaSeleccionadaFormuladas}: {resultado.FilasFormuladas:N0} filas | " +
                                $"{HojaSeleccionadaVentas}: {resultado.FilasVentas:N0} filas";

                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                StatusMessage = $"✗ Error: {ex.Message}";
                MessageBox.Show($"Error al cargar: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _cargandoHojas = false;
                CargaEnProgreso = false;
                ProgressBarVisibility = Visibility.Collapsed;
                ProgresoTexto = "";

                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
            }
        }

        private void DetectarIndicesEnHoja(DataTable hoja, bool esFormuladas)
        {
            int idxPack = -1, idxLab = -1, idxValor = -1, idxAtc = -1;
            int anioMaximo = 0;

            for (int i = 0; i < hoja.Columns.Count; i++)
            {
                string nombre = hoja.Columns[i].ColumnName.ToLower();
                if (nombre.Contains("pack") || nombre.Contains("presentación"))
                    idxPack = i;
                else if (nombre.Contains("laboratory") || nombre.Contains("lab"))
                    idxLab = i;
                else if (nombre.Contains("atc"))
                    idxAtc = i;
                else if (nombre.Contains("mar") || nombre.Contains("mat") || nombre.Contains("value"))
                {
                    var match = Regex.Match(nombre, @"\d{2}");
                    if (match.Success && int.TryParse(match.Value, out int anio))
                    {
                        if (anio > anioMaximo)
                        {
                            anioMaximo = anio;
                            idxValor = i;
                        }
                    }
                }
            }

            if (idxValor == -1) idxValor = hoja.Columns.Count - 1;
            if (idxPack == -1) idxPack = 3;
            if (idxLab == -1) idxLab = 1;
            if (idxAtc == -1) idxAtc = 0;

            if (esFormuladas)
            {
                _idxPackForm = idxPack;
                _idxLabForm = idxLab;
                _idxValorForm = idxValor;
                _idxAtcForm = idxAtc;
            }
            else
            {
                _idxPackVent = idxPack;
                _idxLabVent = idxLab;
                _idxValorVent = idxValor;
                _idxAtcVent = idxAtc;
            }
        }

        private double ConvertirADouble(string valorStr)
        {
            if (string.IsNullOrWhiteSpace(valorStr)) return 0;
            valorStr = valorStr.Trim().Replace(" ", "");

            if (double.TryParse(valorStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double invariante))
                return Math.Round(invariante, 2);

            valorStr = valorStr.Replace(".", "").Replace(",", ".");
            if (double.TryParse(valorStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double resultado))
                return Math.Round(resultado, 2);
            return 0;
        }

        private string ObtenerPrefijoATC(string atcCompleto)
        {
            if (string.IsNullOrWhiteSpace(atcCompleto)) return "";
            if (atcCompleto.Length >= 4)
                return atcCompleto.Substring(0, 4).ToUpper();
            return atcCompleto.ToUpper();
        }

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
                    ToolTipLabelFormatter = point => $"{lab.Lab}\n{lab.Suma:N0} ({share:P1})"
                };
            }).ToArray();
            OnPropertyChanged(nameof(ParticipacionLaboratorioSeries));

            TotalValorFinal = Math.Round(totalGeneral, 2);
            TotalProductos = datos.Select(x => x.Pack).Distinct().Count();
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
        private void GenerarComparacion()
        {
            if (_hojaFormuladas == null || _hojaVentas == null)
            {
                MessageBox.Show("Primero carga las hojas.");
                return;
            }
            if (_idxPackForm == -1 || _idxLabForm == -1 || _idxValorForm == -1 || _idxAtcForm == -1 ||
                _idxPackVent == -1 || _idxLabVent == -1 || _idxValorVent == -1 || _idxAtcVent == -1)
            {
                DetectarIndicesEnHoja(_hojaFormuladas, true);
                DetectarIndicesEnHoja(_hojaVentas, false);
            }

            int idxProdForm = DetectarIndiceProducto(_hojaFormuladas);
            int idxProdVent = DetectarIndiceProducto(_hojaVentas);

            var gruposForm = new Dictionary<(string atc, string lab, string prefijoProd, string prefijoPack), List<(string pack, double valor)>>();
            foreach (DataRow row in _hojaFormuladas.Rows)
            {
                string atcCompleto = row[_idxAtcForm]?.ToString().Trim();
                string atcPrefijo = ObtenerPrefijoATC(atcCompleto);
                if (string.IsNullOrEmpty(atcPrefijo)) continue;
                string lab = NormalizarLaboratorio(row[_idxLabForm]?.ToString());
                string pack = row[_idxPackForm]?.ToString().Trim();
                if (string.IsNullOrEmpty(pack)) continue;
                string prefijoProd = ObtenerPrefijoProducto(row[idxProdForm]?.ToString());
                string prefijoPack = ObtenerPrefijoPack(pack);
                double valor = ConvertirADouble(row[_idxValorForm]?.ToString());
                var key = (atcPrefijo, lab, prefijoProd, prefijoPack);
                if (!gruposForm.ContainsKey(key))
                    gruposForm[key] = new List<(string pack, double valor)>();
                gruposForm[key].Add((pack, valor));
            }

            var gruposVent = new Dictionary<(string atc, string lab, string prefijoProd, string prefijoPack), List<(string pack, double valor)>>();
            foreach (DataRow row in _hojaVentas.Rows)
            {
                string atcCompleto = row[_idxAtcVent]?.ToString().Trim();
                string atcPrefijo = ObtenerPrefijoATC(atcCompleto);
                if (string.IsNullOrEmpty(atcPrefijo)) continue;
                string lab = NormalizarLaboratorio(row[_idxLabVent]?.ToString());
                string pack = row[_idxPackVent]?.ToString().Trim();
                if (string.IsNullOrEmpty(pack)) continue;
                string prefijoProd = ObtenerPrefijoProducto(row[idxProdVent]?.ToString());
                string prefijoPack = ObtenerPrefijoPack(pack);
                double valor = ConvertirADouble(row[_idxValorVent]?.ToString());
                var key = (atcPrefijo, lab, prefijoProd, prefijoPack);
                if (!gruposVent.ContainsKey(key))
                    gruposVent[key] = new List<(string pack, double valor)>();
                gruposVent[key].Add((pack, valor));
            }

            var todasClaves = new HashSet<(string atc, string lab, string prefijoProd, string prefijoPack)>(gruposForm.Keys);
            todasClaves.UnionWith(gruposVent.Keys);

            var dt = new DataTable();
            dt.Columns.Add("Pack (Formuladas)", typeof(string));
            dt.Columns.Add("Valor final (Formuladas)", typeof(string));
            dt.Columns.Add("Pack (Ventas)", typeof(string));
            dt.Columns.Add("Valor final (Ventas)", typeof(string));

            foreach (var key in todasClaves
                         .OrderBy(k => k.atc)
                         .ThenBy(k => k.lab)
                         .ThenBy(k => k.prefijoProd)
                         .ThenBy(k => k.prefijoPack))
            {
                var listaForm = gruposForm.ContainsKey(key)
                    ? gruposForm[key].OrderBy(x => x.pack).ToList()
                    : new List<(string pack, double valor)>();
                var listaVent = gruposVent.ContainsKey(key)
                    ? gruposVent[key].OrderBy(x => x.pack).ToList()
                    : new List<(string pack, double valor)>();
                int maxFilas = Math.Max(listaForm.Count, listaVent.Count);
                for (int i = 0; i < maxFilas; i++)
                {
                    string packForm = i < listaForm.Count ? listaForm[i].pack : null;
                    string valorForm = i < listaForm.Count ? listaForm[i].valor.ToString("N2", _culturaColombia) : null;
                    string packVent = i < listaVent.Count ? listaVent[i].pack : null;
                    string valorVent = i < listaVent.Count ? listaVent[i].valor.ToString("N2", _culturaColombia) : null;
                    dt.Rows.Add(packForm, valorForm, packVent, valorVent);
                }

                dt.Rows.Add(null, null, null, null);
            }

            _resultadoComparacion = dt;
            ResultadoView = dt;
            OnPropertyChanged(nameof(ResultadoView));
            StatusMessage = $"Comparación generada: {dt.Rows.Count:N0} filas.";
        }
        private string ObtenerPrefijoProducto(string producto)
        {
            string limpio = NormalizarTextoAbsoluto(producto);
            var partes = limpio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return partes.Length > 0 ? partes[0] : "SIN_DATO";
        }

        private static int DetectarIndiceProducto(DataTable hoja)
        {
            for (int i = 0; i < hoja.Columns.Count; i++)
            {
                string n = hoja.Columns[i].ColumnName.ToUpper();
                if (n.Contains("PRODUCT") || n.Contains("PRODUCTO")) return i;
            }
            return Math.Min(2, hoja.Columns.Count - 1);
        }

        private static string ObtenerPrefijoPack(string pack)
        {
            if (string.IsNullOrWhiteSpace(pack)) return "";
            var partes = pack.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return partes.Length > 0 ? partes[0].ToUpper() : "";
        }

        private static string NormalizarLaboratorio(string lab)
        {
            if (string.IsNullOrWhiteSpace(lab)) return "No indicado";
            lab = Regex.Replace(lab, @"\s+", " ").Trim();
            return CultureInfo.GetCultureInfo("es-CO").TextInfo.ToTitleCase(lab.ToLower());
        }

        private void ExportarResultado()
        {
            if (_listaComparacionEspejo == null || _listaComparacionEspejo.Count == 0)
            {
                MessageBox.Show("Primero carga los archivos.");
                return;
            }
            var sfd = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Comparacion_Espejo.xlsx" };
            if (sfd.ShowDialog() == true)
            {
                var dtExport = new DataTable();
                dtExport.Columns.Add("Código ATC", typeof(string));
                dtExport.Columns.Add("Laboratorio", typeof(string));
                dtExport.Columns.Add("Pack PX", typeof(string));
                dtExport.Columns.Add("Recetado (PX)", typeof(double));
                dtExport.Columns.Add("Pack UN", typeof(string));
                dtExport.Columns.Add("Vendido (UN)", typeof(double));

                foreach (var item in _listaComparacionEspejo)
                {
                    dtExport.Rows.Add(item.Atc, item.Laboratorio,
                        item.PackPx, item.TotalRecetado, item.PackUn, item.TotalVendido);
                }

                _excelService.ExportarDataTable(dtExport, sfd.FileName);
                StatusMessage = $"✓ Exportado: {sfd.FileName}";
            }
        }
        private void GenerarInforme()
        {
            MessageBox.Show("Función no disponible aún en esta versión.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private (Dictionary<(string atc, string lab, string prod), double> valores,
         Dictionary<(string atc, string lab, string prod), List<string>> packs)
    MapearHojaConPacks(DataTable dt)
        {
            var dicValores = new Dictionary<(string, string, string), double>();
            var dicPacks = new Dictionary<(string, string, string), List<string>>();

            if (dt == null || dt.Rows.Count == 0) return (dicValores, dicPacks);

            int total = dt.Columns.Count;
            int idxAtc = -1, idxLab = -1, idxProd = -1, idxPack = -1;

            for (int i = 0; i < total; i++)
            {
                string n = dt.Columns[i].ColumnName.ToUpper();
                if (n.Contains("ATC") && idxAtc == -1) idxAtc = i;
                else if ((n.Contains("LABORATORY") || n.Contains("LAB")) && idxLab == -1) idxLab = i;
                else if ((n.Contains("PRODUCT") || n.Contains("PRODUCTO")) && idxProd == -1) idxProd = i;
                else if (n.Contains("PACK") || n.Contains("PRESENTACI")) idxPack = i;
            }

            if (idxAtc == -1) idxAtc = 0;
            if (idxLab == -1) idxLab = 1;
            if (idxProd == -1) idxProd = 2;
            if (idxPack == -1) idxPack = 3;

            int inicioValores = Math.Max(0, total - 5);

            foreach (DataRow row in dt.Rows)
            {
                try
                {
                    string atcRaw = row[idxAtc]?.ToString()?.Trim().ToUpper() ?? "";
                    string atc3 = atcRaw.Length >= 4 ? atcRaw[..4] : atcRaw;
                    if (string.IsNullOrWhiteSpace(atc3)) atc3 = "SIN ATC";

                    string lab = NormalizarTextoAbsoluto(row[idxLab]?.ToString());
                    string prod = NormalizarTextoAbsoluto(row[idxProd]?.ToString());
                    string pack = row[idxPack]?.ToString()?.Trim() ?? "SIN PACK";

                    var llave = (atc3, lab, prod);

                    double suma = 0;
                    for (int i = inicioValores; i < total; i++)
                    {
                        suma += ConvertirADouble(row[i]?.ToString());
                    }

                    if (suma != 0)
                    {
                        if (dicValores.TryGetValue(llave, out double existing))
                            dicValores[llave] = existing + suma;
                        else
                            dicValores[llave] = suma;
                    }

                    if (!dicPacks.TryGetValue(llave, out var lista))
                    {
                        lista = new List<string>(2);
                        dicPacks[llave] = lista;
                    }
                    if (!lista.Contains(pack) && lista.Count < 4)
                        lista.Add(pack);
                }
                catch { /* fila dañada — ignorar */ }
            }

            return (dicValores, dicPacks);
        }

        private string NormalizarTextoAbsoluto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "SIN_DATO";

            string resultado = texto.ToUpper().Trim();
            resultado = resultado.Replace("\u00A0", " ");

            string textoNormalizado = resultado.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in textoNormalizado)
            {
                System.Globalization.UnicodeCategory uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
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

            resultado = Regex.Replace(resultado, @"\s+", " ");

            return resultado.Trim();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;
        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);
        public void Execute(object parameter) => _execute((T)parameter);
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}