using AnalizadorMoleculas.Models;
using AnalizadorMoleculas.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace AnalizadorMoleculas.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private readonly ExcelService _excelService = new ExcelService();
        private readonly CultureInfo _culturaColombia = new CultureInfo("es-CO");

        private string _vistaActual = "hub";
        public string VistaActual
        {
            get => _vistaActual;
            set { _vistaActual = value; OnPropertyChanged(nameof(VistaActual)); }
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

        public ICommand CargarCommand { get; private set; }
        public ICommand GenerarComparacionCommand { get; private set; }
        public ICommand ExportarCommand { get; private set; }
        public ICommand GenerarInformeCommand { get; private set; }
        public ICommand CalcularEstadisticasCommand { get; private set; }
        public ICommand ExportarEstadisticasCommand { get; private set; }
        public ICommand NavigateCommand { get; private set; }

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

        private string ObtenerPrefijoProducto(string producto)
        {
            string limpio = NormalizarTextoAbsoluto(producto);
            var partes = limpio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return partes.Length > 0 ? partes[0] : "SIN_DATO";
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

        private static int DetectarIndiceProducto(DataTable hoja)
        {
            for (int i = 0; i < hoja.Columns.Count; i++)
            {
                string n = hoja.Columns[i].ColumnName.ToUpper();
                if (n.Contains("PRODUCT") || n.Contains("PRODUCTO")) return i;
            }
            return Math.Min(2, hoja.Columns.Count - 1);
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
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
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