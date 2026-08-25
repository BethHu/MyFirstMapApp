using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;

namespace MyFirstMapApp
{
    public class LayerStyleSettings
    {
        public System.Drawing.Color MarkerColor { get; set; } = System.Drawing.Color.Red;
        public double MarkerSize { get; set; } = 10;
        public SimpleMarkerSymbolStyle MarkerStyle { get; set; } = SimpleMarkerSymbolStyle.Circle;
        public System.Drawing.Color MarkerOutlineColor { get; set; } = System.Drawing.Color.Black;
        public double MarkerOutlineWidth { get; set; } = 1;

        public System.Drawing.Color LineColor { get; set; } = System.Drawing.Color.Blue;
        public double LineWidth { get; set; } = 2;
        public SimpleLineSymbolStyle LineStyle { get; set; } = SimpleLineSymbolStyle.Solid;

        public System.Drawing.Color FillColor { get; set; } = System.Drawing.Color.FromArgb(150, 100, 149, 237);
        public SimpleFillSymbolStyle FillStyle { get; set; } = SimpleFillSymbolStyle.Solid;
        public System.Drawing.Color OutlineColor { get; set; } = System.Drawing.Color.Black;
        public double OutlineWidth { get; set; } = 1;
        public SimpleLineSymbolStyle OutlineLineStyle { get; set; } = SimpleLineSymbolStyle.Solid;
    }

    public partial class LayerStyleDialog : Window
    {
        public LayerStyleSettings Settings { get; private set; }
        private GeometryType _geometryType;

        private static readonly SimpleMarkerSymbolStyle[] _markerStyleValues =
        {
            SimpleMarkerSymbolStyle.Circle,
            SimpleMarkerSymbolStyle.Square,
            SimpleMarkerSymbolStyle.Diamond,
            SimpleMarkerSymbolStyle.Cross,
            SimpleMarkerSymbolStyle.Triangle,
            SimpleMarkerSymbolStyle.X
        };

        private static readonly SimpleLineSymbolStyle[] _lineStyleValues =
        {
            SimpleLineSymbolStyle.Solid,
            SimpleLineSymbolStyle.Dash,
            SimpleLineSymbolStyle.Dot,
            SimpleLineSymbolStyle.DashDot
        };

        private static readonly SimpleFillSymbolStyle[] _fillStyleValues =
        {
            SimpleFillSymbolStyle.Solid,
            SimpleFillSymbolStyle.Null,
            SimpleFillSymbolStyle.ForwardDiagonal,
            SimpleFillSymbolStyle.BackwardDiagonal,
            SimpleFillSymbolStyle.Cross,
            SimpleFillSymbolStyle.DiagonalCross,
            SimpleFillSymbolStyle.Horizontal,
            SimpleFillSymbolStyle.Vertical
        };

        private static readonly SimpleLineSymbolStyle[] _outlineStyleValues =
        {
            SimpleLineSymbolStyle.Solid,
            SimpleLineSymbolStyle.Dash,
            SimpleLineSymbolStyle.Dot,
            SimpleLineSymbolStyle.DashDot,
            SimpleLineSymbolStyle.Null
        };

        public LayerStyleDialog(string layerName, GeometryType geometryType, LayerStyleSettings currentSettings)
        {
            InitializeComponent();
            _geometryType = geometryType;
            Settings = currentSettings ?? new LayerStyleSettings();
            txtLayerName.Text = $"图层: {layerName}";
            InitComboBoxes();
            SetupSections();
            LoadCurrentSettings();
            UpdateAllLabels();
        }

        private void InitComboBoxes()
        {
            cmbMarkerStyle.Items.Add("圆形 (Circle)");
            cmbMarkerStyle.Items.Add("方形 (Square)");
            cmbMarkerStyle.Items.Add("菱形 (Diamond)");
            cmbMarkerStyle.Items.Add("十字 (Cross)");
            cmbMarkerStyle.Items.Add("三角形 (Triangle)");
            cmbMarkerStyle.Items.Add("X 形 (X)");

            cmbLineStyle.Items.Add("实线 (Solid)");
            cmbLineStyle.Items.Add("虚线 (Dash)");
            cmbLineStyle.Items.Add("点线 (Dot)");
            cmbLineStyle.Items.Add("点划线 (DashDot)");

            cmbFillStyle.Items.Add("实心 (Solid)");
            cmbFillStyle.Items.Add("无填充 (Null)");
            cmbFillStyle.Items.Add("斜线 / (ForwardDiagonal)");
            cmbFillStyle.Items.Add("斜线 \\ (BackwardDiagonal)");
            cmbFillStyle.Items.Add("网格线 (Cross)");
            cmbFillStyle.Items.Add("斜网格 (DiagonalCross)");
            cmbFillStyle.Items.Add("横线 (Horizontal)");
            cmbFillStyle.Items.Add("竖线 (Vertical)");

            cmbOutlineStyle.Items.Add("实线 (Solid)");
            cmbOutlineStyle.Items.Add("虚线 (Dash)");
            cmbOutlineStyle.Items.Add("点线 (Dot)");
            cmbOutlineStyle.Items.Add("点划线 (DashDot)");
            cmbOutlineStyle.Items.Add("无轮廓 (Null)");
        }

        private void SetupSections()
        {
            txtGeoType.Text = _geometryType.ToString();
            pointSection.Visibility = (_geometryType == GeometryType.Point || _geometryType == GeometryType.Multipoint) ? Visibility.Visible : Visibility.Collapsed;
            lineSection.Visibility = (_geometryType == GeometryType.Polyline) ? Visibility.Visible : Visibility.Collapsed;
            polygonSection.Visibility = (_geometryType == GeometryType.Polygon) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadCurrentSettings()
        {
            switch (_geometryType)
            {
                case GeometryType.Point:
                case GeometryType.Multipoint:
                    cmbMarkerStyle.SelectedIndex = IndexOfValue(_markerStyleValues, Settings.MarkerStyle);
                    sldMarkerSize.Value = Settings.MarkerSize;
                    colorMarker.Background = new SolidColorBrush(Color.FromArgb(Settings.MarkerColor.A, Settings.MarkerColor.R, Settings.MarkerColor.G, Settings.MarkerColor.B));
                    colorMarkerOutline.Background = new SolidColorBrush(Color.FromArgb(Settings.MarkerOutlineColor.A, Settings.MarkerOutlineColor.R, Settings.MarkerOutlineColor.G, Settings.MarkerOutlineColor.B));
                    sldMarkerOutlineWidth.Value = Settings.MarkerOutlineWidth;
                    break;

                case GeometryType.Polyline:
                    cmbLineStyle.SelectedIndex = IndexOfValue(_lineStyleValues, Settings.LineStyle);
                    sldLineWidth.Value = Settings.LineWidth;
                    colorLine.Background = new SolidColorBrush(Color.FromArgb(Settings.LineColor.A, Settings.LineColor.R, Settings.LineColor.G, Settings.LineColor.B));
                    break;

                case GeometryType.Polygon:
                    cmbFillStyle.SelectedIndex = IndexOfValue(_fillStyleValues, Settings.FillStyle);
                    colorFill.Background = new SolidColorBrush(Color.FromArgb(Settings.FillColor.A, Settings.FillColor.R, Settings.FillColor.G, Settings.FillColor.B));
                    cmbOutlineStyle.SelectedIndex = IndexOfValue(_outlineStyleValues, Settings.OutlineLineStyle);
                    sldOutlineWidth.Value = Settings.OutlineWidth;
                    colorOutline.Background = new SolidColorBrush(Color.FromArgb(Settings.OutlineColor.A, Settings.OutlineColor.R, Settings.OutlineColor.G, Settings.OutlineColor.B));
                    break;
            }
        }

        private void SaveSettings()
        {
            switch (_geometryType)
            {
                case GeometryType.Point:
                case GeometryType.Multipoint:
                    if (cmbMarkerStyle.SelectedIndex >= 0 && cmbMarkerStyle.SelectedIndex < _markerStyleValues.Length)
                        Settings.MarkerStyle = _markerStyleValues[cmbMarkerStyle.SelectedIndex];
                    Settings.MarkerSize = sldMarkerSize.Value;
                    var mc = ((SolidColorBrush)colorMarker.Background).Color;
                    Settings.MarkerColor = System.Drawing.Color.FromArgb(mc.A, mc.R, mc.G, mc.B);
                    var moc = ((SolidColorBrush)colorMarkerOutline.Background).Color;
                    Settings.MarkerOutlineColor = System.Drawing.Color.FromArgb(moc.A, moc.R, moc.G, moc.B);
                    Settings.MarkerOutlineWidth = sldMarkerOutlineWidth.Value;
                    break;

                case GeometryType.Polyline:
                    if (cmbLineStyle.SelectedIndex >= 0 && cmbLineStyle.SelectedIndex < _lineStyleValues.Length)
                        Settings.LineStyle = _lineStyleValues[cmbLineStyle.SelectedIndex];
                    Settings.LineWidth = sldLineWidth.Value;
                    var lc = ((SolidColorBrush)colorLine.Background).Color;
                    Settings.LineColor = System.Drawing.Color.FromArgb(lc.A, lc.R, lc.G, lc.B);
                    break;

                case GeometryType.Polygon:
                    if (cmbFillStyle.SelectedIndex >= 0 && cmbFillStyle.SelectedIndex < _fillStyleValues.Length)
                        Settings.FillStyle = _fillStyleValues[cmbFillStyle.SelectedIndex];
                    var fc = ((SolidColorBrush)colorFill.Background).Color;
                    Settings.FillColor = System.Drawing.Color.FromArgb(fc.A, fc.R, fc.G, fc.B);
                    if (cmbOutlineStyle.SelectedIndex >= 0 && cmbOutlineStyle.SelectedIndex < _outlineStyleValues.Length)
                        Settings.OutlineLineStyle = _outlineStyleValues[cmbOutlineStyle.SelectedIndex];
                    Settings.OutlineWidth = sldOutlineWidth.Value;
                    var oc = ((SolidColorBrush)colorOutline.Background).Color;
                    Settings.OutlineColor = System.Drawing.Color.FromArgb(oc.A, oc.R, oc.G, oc.B);
                    break;
            }
        }

        private static int IndexOfValue<T>(T[] array, T value)
        {
            for (int i = 0; i < array.Length; i++)
                if (array[i].Equals(value)) return i;
            return 0;
        }

        private void UpdateAllLabels()
        {
            if (txtMarkerSize != null) txtMarkerSize.Text = ((int)sldMarkerSize.Value).ToString();
            if (txtMarkerOutlineWidth != null) txtMarkerOutlineWidth.Text = sldMarkerOutlineWidth.Value.ToString("F1");
            if (txtLineWidth != null) txtLineWidth.Text = sldLineWidth.Value.ToString("F1");
            if (txtOutlineWidth != null) txtOutlineWidth.Text = sldOutlineWidth.Value.ToString("F1");
        }

        private void Sld_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateAllLabels();
        }

        private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is string tag)
            {
                var bg = ((SolidColorBrush)border.Background).Color;
                var dialog = new ColorPickerDialog(bg);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                    border.Background = new SolidColorBrush(dialog.SelectedColor);
            }
        }

        private void ColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is string tag)
            {
                Border targetBorder = null;
                if (tag == "marker") targetBorder = colorMarker;
                else if (tag == "markerOutline") targetBorder = colorMarkerOutline;
                else if (tag == "line") targetBorder = colorLine;
                else if (tag == "fill") targetBorder = colorFill;
                else if (tag == "outline") targetBorder = colorOutline;

                if (targetBorder != null)
                {
                    var bg = ((SolidColorBrush)targetBorder.Background).Color;
                    var dialog = new ColorPickerDialog(bg);
                    dialog.Owner = this;
                    if (dialog.ShowDialog() == true)
                        targetBorder.Background = new SolidColorBrush(dialog.SelectedColor);
                }
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
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
