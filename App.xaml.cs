using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace CalWidget
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 0 && e.Args[0] == "--gen-icon")
            {
                Helpers.IconGenerator.GenerateAppIcon("app.ico");
                Shutdown(0);
                return;
            }

            if (e.Args.Length > 0 && e.Args[0] == "--test")
            {
                Helpers.IconGenerator.GenerateAppIcon("app.ico");
                Tests.VerificationTests.RunAll();
                Shutdown(0);
                return;
            }

            // Дозволяємо вільному запуску вікна


            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString());
                }
                catch { }
                throw;
            }
        }
    }
}
