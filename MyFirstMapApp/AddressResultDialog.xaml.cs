using Esri.ArcGISRuntime.Tasks.Geocoding;
using System.Collections.Generic;
using System.Windows;

namespace MyFirstMapApp
{
    public partial class AddressResultDialog : Window
    {
        public GeocodeResult SelectedResult { get; private set; }

        public AddressResultDialog(IReadOnlyList<GeocodeResult> results)
        {
            InitializeComponent();
            lstResults.ItemsSource = results;
        }

        private void LstResults_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SelectedResult = lstResults.SelectedItem as GeocodeResult;
            btnOK.IsEnabled = SelectedResult != null;
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedResult != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}