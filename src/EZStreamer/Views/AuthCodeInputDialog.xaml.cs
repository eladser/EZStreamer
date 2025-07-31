using System.Windows;

namespace EZStreamer.Views
{
    /// <summary>
    /// Interaction logic for AuthCodeInputDialog.xaml
    /// </summary>
    public partial class AuthCodeInputDialog : Window
    {
        public string AuthorizationCode { get; private set; }

        public AuthCodeInputDialog(string prefilledCode = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(prefilledCode))
            {
                CodeTextBox.Text = prefilledCode;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            AuthorizationCode = CodeTextBox.Text.Trim();
            if (string.IsNullOrEmpty(AuthorizationCode))
            {
                MessageBox.Show("Please enter the authorization code.", "Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
