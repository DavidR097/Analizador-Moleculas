using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AnalizadorMoleculas.ViewModels
{
    public partial class MainViewModel
    {
        // ==========================================
        // PROPIEDADES PARA LA VISTA DE BRECHAS
        // ==========================================

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

        // KPIs de Brechas
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

        // ==========================================
        // COMANDOS
        // ==========================================
        private ICommand _calcularBrechasCommand;
        public ICommand CalcularBrechasCommand => _calcularBrechasCommand ??= new RelayCommand(async () => await CalcularAnalisisBrechasAsync());

        // ==========================================
        // MÉTODO DE ANÁLISIS DE BRECHAS RECONSTRUIDO
        // ==========================================
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
                    // 1. Asegurarnos de que los índices dinámicos estén calculados
                    if (_idxAtcForm == -1 || _idxValorForm == -1 || _idxAtcVent == -1 || _idxValorVent == -1)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DetectarIndicesEnHoja(_hojaFormuladas, true);
                            DetectarIndicesEnHoja(_hojaVentas, false);
                        });
                    }

                    // 2. Procesar hojas usando el motor dinámico blindado
                    var dicFormulados = AgruparBrechasSeguro(_hojaFormuladas, _idxAtcForm, _idxValorForm);
                    var dicVentas = AgruparBrechasSeguro(_hojaVentas, _idxAtcVent, _idxValorVent);

                    // Validación para avisarte si la lectura quedó en ceros
                    if (dicFormulados.Count == 0 && dicVentas.Count == 0)
                    {
                        MostrarMensaje("No se encontraron registros válidos para comparar. Verifique las columnas del Excel.",
                                       "Cruce Vacío", MessageBoxImage.Information);
                        return;
                    }

                    // 3. Crear el Universo Único de Moléculas (Unión Horizontal Completa)
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

                    // Ordenar por las brechas de magnitud más críticas
                    var listaOrdenada = listaResultado.OrderByDescending(b => b.BrechaValorAbsoluto).ToList();

                    // 4. Calcular KPIs de control macro-global
                    double simetriaGlobal = totalUnidadesGlobales > 0
                        ? (1.0 - (totalDiferenciaAbsoluta / totalUnidadesGlobales)) * 100.0
                        : 100.0;

                    var liderDesviacion = listaOrdenada.FirstOrDefault();

                    // 5. Inyectar de regreso a la UI
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ListaBrechas = new ObservableCollection<BrechaMoleculaModel>(listaOrdenada);

                        TotalBrechaUnidades = $"{totalDiferenciaAbsoluta:N0} UN";
                        PorcentajeSimetriaGlobal = $"{simetriaGlobal:F1}%";

                        MoleculaMayorDesviacion = liderDesviacion != null
                            ? $"{liderDesviacion.Molecula} ({liderDesviacion.BrechaAbsoluta:+0;-0;0} UN)"
                            : "Ninguna";
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

        // ==========================================
        // MOTOR DE AGRUPACIÓN SEGURO
        // ==========================================
        private Dictionary<string, double> AgruparBrechasSeguro(DataTable hoja, int colAtc, int colValorInicial)
        {
            var diccionario = new Dictionary<string, double>();
            int totalColumnas = hoja.Columns.Count;

            // Validar que las columnas detectadas existan físicamente sin importar el tamaño del Excel
            if (colAtc < 0 || colAtc >= totalColumnas) return diccionario;

            // Si el índice de valor existe, suma desde esa columna hasta el final. Si no, solo suma la última.
            int inicioValores = (colValorInicial >= 0 && colValorInicial < totalColumnas) ? colValorInicial : totalColumnas - 1;

            foreach (DataRow row in hoja.Rows)
            {
                // 1. Extraer molécula asegurando que lee la columna ATC que tu app ya detectó
                string moleculaCruda = row[colAtc]?.ToString().Trim().ToUpper() ?? "";
                if (string.IsNullOrEmpty(moleculaCruda)) continue;

                // Homologar a ATC III (4 caracteres)
                string molecula = moleculaCruda.Length > 4 ? moleculaCruda.Substring(0, 4) : moleculaCruda;

                // 2. Sumar los valores numéricos
                double sumaFila = 0;
                for (int i = inicioValores; i < totalColumnas; i++)
                {
                    sumaFila += ConvertirADouble(row[i]?.ToString());
                }

                // 3. Acumular en el diccionario
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

    // ==========================================
    // MODELO DE DATOS INTERNO PARA EL DATAGRID
    // ==========================================
    public class BrechaMoleculaModel
    {
        public string Molecula { get; set; }
        public double UnidadesInstitucional { get; set; }
        public double UnidadesComercial { get; set; }
        public double BrechaAbsoluta { get; set; } // Balance de signo neto
        public double BrechaValorAbsoluto { get; set; } // Magnitud real de la desviación
        public double IndiceSimetria { get; set; } // Ajuste de coincidencia (0% - 100%)

        // Formatos y tags rápidos consumibles nativamente por los DataGridBindings o DataTemplates
        public string AlertaEstado => IndiceSimetria >= 85 ? "✓ Simétrico" : IndiceSimetria >= 60 ? "⚠ Desviación Moderada" : "⚡ Brecha Crítica";
        public string ColorAlerta => IndiceSimetria >= 85 ? "#3DDC84" : IndiceSimetria >= 60 ? "#FFB703" : "#F72585";
    }
}