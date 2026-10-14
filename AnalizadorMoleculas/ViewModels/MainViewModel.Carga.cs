using AnalizadorMoleculas.Models;
using AnalizadorMoleculas.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AnalizadorMoleculas.ViewModels
{
    public partial class MainViewModel
    {
        public SqliteViewModel SqliteVM { get; } = new SqliteViewModel();

        private DataTable _hojaFormuladas;
        private DataTable _hojaVentas;
        private DataTable _hojaFormuladasView;
        private DataTable _hojaVentasView;
        private DataTable _resultadoComparacion;
        private DataTable _datosCompletosFormuladas;
        private DataTable _datosCompletosVentas;

        private int _pageSizeHoja = 200;
        private int _currentPageForm = 1;
        private int _currentPageVent = 1;
        private string _filterForm = "";
        private string _filterVent = "";

        public int PageSizeHoja
        {
            get => _pageSizeHoja;
            set { _pageSizeHoja = Math.Max(10, value); OnPropertyChanged(nameof(PageSizeHoja)); UpdatePagedViews(); }
        }

        public ICommand EliminarArchivoCommand { get; private set; }

        private async Task EliminarArchivoAsync()
        {
            try
            {
                var lista = await _databaseService.ObtenerHistorialAsync();
                if (lista == null || lista.Count == 0)
                {
                    StatusMessage = "No hay archivos para eliminar.";
                    return;
                }

                var ultimo = lista[0];
                var res = System.Windows.MessageBox.Show($"Eliminar historial {ultimo.NombreArchivo} ({ultimo.FechaRegistro})?", "Confirmar eliminación", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (res != System.Windows.MessageBoxResult.Yes) return;

                await _databaseService.EliminarAuditoriaAsync(ultimo.Id);
                if (SqliteVM.CargarHistorialSqliteCommand.CanExecute(null)) SqliteVM.CargarHistorialSqliteCommand.Execute(null);
                StatusMessage = $"Auditoría {ultimo.NombreArchivo} eliminada.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error eliminando: {ex.Message}";
            }
        }

        public int CurrentPageForm
        {
            get => _currentPageForm;
            set
            {
                _currentPageForm = Math.Max(1, value);
                OnPropertyChanged(nameof(CurrentPageForm));
                UpdatePagedViews();
            }
        }

        public int CurrentPageVent
        {
            get => _currentPageVent;
            set
            {
                _currentPageVent = Math.Max(1, value);
                OnPropertyChanged(nameof(CurrentPageVent));
                UpdatePagedViews();
            }
        }

        private int _totalRowsForm = 0;
        public int TotalRowsForm { get => _totalRowsForm; set { _totalRowsForm = value; OnPropertyChanged(nameof(TotalRowsForm)); } }

        private int _totalPagesForm = 1;
        public int TotalPagesForm { get => _totalPagesForm; set { _totalPagesForm = value; OnPropertyChanged(nameof(TotalPagesForm)); } }

        private int _totalRowsVent = 0;
        public int TotalRowsVent { get => _totalRowsVent; set { _totalRowsVent = value; OnPropertyChanged(nameof(TotalRowsVent)); } }

        private int _totalPagesVent = 1;
        public int TotalPagesVent { get => _totalPagesVent; set { _totalPagesVent = value; OnPropertyChanged(nameof(TotalPagesVent)); } }

        public string FilterForm
        {
            get => _filterForm;
            set { _filterForm = value ?? string.Empty; OnPropertyChanged(nameof(FilterForm)); CurrentPageForm = 1; UpdatePagedViews(); }
        }

        public string FilterVent
        {
            get => _filterVent;
            set { _filterVent = value ?? string.Empty; OnPropertyChanged(nameof(FilterVent)); CurrentPageVent = 1; UpdatePagedViews(); }
        }

        private readonly DatabaseService _databaseService = new DatabaseService();
        public DataTable HojaFormuladasView
        {
            get => _hojaFormuladasView;
            set { _hojaFormuladasView = value; OnPropertyChanged(nameof(HojaFormuladasView)); }
        }

        public DataTable HojaVentasView
        {
            get => _hojaVentasView;
            set { _hojaVentasView = value; OnPropertyChanged(nameof(HojaVentasView)); }
        }

        public DataTable ResultadoView
        {
            get => _resultadoComparacion;
            set { _resultadoComparacion = value; OnPropertyChanged(nameof(ResultadoView)); }
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

        public ICommand PrevPageFormCommand => _prevPageFormCommand ??= new RelayCommand(() => { if (CurrentPageForm > 1) CurrentPageForm--; });
        private ICommand _prevPageFormCommand;

        public ICommand NextPageFormCommand => _nextPageFormCommand ??= new RelayCommand(() => { CurrentPageForm++; });
        private ICommand _nextPageFormCommand;

        public ICommand PrevPageVentCommand => _prevPageVentCommand ??= new RelayCommand(() => { if (CurrentPageVent > 1) CurrentPageVent--; });
        private ICommand _prevPageVentCommand;

        public ICommand NextPageVentCommand => _nextPageVentCommand ??= new RelayCommand(() => { CurrentPageVent++; });
        private ICommand _nextPageVentCommand;

        private bool _cargaEnProgreso;
        public bool CargaEnProgreso
        {
            get => _cargaEnProgreso;
            set { _cargaEnProgreso = value; OnPropertyChanged(nameof(CargaEnProgreso)); }
        }

        private bool _cargandoHojas = false;

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

                        string nombreArchivo = System.IO.Path.GetFileName(_rutaActual);

                        var existente = await _databaseService.BuscarPorNombreArchivoAsync(nombreArchivo);

                        if (existente != null)
                        {
                            StatusMessage = $"ℹ️ \"{nombreArchivo}\" ya estaba guardado el " +
                                            $"{existente.FechaRegistro:dd/MM/yyyy HH:mm} " +
                                            $"({existente.TotalRegistros:N0} registros). Se cargaron los datos en pantalla " +
                                            $"sin duplicar el historial.";
                        }
                        else
                        {
                            StatusMessage = "Guardando en base de datos...";

                            int totalRegistros = (_datosCompletosFormuladas?.Rows.Count ?? 0) + (_datosCompletosVentas?.Rows.Count ?? 0);

                            await _databaseService.GuardarAuditoriaCompletaAsync(
                                nombreArchivo,
                                totalRegistros,
                                null,
                                _datosCompletosFormuladas,
                                _datosCompletosVentas);

                            if (SqliteVM.CargarHistorialSqliteCommand.CanExecute(null))
                            {
                                SqliteVM.CargarHistorialSqliteCommand.Execute(null);
                            }

                            StatusMessage = "✅ Archivo cargado y guardado correctamente en el historial.";
                        }
                    }
                    else
                    {
                        StatusMessage = "⚠ El archivo debe tener al menos dos hojas.";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"✗ Error: {ex.Message}";
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

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
            _hojaFormuladas = null;
            _hojaVentas = null;
            _datosCompletosFormuladas = null;
            _datosCompletosVentas = null;
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

                _datosCompletosFormuladas = resultado.DatosCompletosFormuladas;
                _datosCompletosVentas = resultado.DatosCompletosVentas;
                _hojaFormuladas = _datosCompletosFormuladas;
                _hojaVentas = _datosCompletosVentas;
                CurrentPageForm = 1;
                CurrentPageVent = 1;
                FilterForm = string.Empty;
                FilterVent = string.Empty;
                UpdatePagedViews();
                HojaAnalisisSeleccionada = HojaSeleccionadaFormuladas;

                const int IDX_ATC = 0, IDX_LAB = 1, IDX_PROD = 2, IDX_PACK = 3, IDX_VALOR = 4;

                _idxAtcForm = IDX_ATC;
                _idxLabForm = IDX_LAB;
                _idxPackForm = IDX_PACK;
                _idxValorForm = IDX_VALOR;

                _idxAtcVent = IDX_ATC;
                _idxLabVent = IDX_LAB;
                _idxPackVent = IDX_PACK;
                _idxValorVent = IDX_VALOR;

                ListaComparacionEspejo.LoadItems(resultado.Items);
                TotalPxCalculado = resultado.TotalPx;
                TotalUnCalculado = resultado.TotalUn;
                PorcentajeConversion = resultado.TotalPx > 0
                    ? $"{(resultado.TotalUn / resultado.TotalPx) * 100:N1}%"
                    : "0%";

                CargarListaLaboratorios();

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
            int minValorIndex = int.MaxValue;

            for (int i = 0; i < hoja.Columns.Count; i++)
            {
                string nombre = hoja.Columns[i].ColumnName.ToLower();
                if (nombre.Contains("pack") || nombre.Contains("presentación"))
                    idxPack = i;
                else if (nombre.Contains("laboratory") || nombre.Contains("lab"))
                    idxLab = i;
                else if (nombre.Contains("atc"))
                    idxAtc = i;
                else if (nombre.Contains("mar") || nombre.Contains("mat") || nombre.Contains("value") || Regex.IsMatch(nombre, "\\d{2}"))
                {
                    if (i < minValorIndex) minValorIndex = i;
                }
            }

            if (minValorIndex != int.MaxValue)
                idxValor = minValorIndex;
            else
                idxValor = hoja.Columns.Count - 1;
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

                    CargarListaLaboratorios();

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
                catch { }
            }

            return (dicValores, dicPacks);
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

        private void OnAuditoriaSqliteCargada(object sender, AuditoriaCargadaEventArgs e)
        {
            try
            {
                _rutaActual = e.Auditoria.NombreArchivo;
                _datosCompletosFormuladas = e.Formuladas;
                _datosCompletosVentas = e.Ventas;
                _hojaFormuladas = e.Formuladas;
                _hojaVentas = e.Ventas;

                NombresHojas.Clear();
                NombresHojas.Add("Formuladas");
                NombresHojas.Add("Ventas");
                _hojaSeleccionadaFormuladas = "Formuladas";
                _hojaSeleccionadaVentas = "Ventas";
                HojaAnalisisSeleccionada = "Formuladas";
                OnPropertyChanged(nameof(HojaSeleccionadaFormuladas));
                OnPropertyChanged(nameof(HojaSeleccionadaVentas));

                DetectarIndicesEnHoja(_hojaFormuladas, true);
                DetectarIndicesEnHoja(_hojaVentas, false);

                CurrentPageForm = 1;
                CurrentPageVent = 1;
                FilterForm = string.Empty;
                FilterVent = string.Empty;
                UpdatePagedViews();

                var comparacion = ConstruirComparacionDesdeTablas(_hojaFormuladas, _hojaVentas);
                ListaComparacionEspejo.LoadItems(comparacion.Items);
                TotalPxCalculado = comparacion.TotalPx;
                TotalUnCalculado = comparacion.TotalUn;
                PorcentajeConversion = comparacion.TotalPx > 0
                    ? $"{(comparacion.TotalUn / comparacion.TotalPx) * 100:N1}%"
                    : "0%";

                CargarListaLaboratorios();
                VistaActual = "espejo";
                StatusMessage = $"✓ Auditoría SQLite cargada: {e.Auditoria.NombreArchivo} ({ListaComparacionEspejo.TotalCount:N0} registros).";
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error cargando auditoría SQLite: {ex.Message}";
                MessageBox.Show($"Error cargando auditoría SQLite:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (List<EspejoComparativo> Items, double TotalPx, double TotalUn) ConstruirComparacionDesdeTablas(DataTable formuladas, DataTable ventas)
        {
            var registrosPx = LeerRegistrosComparacion(formuladas);
            var registrosUn = LeerRegistrosComparacion(ventas);

            var llaves = new List<(string atc, string lab, string prod)>();
            var vistas = new HashSet<(string atc, string lab, string prod)>();
            AgregarLlavesComparacion(registrosPx.Keys, llaves, vistas);
            AgregarLlavesComparacion(registrosUn.Keys, llaves, vistas);

            var items = new List<EspejoComparativo>();
            double totalPx = 0;
            double totalUn = 0;

            foreach (var llave in llaves)
            {
                registrosPx.TryGetValue(llave, out var listaPx);
                registrosUn.TryGetValue(llave, out var listaUn);
                listaPx ??= new List<RegistroComparacionSqlite>();
                listaUn ??= new List<RegistroComparacionSqlite>();

                var mapaPx = listaPx.GroupBy(r => ObtenerClavePackLocal(r.Pack)).ToDictionary(g => g.Key, g => g.ToList());
                var mapaUn = listaUn.GroupBy(r => ObtenerClavePackLocal(r.Pack)).ToDictionary(g => g.Key, g => g.ToList());

                var packs = new List<string>();
                var packsVistos = new HashSet<string>();
                foreach (var r in listaPx)
                {
                    string clave = ObtenerClavePackLocal(r.Pack);
                    if (packsVistos.Add(clave)) packs.Add(clave);
                }
                foreach (var r in listaUn)
                {
                    string clave = ObtenerClavePackLocal(r.Pack);
                    if (packsVistos.Add(clave)) packs.Add(clave);
                }

                foreach (string packKey in packs)
                {
                    var px = mapaPx.TryGetValue(packKey, out var lpx) ? lpx : new List<RegistroComparacionSqlite>();
                    var un = mapaUn.TryGetValue(packKey, out var lun) ? lun : new List<RegistroComparacionSqlite>();
                    int filas = Math.Max(px.Count, un.Count);

                    for (int i = 0; i < filas; i++)
                    {
                        var itemPx = i < px.Count ? px[i] : null;
                        var itemUn = i < un.Count ? un[i] : null;

                        totalPx += itemPx?.Valor ?? 0;
                        totalUn += itemUn?.Valor ?? 0;

                        items.Add(new EspejoComparativo
                        {
                            Atc = llave.atc,
                            Laboratorio = llave.lab,
                            Producto = llave.prod,
                            PackPx = itemPx?.Pack,
                            TotalRecetado = itemPx?.Valor ?? 0,
                            PackUn = itemUn?.Pack,
                            TotalVendido = itemUn?.Valor ?? 0
                        });
                    }
                }
            }

            return (items, totalPx, totalUn);
        }

        private Dictionary<(string atc, string lab, string prod), List<RegistroComparacionSqlite>> LeerRegistrosComparacion(DataTable tabla)
        {
            var resultado = new Dictionary<(string atc, string lab, string prod), List<RegistroComparacionSqlite>>();
            if (tabla == null || tabla.Rows.Count == 0) return resultado;

            int idxAtc = BuscarColumna(tabla, "ATC", 0);
            int idxLab = BuscarColumna(tabla, "Laboratorio", 1);
            int idxProd = BuscarColumna(tabla, "Producto", 2);
            int idxPack = BuscarColumna(tabla, "Presentacion", 3);
            int idxValor = BuscarColumna(tabla, "Unidades", Math.Max(0, tabla.Columns.Count - 1));

            foreach (DataRow row in tabla.Rows)
            {
                string atcRaw = row[idxAtc]?.ToString()?.Trim().ToUpperInvariant() ?? "";
                string atc = atcRaw.Length >= 4 ? atcRaw[..4] : atcRaw;
                if (string.IsNullOrWhiteSpace(atc)) atc = "SIN ATC";

                string lab = NormalizarTextoAbsoluto(row[idxLab]?.ToString());
                string prod = NormalizarTextoAbsoluto(row[idxProd]?.ToString());
                string pack = row[idxPack]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(pack)) pack = "SIN PACK";
                double valor = row[idxValor] is double dVal ? dVal : ConvertirADouble(row[idxValor]?.ToString());

                var llave = (atc, lab, prod);
                if (!resultado.TryGetValue(llave, out var lista))
                {
                    lista = new List<RegistroComparacionSqlite>();
                    resultado[llave] = lista;
                }

                lista.Add(new RegistroComparacionSqlite { Pack = pack, Valor = valor });
            }

            return resultado;
        }

        private static void AgregarLlavesComparacion(
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

        private static int BuscarColumna(DataTable tabla, string nombre, int fallback)
        {
            if (tabla.Columns.Contains(nombre)) return tabla.Columns[nombre].Ordinal;
            return Math.Min(Math.Max(0, fallback), tabla.Columns.Count - 1);
        }

        private string ObtenerClavePackLocal(string pack)
        {
            if (string.IsNullOrWhiteSpace(pack) || pack == "SIN PACK")
                return "SIN_PACK";

            string limpio = NormalizarTextoAbsoluto(pack);
            var palabras = limpio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return palabras.Length > 0 ? palabras[0] : "SIN_PACK";
        }

        private sealed class RegistroComparacionSqlite
        {
            public string Pack { get; set; }
            public double Valor { get; set; }
        }

        private void UpdatePagedViews()
        {
            try
            {
                if (_hojaFormuladas != null)
                {
                    var allRows = _hojaFormuladas.Rows.Cast<DataRow>();
                    if (!string.IsNullOrWhiteSpace(_filterForm))
                    {
                        string f = _filterForm.ToUpperInvariant();
                        allRows = allRows.Where(r => r.ItemArray.Any(c => (c?.ToString() ?? "").ToUpperInvariant().Contains(f)));
                    }
                    var allList = allRows.ToList();
                    TotalRowsForm = allList.Count;
                    TotalPagesForm = Math.Max(1, (int)Math.Ceiling((double)TotalRowsForm / _pageSizeHoja));
                    if (_currentPageForm > TotalPagesForm)
                    {
                        _currentPageForm = TotalPagesForm;
                        OnPropertyChanged(nameof(CurrentPageForm));
                    }

                    var tbl = _hojaFormuladas.Clone();
                    var skip = (CurrentPageForm - 1) * _pageSizeHoja;
                    foreach (var r in allList.Skip(skip).Take(_pageSizeHoja)) tbl.ImportRow(r);
                    HojaFormuladasView = tbl;
                }
                else
                {
                    HojaFormuladasView = null;
                    TotalRowsForm = 0; TotalPagesForm = 1; CurrentPageForm = 1;
                }

                if (_hojaVentas != null)
                {
                    var allRows = _hojaVentas.Rows.Cast<DataRow>();
                    if (!string.IsNullOrWhiteSpace(_filterVent))
                    {
                        string f = _filterVent.ToUpperInvariant();
                        allRows = allRows.Where(r => r.ItemArray.Any(c => (c?.ToString() ?? "").ToUpperInvariant().Contains(f)));
                    }
                    var allList = allRows.ToList();
                    TotalRowsVent = allList.Count;
                    TotalPagesVent = Math.Max(1, (int)Math.Ceiling((double)TotalRowsVent / _pageSizeHoja));
                    if (_currentPageVent > TotalPagesVent)
                    {
                        _currentPageVent = TotalPagesVent;
                        OnPropertyChanged(nameof(CurrentPageVent));
                    }

                    var tbl = _hojaVentas.Clone();
                    var skip = (CurrentPageVent - 1) * _pageSizeHoja;
                    foreach (var r in allList.Skip(skip).Take(_pageSizeHoja)) tbl.ImportRow(r);
                    HojaVentasView = tbl;
                }
                else
                {
                    HojaVentasView = null;
                    TotalRowsVent = 0; TotalPagesVent = 1; CurrentPageVent = 1;
                }
            }
            catch { }
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
    }
}
