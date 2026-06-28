using System;
using System.Windows;

namespace MyFirstMapApp
{
    public partial class CoordinateInputDialog : Window
    {
        public double Longitude { get; private set; }
        public double Latitude { get; private set; }
        public bool IsValid { get; private set; }

        public CoordinateInputDialog()
        {
            InitializeComponent();
            IsValid = false;
        }

        public CoordinateInputDialog(double defaultLon, double defaultLat) : this()
        {
            // Handle sign based on hemisphere
            if (defaultLat >= 0)
            {
                txtLatitude.Text = Math.Abs(defaultLat).ToString("F6");
                rbNorth.IsChecked = true;
            }
            else
            {
                txtLatitude.Text = Math.Abs(defaultLat).ToString("F6");
                rbSouth.IsChecked = true;
            }

            if (defaultLon >= 0)
            {
                txtLongitude.Text = Math.Abs(defaultLon).ToString("F6");
                rbEast.IsChecked = true;
            }
            else
            {
                txtLongitude.Text = Math.Abs(defaultLon).ToString("F6");
                rbWest.IsChecked = true;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(txtLatitude.Text, out double lat) && 
                double.TryParse(txtLongitude.Text, out double lon))
            {
                // Apply hemisphere sign
                Latitude = rbSouth.IsChecked == true ? -lat : lat;
                Longitude = rbWest.IsChecked == true ? -lon : lon;

                // Validate ranges
                if (Latitude < -90 || Latitude > 90)
                {
                    MessageBox.Show("纬度值必须在 -90 到 90 之间", "无效输入", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (Longitude < -180 || Longitude > 180)
                {
                    MessageBox.Show("经度值必须在 -180 到 180 之间", "无效输入", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsValid = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("请输入有效的数字坐标值", "无效输入", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsValid = false;
            DialogResult = false;
            Close();
        }

    }
}