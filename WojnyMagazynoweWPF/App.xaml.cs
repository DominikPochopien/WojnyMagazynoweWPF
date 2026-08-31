using System.Windows;

namespace WojnyMagazynoweWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Uruchamia pierwsze okno
            MainWindow window1 = new MainWindow();
            window1.Show();

            // Uruchamia drugie okno
            MainWindow window2 = new MainWindow();
            window2.Show();
        }
    }
}