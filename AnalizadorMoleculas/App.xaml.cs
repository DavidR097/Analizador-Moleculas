using System.Globalization;
using System.Threading;
using System.Windows;
using OfficeOpenXml;

namespace AnalizadorMoleculas
{
    public partial class App : Application
    {
        public App()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Analizador Moleculas"); 
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var cultura = new CultureInfo("es-CO");
            Thread.CurrentThread.CurrentCulture = cultura;
            Thread.CurrentThread.CurrentUICulture = cultura;
            
            LiveChartsCore.LiveCharts.Configure(config =>
                config
                    .WithLegendTextPaint(new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(
                        new SkiaSharp.SKColor(226, 232, 240)))
                    .WithLegendBackgroundPaint(new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(
                        new SkiaSharp.SKColor(30, 41, 55)))
            );
        }
    }
}