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
            SetSidebarWidth(220);
            if (this.DataContext is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += MainWindow_PropertyChanged;
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
            SetSidebarWidth(64);
        }

        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarLocked = !_sidebarLocked;
            if (_sidebarLocked)
            {
                if (_sidebarExpanded)
                    SetSidebarWidth(220);
                else
                    SetSidebarWidth(64);
            }
            else
            {
                BtnToggleText.Text = _sidebarExpanded ? "<" : ">";
            }
        }
    }
}