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
            
            // Set up proper shutdown handling
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Force cleanup of any remaining WebView2 processes
            try
            {
                // This helps clean up WebView2 processes that might be lingering
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
            }
            catch
            {
                // Ignore any cleanup errors during shutdown
            }

            base.OnExit(e);
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
            
            // If there's a critical error, force shutdown
            if (e.Exception is System.OutOfMemoryException || 
                e.Exception is System.StackOverflowException)
            {
                this.Shutdown(1);
            }
        }
    }
}
