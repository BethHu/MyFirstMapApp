using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MyFirstMapApp
{
    public partial class ColorPickerDialog : Window
    {
        public Color SelectedColor { get; private set; }

        public ColorPickerDialog(Color initialColor)
        {
            InitializeComponent();

            // Initialize color
            SelectedColor = initialColor;
            UpdatePreview();

            // Set slider values
            sliderR.Value = initialColor.R;
            sliderG.Value = initialColor.G;
            sliderB.Value = initialColor.B;
            sliderA.Value = initialColor.A;

            UpdateRGBText();
        }

        private void UpdatePreview()
        {
            ColorPreview.Background = new SolidColorBrush(SelectedColor);
        }

        private void UpdateRGBText()
        {
            txtR.Text = ((int)sliderR.Value).ToString();
            txtG.Text = ((int)sliderG.Value).ToString();
            txtB.Text = ((int)sliderB.Value).ToString();
            txtA.Text = ((int)sliderA.Value).ToString();

            // Update HEX
            byte r = (byte)sliderR.Value;
            byte g = (byte)sliderG.Value;
            byte b = (byte)sliderB.Value;
            byte a = (byte)sliderA.Value;
            txtHex.Text = $"#{a:X2}{r:X2}{g:X2}{b:X2}";

            // Update selected color
            SelectedColor = Color.FromArgb(a, r, g, b);
            UpdatePreview();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateRGBText();
        }

        private void TxtHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var hex = txtHex.Text.TrimStart('#');
                if (hex.Length == 6 || hex.Length == 8)
                {
                    byte a = hex.Length == 8 ? Convert.ToByte(hex.Substring(0, 2), 16) : (byte)255;
                    byte r = Convert.ToByte(hex.Substring(hex.Length - 6, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(hex.Length - 4, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(hex.Length - 2, 2), 16);

                    sliderA.Value = a;
                    sliderR.Value = r;
                    sliderG.Value = g;
                    sliderB.Value = b;

                    UpdateRGBText();
                }
            }
            catch { /* Ignore invalid input */ }
        }

        private void PresetColor_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is string hex)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    if (color != null)
                    {
                        sliderR.Value = color.R;
                        sliderG.Value = color.G;
                        sliderB.Value = color.B;
                        sliderA.Value = color.A;
                        UpdateRGBText();
                    }
                }
                catch { }
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}