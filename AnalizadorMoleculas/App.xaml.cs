using OfficeOpenXml;
using System.Globalization;
using System.Threading;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace AnalizadorMoleculas
{
    public partial class App : Application
    {
        public App()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Analizador Moleculas");
        }

        protected override async void OnStartup(StartupEventArgs e)
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

            try
            {
                VelopackApp.Build().Run();

                var updateManager = new UpdateManager(new GithubSource(
                    repoUrl: "https://github.com/DavidR097/Analizador-Moleculas",
                    accessToken: "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",
                    prerelease: false));

                var updateInfo = await updateManager.CheckForUpdatesAsync();

                if (updateInfo != null && updateInfo.TargetFullRelease != null)
                {
                    var result = MessageBox.Show(
                        $"Hay una nueva versión disponible.\n¿Quieres actualizar ahora?",
                        "Actualización disponible",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        await updateManager.DownloadUpdatesAsync(updateInfo);
                        updateManager.ApplyUpdatesAndRestart(updateInfo);
                    }
                }
            }
            catch
            {
                // Ignorar en desarrollo
            }
        }
    }
}