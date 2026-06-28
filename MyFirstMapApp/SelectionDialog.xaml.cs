using System.Collections.Generic;
using System.Windows;

namespace MyFirstMapApp
{
    public partial class SelectionDialog : Window
    {
        public string SelectedItem { get; private set; }

        public SelectionDialog(List<string> items, string title, string message)
        {
            InitializeComponent();
            Title = title;
            LblMessage.Text = message;
            LstItems.ItemsSource = items;
            if (items.Count > 0)
            {
                LstItems.SelectedIndex = 0;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (LstItems.SelectedItem != null)
            {
                SelectedItem = LstItems.SelectedItem.ToString();
                DialogResult = true;
            }
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
