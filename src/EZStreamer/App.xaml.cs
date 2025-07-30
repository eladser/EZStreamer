using System.Windows;

namespace EZStreamer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Set up global exception handling
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, 
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"An unexpected error occurred: {e.Exception.Message}\n\nPlease restart the application.",
                "EZStreamer Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            e.Handled = true;
        }
    }
}
