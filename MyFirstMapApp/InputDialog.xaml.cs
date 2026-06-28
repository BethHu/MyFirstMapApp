using System.Windows;

namespace MyFirstMapApp
{
    public partial class InputDialog : Window
    {
        public string Answer => txtInput.Text;
        public string Message { get; set; }

        public InputDialog(string message, string defaultValue = "")
        {
            InitializeComponent();
            Message = message;
            DataContext = this;
            txtInput.Text = defaultValue;
            txtInput.Focus();
            txtInput.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}