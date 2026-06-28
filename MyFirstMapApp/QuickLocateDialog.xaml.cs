using System.Collections.Generic;
using System.Windows;

namespace MyFirstMapApp
{
    public partial class QuickLocateDialog : Window
    {
        public CityLocation SelectedCity { get; set; }

        public QuickLocateDialog(List<CityLocation> cities)
        {
            InitializeComponent();
            lstCities.ItemsSource = cities;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            SelectedCity = lstCities.SelectedItem as CityLocation;
            if (SelectedCity != null)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("请选择一个城市", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}