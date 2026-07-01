using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AnalizadorMoleculas.Models;
using AnalizadorMoleculas.Services;

namespace AnalizadorMoleculas.ViewModels
{
    public class SqliteViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService = new DatabaseService();

        private ObservableCollection<AuditoriaHistorial> _historialAuditorias = new();
        public ObservableCollection<AuditoriaHistorial> HistorialAuditorias
        {
            get => _historialAuditorias;
            set
            {
                _historialAuditorias = value;
                OnPropertyChanged(nameof(HistorialAuditorias));
            }
        }

        public ICommand CargarHistorialSqliteCommand { get; }

        public SqliteViewModel()
        {
            CargarHistorialSqliteCommand = new RelayCommand(async () => await CargarHistorialAsync());
            _ = CargarHistorialAsync();
        }

        private async Task CargarHistorialAsync()
        {
            try
            {
                var lista = await _databaseService.ObtenerHistorialAsync();
                HistorialAuditorias.Clear();
                foreach (var item in lista)
                {
                    HistorialAuditorias.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar historial:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}