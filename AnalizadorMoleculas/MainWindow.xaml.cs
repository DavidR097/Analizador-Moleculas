using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using System;
using System.Linq;
using System.Windows;
using mah = MahApps.Metro.Controls;

namespace AnalizadorMoleculas
{
    public partial class MainWindow : mah.MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigurarEjesDelGrafico();
        }

        private void ConfigurarEjesDelGrafico()
        {
            if (this.DataContext is not AnalizadorMoleculas.ViewModels.MainViewModel vm) return;

            CartesianChart.YAxes = new[]
            {
                new Axis
                {
                    Labeler = (value) => value.ToString("N2", new System.Globalization.CultureInfo("es-CO"))
                }
            };

            CartesianChart.XAxes = new[]
            {
                new Axis
                {
                    IsVisible = false
                }
            };
        }

        public Axis[] MolYAxes { get; set; }

        public void InicializarMolecula()
        {
            MolYAxes = new Axis[]
            {
        new Axis
        {
            Labeler = val => val.ToString("N0")
        }
            };
        }
    }
}