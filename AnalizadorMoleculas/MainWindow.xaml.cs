using System.ComponentModel;
using System.Windows;
using AnalizadorMoleculas.Views;
using mah = MahApps.Metro.Controls;

namespace AnalizadorMoleculas
{
    public partial class MainWindow : mah.MetroWindow
    {
        private bool _sidebarLocked = false;
        private bool _sidebarExpanded = true;

        public MainWindow()
        {
            InitializeComponent();
            // asegurar estado inicial
            SetSidebarWidth(220);
            // suscribirse a los cambios de VistaActual en el ViewModel para actualizar contenido
            if (this.DataContext is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += MainWindow_PropertyChanged;
                // inicializar contenido según estado actual
                UpdateContent(((ViewModels.MainViewModel)this.DataContext).VistaActual);
            }
        }

        private void SetSidebarWidth(double width)
        {
            var col = RootGrid.ColumnDefinitions[0];
            col.Width = new GridLength(width);
            _sidebarExpanded = width > 100;
            BtnToggleText.Text = _sidebarExpanded ? "<" : ">";
        }

        private void MainWindow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "VistaActual")
            {
                var vm = this.DataContext as ViewModels.MainViewModel;
                if (vm != null)
                {
                    // actualizar en UI thread
                    Dispatcher.Invoke(() => UpdateContent(vm.VistaActual));
                }
            }
        }

        private void UpdateContent(string vista)
        {
            if (ContentHost == null) return;
            switch ((vista ?? "hub").ToLowerInvariant())
            {
                case "hub": ContentHost.Content = new HubView(); break;
                case "espejo": ContentHost.Content = new EspejoView(); break;
                case "archivos": ContentHost.Content = new ArchivosView(); break;
                case "metricas": ContentHost.Content = new MetricasView(); break;
                case "molecula": ContentHost.Content = new MoleculaView(); break;
                case "brechas": ContentHost.Content = new BrechasView(); break;
                case "sqlite": ContentHost.Content = new SqliteView(); break;
                default: ContentHost.Content = new HubView(); break;
            }
        }

        private void Sidebar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_sidebarLocked) return;
            SetSidebarWidth(220);
        }

        private void Sidebar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_sidebarLocked) return;
            // sólo colapsar si no hay interacción dentro del sidebar
            SetSidebarWidth(64);
        }

        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarLocked = !_sidebarLocked;
            if (_sidebarLocked)
            {
                // fijar en el estado actual (si está expandido, mantener expandido; si colapsado, mantener colapsado)
                if (_sidebarExpanded)
                    SetSidebarWidth(220);
                else
                    SetSidebarWidth(64);
            }
            else
            {
                // al soltar, mantener el ancho actual pero permitir hover para cambiarlo
                // invertir el icono para reflejar acción posible
                BtnToggleText.Text = _sidebarExpanded ? "<" : ">";
            }
        }
    }
}