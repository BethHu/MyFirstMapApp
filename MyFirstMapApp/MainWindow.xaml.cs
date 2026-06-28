using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;
using Esri.ArcGISRuntime.Portal;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyFirstMapApp
{
    public class PropertyDisplay
    {
        public string FieldName { get; set; }
        public string Value { get; set; }
    }

    public class LayerItem : INotifyPropertyChanged
    {
        private System.Drawing.Color _layerColor;
        public string Name { get; set; }
        public Layer Layer { get; set; }

        public System.Drawing.Color LayerColor
        {
            get => _layerColor;
            set { _layerColor = value; OnPropertyChanged(nameof(LayerColor)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<LayerItem> _layerItems = new ObservableCollection<LayerItem>();
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private LayerItem _draggedItem = null;

        private GraphicsOverlay _drawingOverlay;
        private bool _isMeasuring = false;
        private bool _isCollectingPoints = false;
        private string _collectingFor = "";
        private List<MapPoint> _collectedPoints = new List<MapPoint>();
        private LocatorTask _locatorTask;

        private GraphicsOverlay _routeOverlay;
        private RouteTask _routeTask;
        private MapPoint _startPoint;
        private MapPoint _endPoint;
        private bool _isSelectingStart = false;
        private bool _isSelectingEnd = false;

        private bool _is3DMode = false;
        private Scene _scene;
        private ElevationSource _elevationSource;
        private SurfacePlacement _currentSurfacePlacement = SurfacePlacement.DrapedBillboarded;

        public MainWindow()
        {
            InitializeComponent();
            if (string.IsNullOrEmpty(ArcGISRuntimeEnvironment.ApiKey))
                MessageBox.Show("警告：API Key 未设置！请检查 App.xaml.cs", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);

            lstLayers.ItemsSource = _layerItems;
            InitializeMap();
            InitializeDrawingOverlay();
            UpdateStatus("就绪");
        }

        private void InitializeMap()
        {
            try
            {
                // 检查 Key 是否已设置
                if (string.IsNullOrEmpty(ArcGISRuntimeEnvironment.ApiKey))
                {
                    MessageBox.Show("API Key 未设置！请检查 App.xaml.cs。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 创建地图时，如果 Key 无效，会抛出异常
                MyMapView.Map = new Map(new Basemap(BasemapStyle.ArcGISStreets));
            }
            catch (Exception ex)
            {
                // 显示完整的错误信息
                string msg = $"初始化地图失败: {ex.Message}";
                if (ex.InnerException != null)
                    msg += $"\n内部错误: {ex.InnerException.Message}";
                MessageBox.Show(msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeDrawingOverlay()
        {
            _drawingOverlay = new GraphicsOverlay();
            MyMapView.GraphicsOverlays.Add(_drawingOverlay);
            _routeOverlay = new GraphicsOverlay();
            MyMapView.GraphicsOverlays.Add(_routeOverlay);
        }

        // ==================== 绘图功能 ====================
        private void AddPoint_Click(object sender, RoutedEventArgs e)
        { CancelDrawing_Click(sender, e); _isCollectingPoints = true; _collectingFor = "Point"; _collectedPoints.Clear(); UpdateStatus("添加点: 请点击地图添加点"); }

        private void DrawLine_Click(object sender, RoutedEventArgs e)
        { CancelDrawing_Click(sender, e); _isCollectingPoints = true; _collectingFor = "Line"; _collectedPoints.Clear(); UpdateStatus("绘制线: 请点击地图添加折点"); }

        private void DrawPolygon_Click(object sender, RoutedEventArgs e)
        { CancelDrawing_Click(sender, e); _isCollectingPoints = true; _collectingFor = "Polygon"; _collectedPoints.Clear(); UpdateStatus("绘制面: 请点击地图添加折点"); }

        private void MeasureDistance_Click(object sender, RoutedEventArgs e)
        { CancelDrawing_Click(sender, e); _isCollectingPoints = true; _isMeasuring = true; _collectingFor = "Measure"; _collectedPoints.Clear(); UpdateStatus("距离测量: 请点击地图添加折点"); }

        private void MeasureArea_Click(object sender, RoutedEventArgs e)
        { CancelDrawing_Click(sender, e); _isCollectingPoints = true; _isMeasuring = true; _collectingFor = "Area"; _collectedPoints.Clear(); UpdateStatus("面积测量: 请点击地图添加折点"); }

        private void CompleteDrawing_Click(object sender, RoutedEventArgs e) => CompleteDrawing();

        private void CompleteDrawing()
        {
            if (!_isCollectingPoints) { UpdateStatus("当前没有进行中的绘制"); ClearStatusAfterDelay(); return; }
            if (_collectingFor == "Point") { _isCollectingPoints = false; _collectingFor = ""; _collectedPoints.Clear(); UpdateStatus("点添加完成"); ClearStatusAfterDelay(); return; }
            if (_collectedPoints.Count < 2) { UpdateStatus("至少需要2个点"); ClearStatusAfterDelay(); return; }

            try
            {
                ClearTemporaryGraphics();
                if (_collectingFor == "Line" || _collectingFor == "Measure")
                {
                    var lineGeometry = new Polyline(_collectedPoints.ToArray());
                    var lineSymbol = _collectingFor == "Measure" ? new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Red, 3) : new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Blue, 3);
                    var lineGraphic = new Graphic(lineGeometry, lineSymbol);
                    if (!_isMeasuring) _drawingOverlay.Graphics.Add(lineGraphic);
                    var length = GeometryEngine.Length(lineGeometry);
                    string unit = "米"; double displayLength = length;
                    if (length >= 1000) { displayLength = length / 1000; unit = "千米"; }
                    if (_isMeasuring)
                    {
                        var midPoint = new MapPoint((_collectedPoints[0].X + _collectedPoints[_collectedPoints.Count - 1].X) / 2, (_collectedPoints[0].Y + _collectedPoints[_collectedPoints.Count - 1].Y) / 2, _collectedPoints[0].SpatialReference);
                        var textSymbol = new TextSymbol { Text = $"{displayLength:F2} {unit}", Color = System.Drawing.Color.Red, Size = 14, HaloColor = System.Drawing.Color.White, HaloWidth = 2 };
                        _drawingOverlay.Graphics.Add(new Graphic(midPoint, textSymbol));
                        _drawingOverlay.Graphics.Add(lineGraphic);
                    }
                    UpdateStatus($"测量结果: {displayLength:F2} {unit}");
                }
                else if (_collectingFor == "Polygon" || _collectingFor == "Area")
                {
                    _collectedPoints.Add(_collectedPoints[0]);
                    var polygonGeometry = new Polygon(_collectedPoints.ToArray());
                    var fillSymbol = _collectingFor == "Area" ? new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, System.Drawing.Color.FromArgb(50, 255, 192, 192), new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Red, 2)) : new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, System.Drawing.Color.FromArgb(50, 100, 149, 237), new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Blue, 2));
                    var polygonGraphic = new Graphic(polygonGeometry, fillSymbol);
                    if (!_isMeasuring) _drawingOverlay.Graphics.Add(polygonGraphic);
                    var area = GeometryEngine.Area(polygonGeometry);
                    string unit = "平方米"; double displayArea = area;
                    if (area >= 10000) { displayArea = area / 10000; unit = "公顷"; }
                    if (area >= 1000000) { displayArea = area / 1000000; unit = "平方公里"; }
                    if (_isMeasuring)
                    {
                        var envelope = polygonGeometry.Extent;
                        var centroid = new MapPoint((envelope.XMin + envelope.XMax) / 2, (envelope.YMin + envelope.YMax) / 2, envelope.SpatialReference);
                        var textSymbol = new TextSymbol { Text = $"{displayArea:F2} {unit}", Color = System.Drawing.Color.Red, Size = 14, HaloColor = System.Drawing.Color.White, HaloWidth = 2 };
                        _drawingOverlay.Graphics.Add(new Graphic(centroid, textSymbol));
                        _drawingOverlay.Graphics.Add(polygonGraphic);
                    }
                    UpdateStatus($"面积结果: {displayArea:F2} {unit}");
                }
                _isCollectingPoints = false; _isMeasuring = false; _collectingFor = ""; _collectedPoints.Clear(); ClearStatusAfterDelay();
            }
            catch (Exception ex) { UpdateStatus($"绘制错误: {ex.Message}"); ClearStatusAfterDelay(); }
        }

        private void CancelDrawing_Click(object sender, RoutedEventArgs e)
        { ClearTemporaryGraphics(); _isCollectingPoints = false; _isMeasuring = false; _collectingFor = ""; _collectedPoints.Clear(); UpdateStatus("绘制已取消"); ClearStatusAfterDelay(); }

        private void ClearTemporaryGraphics()
        { var tempGraphics = _drawingOverlay.Graphics.Where(g => g.Attributes.ContainsKey("_temp")).ToList(); foreach (var g in tempGraphics) _drawingOverlay.Graphics.Remove(g); }

        private void ClearGraphics_Click(object sender, RoutedEventArgs e)
        { _drawingOverlay.Graphics.Clear(); _isCollectingPoints = false; _isMeasuring = false; _collectingFor = ""; _collectedPoints.Clear(); UpdateStatus("所有图形已清除"); ClearStatusAfterDelay(); }

        // ==================== 导出GeoJSON ====================
        private void ExportShapefile_Click(object sender, RoutedEventArgs e)
        {
            if (_drawingOverlay.Graphics.Count == 0) { UpdateStatus("没有可导出的图形"); ClearStatusAfterDelay(); return; }
            try
            {
                var saveDialog = new SaveFileDialog { Filter = "GeoJSON File|*.geojson|JSON File|*.json", Title = "Export Graphics", FileName = "ExportedGraphics" };
                if (saveDialog.ShowDialog() != true) return;
                var geoJsonContent = ExportToGeoJSON();
                System.IO.File.WriteAllText(saveDialog.FileName, geoJsonContent);
                UpdateStatus($"导出成功: {saveDialog.FileName}"); ClearStatusAfterDelay(5000);
            }
            catch (Exception ex) { UpdateStatus($"导出失败: {ex.Message}"); ClearStatusAfterDelay(); }
        }

        private string ExportToGeoJSON()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{\"type\":\"FeatureCollection\",\"features\":[");
            int id = 1, count = 0;
            var graphicsToExport = _drawingOverlay.Graphics.Where(g => !g.Attributes.ContainsKey("_temp") && g.Geometry != null).ToList();
            foreach (var graphic in graphicsToExport)
            {
                if (count > 0) sb.AppendLine(",");
                count++;
                var geometry = graphic.Geometry;
                var wgs84Geometry = ProjectToWGS84(geometry);
                var coordinates = GetGeoJSONCoordinatesString(wgs84Geometry);
                var geoJsonType = GetGeoJSONGeometryType(wgs84Geometry.GeometryType);
                sb.AppendLine("{\"type\":\"Feature\",\"properties\":{\"id\":" + id + ",\"geometryType\":\"" + geometry.GeometryType + "\"},");
                sb.AppendLine("\"geometry\":{\"type\":\"" + geoJsonType + "\",\"coordinates\":" + coordinates + "}}");
                id++;
            }
            sb.AppendLine("]}");
            return sb.ToString();
        }

        private string GetGeoJSONGeometryType(GeometryType geometryType)
        { switch (geometryType) { case GeometryType.Point: return "Point"; case GeometryType.Polyline: return "LineString"; case GeometryType.Polygon: return "Polygon"; case GeometryType.Multipoint: return "MultiPoint"; default: return "Unknown"; } }

        private string GetGeoJSONCoordinatesString(Esri.ArcGISRuntime.Geometry.Geometry geometry)
        {
            if (geometry is MapPoint point) return $"[{point.X},{point.Y}]";
            else if (geometry is Polyline polyline)
            {
                var sb = new System.Text.StringBuilder(); sb.Append("[");
                var points = polyline.Parts.First().Points.ToList();
                for (int i = 0; i < points.Count; i++) { if (i > 0) sb.Append(","); sb.Append($"[{points[i].X},{points[i].Y}]"); }
                sb.Append("]"); return sb.ToString();
            }
            else if (geometry is Polygon polygon)
            {
                var sb = new System.Text.StringBuilder(); sb.Append("[");
                var parts = polygon.Parts.ToList();
                for (int i = 0; i < parts.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append("[");
                    var points = parts[i].Points.ToList();
                    for (int j = 0; j < points.Count; j++) { if (j > 0) sb.Append(","); sb.Append($"[{points[j].X},{points[j].Y}]"); }
                    sb.Append("]");
                }
                sb.Append("]"); return sb.ToString();
            }
            return "[]";
        }

        private Esri.ArcGISRuntime.Geometry.Geometry ProjectToWGS84(Esri.ArcGISRuntime.Geometry.Geometry geometry)
        {
            if (geometry == null) return null;
            try { if (geometry.SpatialReference != null && geometry.SpatialReference.Equals(SpatialReferences.Wgs84)) return geometry; return GeometryEngine.Project(geometry, SpatialReferences.Wgs84); }
            catch { return geometry; }
        }

        // ==================== 坐标输入 ====================
        private async void InputCoordinate_Click(object sender, RoutedEventArgs e)
        {
            double defaultLon = 0, defaultLat = 0;
            var vp = MyMapView.GetCurrentViewpoint(ViewpointType.CenterAndScale);
            if (vp != null && vp.TargetGeometry is MapPoint center)
            {
                var wgs84Point = GeometryEngine.Project(center, SpatialReferences.Wgs84) as MapPoint;
                if (wgs84Point != null) { defaultLon = wgs84Point.X; defaultLat = wgs84Point.Y; }
            }
            var dialog = new CoordinateInputDialog(defaultLon, defaultLat);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.IsValid)
            {
                var mapPoint = new MapPoint(dialog.Longitude, dialog.Latitude, SpatialReferences.Wgs84);
                var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 12);
                _drawingOverlay.Graphics.Add(new Graphic(mapPoint, markerSymbol));
                await MyMapView.SetViewpointCenterAsync(mapPoint, 10000);
                string latDir = dialog.Latitude >= 0 ? "N" : "S";
                string lonDir = dialog.Longitude >= 0 ? "E" : "W";
                UpdateStatus($"坐标点添加 {Math.Abs(dialog.Latitude):F4}{latDir}, {Math.Abs(dialog.Longitude):F4}{lonDir}");
                ClearStatusAfterDelay();
            }
        }

        private void InputCoordinateForLine_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCollectingPoints || (_collectingFor != "Line" && _collectingFor != "Measure"))
            { _isCollectingPoints = true; _collectingFor = "Line"; _isMeasuring = false; _collectedPoints.Clear(); UpdateStatus("坐标绘制线 - 输入坐标添加折点"); }
            var dialog = new CoordinateInputDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.IsValid) AddPointToDrawing(dialog.Longitude, dialog.Latitude);
        }

        private void InputCoordinateForPolygon_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCollectingPoints || (_collectingFor != "Polygon" && _collectingFor != "Area"))
            { _isCollectingPoints = true; _collectingFor = "Polygon"; _isMeasuring = false; _collectedPoints.Clear(); UpdateStatus("坐标绘制面 - 输入坐标添加折点"); }
            var dialog = new CoordinateInputDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.IsValid) AddPointToDrawing(dialog.Longitude, dialog.Latitude);
        }

        private void AddPointToDrawing(double x, double y)
        {
            var mapPoint = new MapPoint(x, y, SpatialReferences.Wgs84);
            _collectedPoints.Add(mapPoint);
            var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, _isMeasuring ? System.Drawing.Color.Red : System.Drawing.Color.Blue, 8);
            var markerGraphic = new Graphic(mapPoint, markerSymbol);
            markerGraphic.Attributes["_temp"] = true;
            _drawingOverlay.Graphics.Add(markerGraphic);
            if (_collectedPoints.Count >= 2)
            {
                ClearTemporaryGraphics();
                bool isPolygonMode = (_collectingFor == "Polygon" || _collectingFor == "Area");
                if (isPolygonMode && _collectedPoints.Count >= 3)
                {
                    var tempPoints = new List<MapPoint>(_collectedPoints);
                    tempPoints.Add(_collectedPoints[0]);
                    var polygonGeometry = new Polygon(tempPoints);
                    var fillSymbol = new SimpleFillSymbol(SimpleFillSymbolStyle.ForwardDiagonal, _isMeasuring ? System.Drawing.Color.Red : System.Drawing.Color.Blue, null);
                    var polygonGraphic = new Graphic(polygonGeometry, fillSymbol);
                    polygonGraphic.Attributes["_temp"] = true;
                    _drawingOverlay.Graphics.Add(polygonGraphic);
                    var area = GeometryEngine.Area(polygonGeometry);
                    string unit = "平方米"; double displayArea = area;
                    if (area >= 10000) { displayArea = area / 10000; unit = "公顷"; }
                    if (area >= 1000000) { displayArea = area / 1000000; unit = "平方公里"; }
                    UpdateStatus($"面积预览: {displayArea:F2} {unit} - 当前点 ({x:F6}, {y:F6})");
                }
                else
                {
                    var lineGeometry = new Polyline(_collectedPoints);
                    var lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, _isMeasuring ? System.Drawing.Color.Red : System.Drawing.Color.Blue, 3);
                    var lineGraphic = new Graphic(lineGeometry, lineSymbol);
                    lineGraphic.Attributes["_temp"] = true;
                    _drawingOverlay.Graphics.Add(lineGraphic);
                    if (isPolygonMode) UpdateStatus($"已添加 {_collectedPoints.Count} 个点 - 至少3点才能成面 ({x:F6}, {y:F6})");
                    else
                    {
                        var length = GeometryEngine.Length(lineGeometry);
                        string unit = "米"; double displayLength = length;
                        if (length >= 1000) { displayLength = length / 1000; unit = "千米"; }
                        UpdateStatus($"距离预览: {displayLength:F2} {unit} - 当前点 ({x:F6}, {y:F6})");
                    }
                }
            }
            else UpdateStatus($"已添加 1 个点 - ({x:F6}, {y:F6})");
        }

        // ==================== 视图控制 ====================
        private async void ZoomIn_Click(object sender, RoutedEventArgs e)
        { if (MyMapView == null) return; try { var vp = MyMapView.GetCurrentViewpoint(ViewpointType.CenterAndScale); if (vp != null) await MyMapView.SetViewpointScaleAsync(vp.TargetScale / 2); } catch (Exception ex) { MessageBox.Show($"缩放失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); } }

        private async void ZoomOut_Click(object sender, RoutedEventArgs e)
        { if (MyMapView == null) return; try { var vp = MyMapView.GetCurrentViewpoint(ViewpointType.CenterAndScale); if (vp != null) await MyMapView.SetViewpointScaleAsync(vp.TargetScale * 2); } catch (Exception ex) { MessageBox.Show($"缩放失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); } }

        private async void ZoomToFullExtent_Click(object sender, RoutedEventArgs e)
        { if (MyMapView?.Map == null) return; try { if (MyMapView.Map.InitialViewpoint != null) await MyMapView.SetViewpointAsync(MyMapView.Map.InitialViewpoint); } catch (Exception ex) { MessageBox.Show($"全图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); } }

        private async void RefreshMap_Click(object sender, RoutedEventArgs e)
        { if (MyMapView == null) return; try { var vp = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry); if (vp != null) await MyMapView.SetViewpointAsync(vp); MessageBox.Show("地图已刷新", "提示", MessageBoxButton.OK, MessageBoxImage.Information); } catch (Exception ex) { MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); } }

        // ==================== 底图与图层管理 ====================
        private void CmbBasemap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MyMapView?.Map == null) return;
            var selected = cmbBasemap.SelectedItem as ComboBoxItem;
            if (selected?.Tag == null) return;
            string tag = selected.Tag.ToString();
            Basemap newBasemap = null;
            try
            {
                switch (tag)
                {
                    case "Streets": newBasemap = new Basemap(BasemapStyle.ArcGISStreets); break;
                    case "Topographic": newBasemap = new Basemap(BasemapStyle.ArcGISTopographic); break;
                    case "Imagery": newBasemap = new Basemap(BasemapStyle.ArcGISImageryStandard); break;
                    case "LightGray": newBasemap = new Basemap(BasemapStyle.ArcGISLightGray); break;
                    case "DarkGray": newBasemap = new Basemap(BasemapStyle.ArcGISDarkGray); break;
                    case "Navigation": newBasemap = new Basemap(BasemapStyle.ArcGISNavigation); break;
                    default: newBasemap = new Basemap(BasemapStyle.ArcGISStreets); break;
                }
            }
            catch { newBasemap = new Basemap(BasemapStyle.ArcGISStreets); }
            if (newBasemap != null) MyMapView.Map.Basemap = newBasemap;
        }

        private async void BtnAddLayer_Click(object sender, RoutedEventArgs e)
        {
            var selected = cmbDataSource.SelectedItem as ComboBoxItem;
            if (selected?.Tag == null) return;
            string sourceType = selected.Tag.ToString();
            try
            {
                var layer = await CreateFeatureLayerFromSource(sourceType);
                if (layer != null)
                {
                    layer.Name = $"{sourceType} 图层 {_layerItems.Count + 1}";
                    MyMapView.Map.OperationalLayers.Add(layer);
                    var randomColor = GetRandomColor();
                    var item = new LayerItem { Name = layer.Name, Layer = layer, LayerColor = randomColor };
                    _layerItems.Add(item);
                    ApplyRendererToLayer(layer, randomColor);
                    if (layer.FullExtent != null && !layer.FullExtent.IsEmpty) await MyMapView.SetViewpointGeometryAsync(layer.FullExtent);
                }
            }
            catch (Exception ex) { MessageBox.Show($"添加图层失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void RemoveLayer_Click(object sender, RoutedEventArgs e)
        { var button = sender as Button; if (button?.Tag is LayerItem item) { MyMapView.Map.OperationalLayers.Remove(item.Layer); _layerItems.Remove(item); } }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is LayerItem item)
            {
                var mediaColor = System.Windows.Media.Color.FromArgb(item.LayerColor.A, item.LayerColor.R, item.LayerColor.G, item.LayerColor.B);
                var dialog = new ColorPickerDialog(mediaColor);
                if (dialog.ShowDialog() == true)
                {
                    item.LayerColor = System.Drawing.Color.FromArgb(dialog.SelectedColor.A, dialog.SelectedColor.R, dialog.SelectedColor.G, dialog.SelectedColor.B);
                    ApplyRendererToLayer(item.Layer, item.LayerColor);
                }
            }
        }

        private async void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            if (_isSelectingStart || _isSelectingEnd)
            {
                var mapPoint = e.Location;
                if (_isSelectingStart) { _startPoint = mapPoint; txtRouteStart.Text = $"({mapPoint.X:F4}, {mapPoint.Y:F4})"; _isSelectingStart = false; }
                else if (_isSelectingEnd) { _endPoint = mapPoint; txtRouteEnd.Text = $"({mapPoint.X:F4}, {mapPoint.Y:F4})"; _isSelectingEnd = false; }
                return;
            }
            if (_isCollectingPoints && MyMapView != null)
            {
                try
                {
                    var mapPoint = e.Location;
                    if (_collectingFor == "Point")
                    {
                        var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 12);
                        _drawingOverlay.Graphics.Add(new Graphic(mapPoint, markerSymbol));
                        UpdateStatus($"点已添加: ({mapPoint.X:F4}, {mapPoint.Y:F4})"); ClearStatusAfterDelay(); return;
                    }
                    if (_collectingFor == "Line" || _collectingFor == "Measure")
                    {
                        _collectedPoints.Add(mapPoint);
                        var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, _collectingFor == "Measure" ? System.Drawing.Color.Red : System.Drawing.Color.Blue, 8);
                        var markerGraphic = new Graphic(mapPoint, markerSymbol);
                        markerGraphic.Attributes["_temp"] = true;
                        _drawingOverlay.Graphics.Add(markerGraphic);
                        if (_collectedPoints.Count >= 2)
                        {
                            ClearTemporaryGraphics();
                            var lineGeometry = new Polyline(_collectedPoints.ToArray());
                            var lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, _collectingFor == "Measure" ? System.Drawing.Color.Red : System.Drawing.Color.Blue, 3);
                            var lineGraphic = new Graphic(lineGeometry, lineSymbol);
                            lineGraphic.Attributes["_temp"] = true;
                            _drawingOverlay.Graphics.Add(lineGraphic);
                            var length = GeometryEngine.Length(lineGeometry);
                            string unit = "米"; double displayLength = length;
                            if (length >= 1000) { displayLength = length / 1000; unit = "千米"; }
                            UpdateStatus($"距离预览: {displayLength:F2} {unit} (双击完成)");
                        }
                        else UpdateStatus("请继续点击添加折点");
                        return;
                    }
                    if (_collectingFor == "Polygon" || _collectingFor == "Area")
                    {
                        _collectedPoints.Add(mapPoint);
                        var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, _collectingFor == "Area" ? System.Drawing.Color.Red : System.Drawing.Color.Blue, 8);
                        var markerGraphic = new Graphic(mapPoint, markerSymbol);
                        markerGraphic.Attributes["_temp"] = true;
                        _drawingOverlay.Graphics.Add(markerGraphic);
                        if (_collectedPoints.Count >= 2)
                        {
                            ClearTemporaryGraphics();
                            var tempPoints = new List<MapPoint>(_collectedPoints);
                            if (_collectedPoints.Count >= 3) tempPoints.Add(_collectedPoints[0]);
                            var lineGeometry = new Polyline(tempPoints.ToArray());
                            var lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, _collectingFor == "Area" ? System.Drawing.Color.Red : System.Drawing.Color.Blue, 2);
                            var lineGraphic = new Graphic(lineGeometry, lineSymbol);
                            lineGraphic.Attributes["_temp"] = true;
                            _drawingOverlay.Graphics.Add(lineGraphic);
                            if (_collectedPoints.Count >= 3)
                            {
                                var tempPolygon = new Polygon(_collectedPoints.Concat(new[] { _collectedPoints[0] }).ToArray());
                                var area = GeometryEngine.Area(tempPolygon);
                                string unit = "平方米"; double displayArea = area;
                                if (area >= 10000) { displayArea = area / 10000; unit = "公顷"; }
                                if (area >= 1000000) { displayArea = area / 1000000; unit = "平方公里"; }
                                UpdateStatus($"面积预览: {displayArea:F2} {unit} (双击完成)");
                            }
                            else UpdateStatus($"已添加 {_collectedPoints.Count} 个点，至少3点成面");
                        }
                        else UpdateStatus("请继续点击添加折点");
                        return;
                    }
                }
                catch (Exception ex) { UpdateStatus($"绘图错误: {ex.Message}"); ClearStatusAfterDelay(); return; }
            }
            try
            {
                var layers = MyMapView.Map?.OperationalLayers;
                if (layers == null || layers.Count == 0) return;
                for (int i = layers.Count - 1; i >= 0; i--)
                {
                    if (layers[i] is FeatureLayer featureLayer && featureLayer.IsVisible)
                    {
                        var identifyResult = await MyMapView.IdentifyLayerAsync(featureLayer, e.Position, 10, false);
                        if (identifyResult?.GeoElements?.Count > 0)
                        {
                            var feature = identifyResult.GeoElements.First() as Feature;
                            if (feature != null)
                            {
                                var properties = new ObservableCollection<PropertyDisplay>();
                                foreach (var attr in feature.Attributes) properties.Add(new PropertyDisplay { FieldName = attr.Key, Value = attr.Value?.ToString() ?? "<null>" });
                                PropertyItems.ItemsSource = properties;
                                txtLayerName.Text = $"图层: {featureLayer.Name} (ID: {feature.Attributes.FirstOrDefault().Key})";
                                PropertyPanel.Visibility = Visibility.Visible;
                                return;
                            }
                        }
                    }
                }
                PropertyPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"要素识别错误: {ex.Message}"); }
        }

        private void MyMapView_GeoViewDoubleTapped(object sender, GeoViewInputEventArgs e)
        { if (_isCollectingPoints && (_collectingFor == "Line" || _collectingFor == "Measure" || _collectingFor == "Polygon" || _collectingFor == "Area")) { if (_collectedPoints.Count >= 2) CompleteDrawing(); } }

        private void ClosePropertyPanel_Click(object sender, RoutedEventArgs e) { PropertyPanel.Visibility = Visibility.Collapsed; }

        // ==================== 图层列表拖拽 ====================
        private void LstLayers_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        { var listBox = sender as ListBox; var item = GetListBoxItemAt(listBox, e.GetPosition(listBox)); if (item != null) { _draggedItem = item.Content as LayerItem; if (_draggedItem != null) { _dragStartPoint = e.GetPosition(listBox); _isDragging = true; } } }

        private void LstLayers_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _draggedItem == null) return;
            var listBox = sender as ListBox;
            Point currentPos = e.GetPosition(listBox);
            if (Math.Abs(currentPos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(currentPos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            { DataObject data = new DataObject("LayerItem", _draggedItem); DragDrop.DoDragDrop(listBox, data, DragDropEffects.Move); _isDragging = false; _draggedItem = null; }
        }

        private void LstLayers_Drop(object sender, DragEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;
            LayerItem draggedItem = e.Data.GetData("LayerItem") as LayerItem;
            if (draggedItem == null) return;
            Point dropPoint = e.GetPosition(listBox);
            ListBoxItem targetItem = GetListBoxItemAt(listBox, dropPoint);
            if (targetItem == null) return;
            LayerItem targetData = targetItem.Content as LayerItem;
            if (targetData == null || targetData == draggedItem) return;
            int oldIndex = _layerItems.IndexOf(draggedItem);
            int newIndex = _layerItems.IndexOf(targetData);
            if (oldIndex < newIndex) for (int i = oldIndex; i < newIndex; i++) _layerItems.Move(i, i + 1);
            else for (int i = oldIndex; i > newIndex; i--) _layerItems.Move(i, i - 1);
            SyncLayerOrder();
            e.Handled = true;
        }

        private ListBoxItem GetListBoxItemAt(ListBox listBox, Point point)
        {
            if (listBox == null) return null;
            var result = VisualTreeHelper.HitTest(listBox, point);
            if (result == null) return null;
            DependencyObject current = result.VisualHit;
            while (current != null && !(current is ListBoxItem)) current = VisualTreeHelper.GetParent(current);
            return current as ListBoxItem;
        }

        private void SyncLayerOrder()
        { var layers = MyMapView.Map?.OperationalLayers; if (layers == null) return; var tempLayers = layers.ToList(); layers.Clear(); foreach (var item in _layerItems) layers.Add(item.Layer); }

        private void btnClearLayers_Click(object sender, RoutedEventArgs e)
        {
            if (_layerItems.Count > 0)
            {
                if (MessageBox.Show("确定要清除所有图层吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                { MyMapView.Map.OperationalLayers.Clear(); _layerItems.Clear(); PropertyPanel.Visibility = Visibility.Collapsed; }
            }
            else MessageBox.Show("没有图层可清除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyRendererToLayer(Layer layer, System.Drawing.Color color)
        {
            try
            {
                if (layer is FeatureLayer featureLayer && featureLayer.FeatureTable != null) ApplyRendererToFeatureLayer(featureLayer, color);
                else if (layer is FeatureCollectionLayer fcLayer && fcLayer.FeatureCollection?.Tables.Count > 0)
                { foreach (var table in fcLayer.FeatureCollection.Tables) { var fcTable = table as FeatureCollectionTable; if (fcTable != null) ApplyRendererToFeatureCollectionTable(fcTable, color); } }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"应用渲染器错误: {ex.Message}"); }
        }

        private void ApplyRendererToFeatureLayer(FeatureLayer layer, System.Drawing.Color color)
        {
            var geoType = layer.FeatureTable.GeometryType;
            var drawColor = color;
            if (geoType == GeometryType.Point || geoType == GeometryType.Multipoint) layer.Renderer = new SimpleRenderer(new SimpleMarkerSymbol { Color = drawColor, Size = 10, Style = SimpleMarkerSymbolStyle.Circle });
            else if (geoType == GeometryType.Polyline) layer.Renderer = new SimpleRenderer(new SimpleLineSymbol { Color = drawColor, Width = 2 });
            else if (geoType == GeometryType.Polygon) { var fillColor = System.Drawing.Color.FromArgb(150, color.R, color.G, color.B); layer.Renderer = new SimpleRenderer(new SimpleFillSymbol { Color = fillColor, Outline = new SimpleLineSymbol { Color = System.Drawing.Color.Black, Width = 1 } }); }
        }

        private void ApplyRendererToFeatureCollectionTable(FeatureCollectionTable table, System.Drawing.Color color)
        {
            var geoType = table.GeometryType;
            var drawColor = color;
            if (geoType == GeometryType.Point || geoType == GeometryType.Multipoint) table.Renderer = new SimpleRenderer(new SimpleMarkerSymbol { Color = drawColor, Size = 10, Style = SimpleMarkerSymbolStyle.Circle });
            else if (geoType == GeometryType.Polyline) table.Renderer = new SimpleRenderer(new SimpleLineSymbol { Color = drawColor, Width = 2 });
            else if (geoType == GeometryType.Polygon) { var fillColor = System.Drawing.Color.FromArgb(150, color.R, color.G, color.B); table.Renderer = new SimpleRenderer(new SimpleFillSymbol { Color = fillColor, Outline = new SimpleLineSymbol { Color = System.Drawing.Color.Black, Width = 1 } }); }
        }

        private System.Drawing.Color GetRandomColor()
        { var random = new Random(); return System.Drawing.Color.FromArgb(255, random.Next(50, 230), random.Next(50, 230), random.Next(50, 230)); }

        // ==================== 创建FeatureLayer ====================
        private async Task<Layer> CreateFeatureLayerFromSource(string sourceType)
        {
            try
            {
                FeatureTable table = null;
                switch (sourceType)
                {
                    case "Shapefile":
                        var shpPath = SelectFile("Shapefile (*.shp)|*.shp");
                        if (string.IsNullOrEmpty(shpPath)) return null;
                        table = new ShapefileFeatureTable(shpPath);
                        break;
                    case "GeoJSON":
                        var geojsonPath = SelectFile("GeoJSON (*.geojson;*.json)|*.geojson;*.json");
                        if (string.IsNullOrEmpty(geojsonPath)) return null;
                        table = await LoadGeoJsonManual(geojsonPath);
                        if (table == null) return null;
                        break;
                    case "GeoPackage":
                        var gpkgPath = SelectFile("GeoPackage (*.gpkg)|*.gpkg");
                        if (string.IsNullOrEmpty(gpkgPath)) return null;
                        var gpkg = new GeoPackage(gpkgPath);
                        await gpkg.LoadAsync();
                        if (gpkg.GeoPackageFeatureTables.Count == 0) { MessageBox.Show("GeoPackage 中无要素表", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return null; }
                        table = gpkg.GeoPackageFeatureTables[0];
                        break;
                    case "MobileGeodatabase":
                        var geoDbPath = SelectFile("移动地理数据库 (*.geodatabase)|*.geodatabase");
                        if (string.IsNullOrEmpty(geoDbPath)) return null;
                        var geoDb = await Geodatabase.OpenAsync(geoDbPath);
                        if (geoDb.GeodatabaseFeatureTables.Count == 0) { MessageBox.Show("移动数据库中无要素表", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return null; }
                        table = geoDb.GeodatabaseFeatureTables[0];
                        break;
                    case "FeatureService":
                        var serviceUrl = InputUrl("请输入 ArcGIS 要素服务 URL:", "https://sampleserver6.arcgisonline.com/arcgis/rest/services/WorldCities/MapServer/0");
                        if (string.IsNullOrEmpty(serviceUrl)) return null;
                        table = new ServiceFeatureTable(new Uri(serviceUrl));
                        break;
                    case "WFS":
                        var wfsUrl = InputUrl("请输入 WFS 服务 URL:", "https://sampleserver6.arcgisonline.com/arcgis/services/SampleWorldCities/MapServer/WFSServer?service=WFS&request=GetFeature&version=2.0.0");
                        if (string.IsNullOrEmpty(wfsUrl)) return null;
                        var typeName = InputText("请输入 WFS 类型名称:", "SampleWorldCities");
                        if (string.IsNullOrEmpty(typeName)) return null;
                        var wfsTable = new WfsFeatureTable(new Uri(wfsUrl), typeName);
                        wfsTable.FeatureRequestMode = FeatureRequestMode.ManualCache;
                        await wfsTable.LoadAsync();
                        var queryParams = new QueryParameters { WhereClause = "1=1" };
                        await wfsTable.PopulateFromServiceAsync(queryParams, true, null);
                        table = wfsTable;
                        break;
                    case "OGCAPI":
                        var ogcUrl = InputUrl("请输入 OGC API 服务 URL:", "https://services.arcgis.com/V6ZHFr6zdgNZuVG0/arcgis/rest/services/World_Continent_Boundaries/FeatureServer/0");
                        if (string.IsNullOrEmpty(ogcUrl)) return null;
                        var collectionId = InputText("请输入集合 ID:", "World_Continent_Boundaries");
                        if (string.IsNullOrEmpty(collectionId)) return null;
                        var ogcTable = new OgcFeatureCollectionTable(new Uri(ogcUrl), collectionId);
                        ogcTable.FeatureRequestMode = FeatureRequestMode.ManualCache;
                        await ogcTable.LoadAsync();
                        var ogcQuery = new QueryParameters { WhereClause = "1=1" };
                        await ogcTable.PopulateFromServiceAsync(ogcQuery, true, null);
                        table = ogcTable;
                        break;
                    default: return null;
                }
                await table.LoadAsync();
                if (table is FeatureCollectionTable fcTable)
                { var featureCollection = new FeatureCollection(); featureCollection.Tables.Add(fcTable); return new FeatureCollectionLayer(featureCollection) { IsVisible = true }; }
                return new FeatureLayer(table) { IsVisible = true };
            }
            catch (Exception ex) { MessageBox.Show($"添加图层失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return null; }
        }

        // ==================== 使用 Newtonsoft.Json 加载 GeoJSON ====================
        private async Task<FeatureTable> LoadGeoJsonManual(string filePath)
        {
            try
            {
                string jsonContent = System.IO.File.ReadAllText(filePath);
                JObject root = JObject.Parse(jsonContent);

                JArray features = root["features"] as JArray;
                if (features == null || features.Count == 0)
                {
                    MessageBox.Show("GeoJSON 中未找到要素", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                if (MyMapView.Map != null && MyMapView.Map.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded)
                    await MyMapView.Map.RetryLoadAsync();
                var targetSr = MyMapView.Map?.SpatialReference ?? SpatialReferences.WebMercator;

                List<Esri.ArcGISRuntime.Geometry.Geometry> parsedGeometries = new List<Esri.ArcGISRuntime.Geometry.Geometry>();
                GeometryType? tableGeometryType = null;

                foreach (JObject feature in features)
                {
                    try
                    {
                        JObject geometryObj = feature["geometry"] as JObject;
                        if (geometryObj == null) continue;

                        string type = geometryObj["type"]?.ToString();
                        JToken coordinates = geometryObj["coordinates"];

                        if (string.IsNullOrEmpty(type) || coordinates == null) continue;

                        Esri.ArcGISRuntime.Geometry.Geometry geometry = ParseGeometryFromJToken(type, coordinates);
                        if (geometry == null) continue;

                        if (geometry.SpatialReference == null || geometry.SpatialReference.IsGeographic)
                        {
                            try { geometry = GeometryEngine.Project(geometry, targetSr); }
                            catch { continue; }
                        }
                        else if (!geometry.SpatialReference.Equals(targetSr))
                        {
                            try { geometry = GeometryEngine.Project(geometry, targetSr); }
                            catch { continue; }
                        }

                        if (tableGeometryType == null)
                            tableGeometryType = geometry.GeometryType;
                        else if (geometry.GeometryType != tableGeometryType)
                            continue;

                        parsedGeometries.Add(geometry);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"解析 Feature 失败: {ex.Message}");
                    }
                }

                if (parsedGeometries.Count == 0)
                {
                    MessageBox.Show("GeoJSON 无法解析有效图形", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                var table = new FeatureCollectionTable(new List<Field>(), tableGeometryType.Value, targetSr);
                foreach (var geometry in parsedGeometries)
                {
                    var newFeature = table.CreateFeature(new Dictionary<string, object>(), geometry);
                    await table.AddFeatureAsync(newFeature);
                }

                UpdateStatus($"成功加载 {parsedGeometries.Count} 个要素");
                return table;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载 GeoJSON 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        // ==================== Newtonsoft.Json 解析几何 ====================
        private Esri.ArcGISRuntime.Geometry.Geometry ParseGeometryFromJToken(string type, JToken coordinates)
        {
            try
            {
                switch (type)
                {
                    case "Point": return ParsePointFromJToken(coordinates);
                    case "MultiPoint": return ParseMultiPointFromJToken(coordinates);
                    case "LineString": return ParseLineStringFromJToken(coordinates);
                    case "MultiLineString": return ParseMultiLineStringFromJToken(coordinates);
                    case "Polygon": return ParsePolygonFromJToken(coordinates);
                    case "MultiPolygon": return ParseMultiPolygonFromJToken(coordinates);
                    default:
                        System.Diagnostics.Debug.WriteLine($"未知几何类型: {type}");
                        return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析几何失败 ({type}): {ex.Message}");
                return null;
            }
        }

        private MapPoint ParsePointFromJToken(JToken coords)
        {
            if (coords == null || coords.Type != JTokenType.Array) return null;
            var arr = coords as JArray;
            if (arr == null || arr.Count < 2) return null;
            double x = arr[0].Value<double>();
            double y = arr[1].Value<double>();
            double z = arr.Count > 2 ? arr[2].Value<double>() : 0;
            return new MapPoint(x, y, z, SpatialReferences.Wgs84);
        }

        private Multipoint ParseMultiPointFromJToken(JToken coords)
        {
            if (coords == null || coords.Type != JTokenType.Array) return null;
            var points = new List<MapPoint>();
            foreach (var item in coords as JArray)
            {
                var pt = ParsePointFromJToken(item);
                if (pt != null) points.Add(pt);
            }
            return points.Count > 0 ? new Multipoint(points, SpatialReferences.Wgs84) : null;
        }

        private Polyline ParseLineStringFromJToken(JToken coords)
        {
            if (coords == null || coords.Type != JTokenType.Array) return null;
            var points = new List<MapPoint>();
            foreach (var item in coords as JArray)
            {
                var pt = ParsePointFromJToken(item);
                if (pt != null) points.Add(pt);
            }
            return points.Count >= 2 ? new Polyline(points, SpatialReferences.Wgs84) : null;
        }

        private Polyline ParseMultiLineStringFromJToken(JToken coords)
        {
            if (coords == null || coords.Type != JTokenType.Array) return null;
            var allPoints = new List<MapPoint>();
            foreach (var line in coords as JArray)
            {
                foreach (var ptToken in line as JArray)
                {
                    var pt = ParsePointFromJToken(ptToken);
                    if (pt != null) allPoints.Add(pt);
                }
            }
            return allPoints.Count >= 2 ? new Polyline(allPoints, SpatialReferences.Wgs84) : null;
        }

        private Polygon ParsePolygonFromJToken(JToken coords)
        {
            if (coords == null || coords.Type != JTokenType.Array) return null;
            var rings = new List<List<MapPoint>>();
            foreach (var ring in coords as JArray)
            {
                var pts = new List<MapPoint>();
                foreach (var ptToken in ring as JArray)
                {
                    var pt = ParsePointFromJToken(ptToken);
                    if (pt != null) pts.Add(pt);
                }
                if (pts.Count >= 3) rings.Add(pts);
            }
            return rings.Count > 0 ? new Polygon(rings, SpatialReferences.Wgs84) : null;
        }

        private Polygon ParseMultiPolygonFromJToken(JToken coords)
        {
            if (coords == null || coords.Type != JTokenType.Array) return null;
            var allRings = new List<List<MapPoint>>();
            foreach (var polygon in coords as JArray)
            {
                foreach (var ring in polygon as JArray)
                {
                    var pts = new List<MapPoint>();
                    foreach (var ptToken in ring as JArray)
                    {
                        var pt = ParsePointFromJToken(ptToken);
                        if (pt != null) pts.Add(pt);
                    }
                    if (pts.Count >= 3) allRings.Add(pts);
                }
            }
            return allRings.Count > 0 ? new Polygon(allRings, SpatialReferences.Wgs84) : null;
        }

        // ==================== 双击缩放到图层范围 ====================
        private async void LstLayers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox; if (listBox == null) return;
            var hitTestResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox)); if (hitTestResult == null) return;
            DependencyObject current = hitTestResult.VisualHit;
            while (current != null && !(current is ListBoxItem)) current = VisualTreeHelper.GetParent(current);
            var listBoxItem = current as ListBoxItem; if (listBoxItem == null) return;
            var layerItem = listBoxItem.Content as LayerItem; if (layerItem?.Layer == null) return;
            try
            {
                if (layerItem.Layer.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded) await layerItem.Layer.LoadAsync();
                Esri.ArcGISRuntime.Geometry.Geometry extent = null;
                if (layerItem.Layer.FullExtent != null && !layerItem.Layer.FullExtent.IsEmpty) extent = layerItem.Layer.FullExtent;
                else if (layerItem.Layer is FeatureLayer featureLayer && featureLayer.FeatureTable != null)
                { if (featureLayer.FeatureTable.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded) await featureLayer.FeatureTable.LoadAsync(); extent = featureLayer.FeatureTable.Extent; }
                else if (layerItem.Layer is FeatureCollectionLayer fcLayer && fcLayer.FeatureCollection?.Tables.Count > 0)
                { var table = fcLayer.FeatureCollection.Tables[0]; if (table.LoadStatus != Esri.ArcGISRuntime.LoadStatus.Loaded) await table.LoadAsync(); extent = table.Extent; }
                if (extent == null || extent.IsEmpty) { MessageBox.Show("该图层没有有效范围", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                await MyMapView.SetViewpointGeometryAsync(extent, 50);
            }
            catch (Exception ex) { MessageBox.Show($"缩放到图层失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // ==================== 辅助输入 ====================
        private string SelectFile(string filter) { var dlg = new OpenFileDialog { Filter = filter, RestoreDirectory = true }; return dlg.ShowDialog() == true ? dlg.FileName : null; }
        private string InputUrl(string message, string defaultValue = "") { var input = new InputDialog(message, defaultValue); return input.ShowDialog() == true ? input.Answer : null; }
        private string InputText(string message, string defaultValue = "") { var input = new InputDialog(message, defaultValue); return input.ShowDialog() == true ? input.Answer : null; }

        // ==================== 状态栏 ====================
        private void UpdateStatus(string message) { txtStatus.Text = message; }
        private async void ClearStatusAfterDelay(int milliseconds = 3000) { await Task.Delay(milliseconds); if (txtStatus != null && !_isCollectingPoints) txtStatus.Text = "就绪"; }

        // ==================== 地址搜索 ====================
        private async void SearchAddress_Click(object sender, RoutedEventArgs e)
        {
            string address = txtSearchAddress.Text.Trim();
            if (string.IsNullOrEmpty(address)) { MessageBox.Show("请输入搜索地址", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            UpdateStatus("正在搜索地址...");
            try
            {
                if (!IsNetworkAvailable()) { UpdateStatus("网络不可用"); ClearStatusAfterDelay(); MessageBox.Show("网络不可用，请检查网络连接", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (_locatorTask == null)
                {
                    var locatorUrl = "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer";
                    _locatorTask = await LocatorTask.CreateAsync(new Uri(locatorUrl));
                    // ✅ 为地理编码服务设置 API Key
                    _locatorTask.ApiKey = ArcGISRuntimeEnvironment.ApiKey;
                }
                var geocodeParams = new GeocodeParameters { MaxResults = 5, OutputSpatialReference = SpatialReferences.Wgs84 };
                geocodeParams.ResultAttributeNames.Add("*");
                UpdateStatus("正在查询...");
                var results = await _locatorTask.GeocodeAsync(address, geocodeParams);
                if (results == null || !results.Any()) { UpdateStatus("未找到该地址"); ClearStatusAfterDelay(); MessageBox.Show("未找到该地址，请检查输入", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                var resultDialog = new AddressResultDialog(results);
                if (resultDialog.ShowDialog() == true && resultDialog.SelectedResult != null)
                {
                    var selectedResult = resultDialog.SelectedResult;
                    ClearSearchGraphics();
                    AddSearchMarker(selectedResult);
                    if (selectedResult.Extent != null) await MyMapView.SetViewpointAsync(new Viewpoint(selectedResult.Extent));
                    else if (selectedResult.DisplayLocation != null) await MyMapView.SetViewpointCenterAsync(selectedResult.DisplayLocation, 5000);
                    UpdateStatus($"定位成功: {selectedResult.Label}");
                }
            }
            catch (Exception ex) { UpdateStatus($"搜索失败: {ex.Message}"); ClearStatusAfterDelay(); MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private bool IsNetworkAvailable()
        { try { using (var client = new System.Net.WebClient()) using (client.OpenRead("http://www.google.com")) return true; } catch { return false; } }

        private void AddSearchMarker(GeocodeResult result)
        {
            if (result.DisplayLocation == null) return;
            var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Diamond, System.Drawing.Color.Green, 12);
            var graphic = new Graphic(result.DisplayLocation) { Symbol = markerSymbol };
            graphic.Attributes["_search"] = true; graphic.Attributes["Label"] = result.Label;
            _drawingOverlay.Graphics.Add(graphic);
            var textSymbol = new TextSymbol { Color = System.Drawing.Color.Black, Size = 12, Text = result.Label, HorizontalAlignment = Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center, VerticalAlignment = Esri.ArcGISRuntime.Symbology.VerticalAlignment.Bottom, OffsetY = -5 };
            var textGraphic = new Graphic(result.DisplayLocation) { Symbol = textSymbol };
            textGraphic.Attributes["_search"] = true;
            _drawingOverlay.Graphics.Add(textGraphic);
        }

        private void QuickLocate_Click(object sender, RoutedEventArgs e)
        {
            var cities = new List<CityLocation> {
                new CityLocation("北京",116.4074,39.9042), new CityLocation("上海",121.4737,31.2304),
                new CityLocation("广州",113.2644,23.1291), new CityLocation("深圳",114.0579,22.5431),
                new CityLocation("成都",104.0668,30.5728), new CityLocation("杭州",120.1552,30.2874),
                new CityLocation("南京",118.7969,32.0603), new CityLocation("武汉",114.3055,30.5928),
                new CityLocation("西安",108.9480,34.2619), new CityLocation("重庆",106.5516,29.5630),
                new CityLocation("天津",117.2008,39.0842), new CityLocation("苏州",120.6208,31.3251),
                new CityLocation("香港",114.1747,22.3193), new CityLocation("东京",139.6917,35.6895),
                new CityLocation("首尔",127.0794,37.5407), new CityLocation("纽约",-74.0060,40.7128),
                new CityLocation("伦敦",-0.1276,51.5074), new CityLocation("巴黎",2.3522,48.8566),
                new CityLocation("悉尼",151.2093,-33.8688), new CityLocation("新加坡",103.8198,1.3521)
            };
            var dialog = new QuickLocateDialog(cities);
            if (dialog.ShowDialog() == true && dialog.SelectedCity != null)
            {
                var city = dialog.SelectedCity;
                var mapPoint = new MapPoint(city.Longitude, city.Latitude, SpatialReferences.Wgs84);
                ClearSearchGraphics();
                AddCityMarker(city);
                MyMapView.SetViewpointCenterAsync(mapPoint, 50000);
                UpdateStatus($"快速定位: {city.Name}");
            }
        }

        private void AddCityMarker(CityLocation city)
        {
            var mapPoint = new MapPoint(city.Longitude, city.Latitude, SpatialReferences.Wgs84);
            var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Diamond, System.Drawing.Color.Green, 12);
            var graphic = new Graphic(mapPoint) { Symbol = markerSymbol };
            graphic.Attributes["_search"] = true; graphic.Attributes["Label"] = city.Name;
            _drawingOverlay.Graphics.Add(graphic);
            var textSymbol = new TextSymbol { Color = System.Drawing.Color.Black, Size = 12, Text = city.Name, HorizontalAlignment = Esri.ArcGISRuntime.Symbology.HorizontalAlignment.Center, VerticalAlignment = Esri.ArcGISRuntime.Symbology.VerticalAlignment.Bottom, OffsetY = -5 };
            var textGraphic = new Graphic(mapPoint) { Symbol = textSymbol };
            textGraphic.Attributes["_search"] = true;
            _drawingOverlay.Graphics.Add(textGraphic);
        }

        private void ClearSearchGraphics()
        { var searchGraphics = _drawingOverlay.Graphics.Where(g => g.Attributes.ContainsKey("_search") && (bool)g.Attributes["_search"]).ToList(); foreach (var g in searchGraphics) _drawingOverlay.Graphics.Remove(g); }

        // ==================== 路径规划 ====================
        private void RoutePlanning_Click(object sender, RoutedEventArgs e)
        { RoutePanel.Visibility = RoutePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (RoutePanel.Visibility == Visibility.Visible) { _isSelectingStart = false; _isSelectingEnd = false; } }

        private void CloseRoutePanel_Click(object sender, RoutedEventArgs e) { RoutePanel.Visibility = Visibility.Collapsed; ClearRouteGraphics(); }

        private void PickStartPoint_Click(object sender, RoutedEventArgs e) { _isSelectingStart = true; _isSelectingEnd = false; MessageBox.Show("请在地图上点击起点", "提示", MessageBoxButton.OK, MessageBoxImage.Information); }

        private void PickEndPoint_Click(object sender, RoutedEventArgs e) { _isSelectingStart = false; _isSelectingEnd = true; MessageBox.Show("请在地图上点击终点", "提示", MessageBoxButton.OK, MessageBoxImage.Information); }

        private async void CalculateRoute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var serviceItem = cmbRouteService.SelectedItem as ComboBoxItem;
                var travelModeItem = cmbTravelMode.SelectedItem as ComboBoxItem;
                if (serviceItem == null || travelModeItem == null) { MessageBox.Show("请选择路线服务和出行方式", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                string startText = txtRouteStart.Text.Trim(); string endText = txtRouteEnd.Text.Trim();
                MapPoint start = _startPoint; MapPoint end = _endPoint;
                if (start == null && !string.IsNullOrEmpty(startText)) { start = await GeocodeLocation(startText); if (start == null) { MessageBox.Show("无法解析起点地址", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; } }
                if (end == null && !string.IsNullOrEmpty(endText)) { end = await GeocodeLocation(endText); if (end == null) { MessageBox.Show("无法解析终点地址", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; } }
                if (start == null || end == null) { MessageBox.Show("请设置有效的起终点（坐标或地址）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                string service = serviceItem.Tag.ToString(); string travelMode = travelModeItem.Tag.ToString();
                if (service == "ArcGIS") await CalculateArcGISRoute(start, end, travelMode);
                else if (service == "Tianditu") { MessageBox.Show("天地图路线需要 API Key，暂用 ArcGIS Online", "提示", MessageBoxButton.OK, MessageBoxImage.Information); await CalculateArcGISRoute(start, end, travelMode); }
            }
            catch (Exception ex) { MessageBox.Show($"路径规划失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async Task<MapPoint> GeocodeLocation(string address)
        {
            try
            {
                if (_locatorTask == null)
                {
                    var locatorUrl = "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer";
                    _locatorTask = await LocatorTask.CreateAsync(new Uri(locatorUrl));
                    _locatorTask.ApiKey = ArcGISRuntimeEnvironment.ApiKey;
                }
                var results = await _locatorTask.GeocodeAsync(address);
                if (results.Any()) return results.First().DisplayLocation;
            }
            catch { }
            return null;
        }

        private async Task CalculateArcGISRoute(MapPoint start, MapPoint end, string travelMode)
        {
            try
            {
                // 硬编码你的 API Key（直接从 App.xaml.cs 复制）
                string apiKey = "AAPTaHMNa0MN4o4so22wXNOf2sA..O2MK3rHy6tqM87UzMvBqvy3Bp0UFFvdnNiOBhU6RF1GGGSZeyQFabvS22MbKR0-qmvq6wjPdRiQqvgQxXVmcJ1-L2aN2IZQI-L0P_V9bQwx1XnOwdcf5-n30Y4LrLy9Wl87tJYHTzFl9is6WAUwosQi12pfPUxxTDkmm7pFU9PpVlWx4dEYrVW6o2xLsnq2YRqVLsRpPsLospvvLJPVi_XJ9GBi_HY07cGhGd_Lonf40wAqjOxQyAT1_FL6wdkLV";

                // 使用直接 HTTP 请求调用路由服务（确保 token 被正确传递）
                string stops = $"{start.X},{start.Y};{end.X},{end.Y}";
                string url = $"https://route.arcgis.com/arcgis/rest/services/World/Route/NAServer/Route_World/solve?f=json&stops={stops}&token={apiKey}";

                using (var client = new System.Net.Http.HttpClient())
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync();

                    // 解析 JSON 响应
                    dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                    
                    if (data.routes != null && data.routes.features != null && data.routes.features.Count > 0)
                    {
                        _routeOverlay.Graphics.Clear();
                        
                        // 获取路线几何数据
                        var routeFeature = data.routes.features[0];
                        var geometry = routeFeature.geometry;
                        
                        // 解析 polyline 坐标
                        List<MapPoint> points = new List<MapPoint>();
                        foreach (var path in geometry.paths)
                        {
                            foreach (var coord in path)
                            {
                                points.Add(new MapPoint(
                                    Convert.ToDouble(coord[0]),
                                    Convert.ToDouble(coord[1]),
                                    SpatialReferences.Wgs84
                                ));
                            }
                        }
                        
                        // 创建路线几何
                        Polyline routeGeometry = new Polyline(points);
                        
                        // 添加路线和标记
                        _routeOverlay.Graphics.Add(new Graphic(routeGeometry, new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Blue, 4)));
                        _routeOverlay.Graphics.Add(new Graphic(start, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Green, 12)));
                        _routeOverlay.Graphics.Add(new Graphic(end, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 12)));

                        // 获取路线属性
                        var attributes = routeFeature.attributes;
                        double distanceKm = Convert.ToDouble(attributes.Total_Kilometers);
                        double durationMin = Convert.ToDouble(attributes.Total_TravelTime);

                        RouteResultPanel.Visibility = Visibility.Visible;
                        txtRouteDistance.Text = $"距离: {distanceKm:F2} 公里";
                        txtRouteDuration.Text = $"用时: {durationMin:F1} 分钟";

                        await MyMapView.SetViewpointGeometryAsync(routeGeometry.Extent, 50);
                    }
                    else
                    {
                        MessageBox.Show("未找到路线", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"路线计算失败: {ex.Message}";
                if (ex.Message.Contains("Invalid Token") || ex.Message.Contains("token"))
                {
                    errorMsg += "\n\n请检查：\n1. API Key 是否正确\n2. 是否在 ArcGIS Developer Dashboard 中启用了 Routing 服务\n3. 是否启用了 Geocoding 服务";
                }
                MessageBox.Show(errorMsg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearRoute_Click(object sender, RoutedEventArgs e)
        { ClearRouteGraphics(); _startPoint = null; _endPoint = null; txtRouteStart.Text = ""; txtRouteEnd.Text = ""; RouteResultPanel.Visibility = Visibility.Collapsed; }

        private void ClearRouteGraphics() { _routeOverlay.Graphics.Clear(); _startPoint = null; _endPoint = null; }

        // ==================== 3D功能 ====================
        private void Toggle3D_Click(object sender, RoutedEventArgs e)
        {
            _is3DMode = !_is3DMode;
            if (_is3DMode) { SwitchTo3D(); btnToggle3D.Content = "切换到 2D 地图"; }
            else { SwitchTo2D(); btnToggle3D.Content = "切换到 3D 场景"; }
        }

        private void SwitchTo3D()
        {
            try
            {
                if (_scene == null) InitializeScene();
                MySceneView.Scene = _scene;
                MyMapView.Visibility = Visibility.Collapsed;
                MySceneView.Visibility = Visibility.Visible;
                SyncGraphicsOverlays3D();
                UpdateStatus("已切换到3D场景");
            }
            catch (Exception ex) { MessageBox.Show($"切换到3D失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SwitchTo2D() { MySceneView.Visibility = Visibility.Collapsed; MyMapView.Visibility = Visibility.Visible; UpdateStatus("已切换到2D地图"); }

        private void InitializeScene()
        {
            _scene = new Scene();
            Update3DBasemap("TerrainWithImagery");
            try
            {
                _elevationSource = new ArcGISTiledElevationSource(new Uri("https://elevation3d.arcgis.com/arcgis/rest/services/WorldElevation3D/Terrain3D/ImageServer"));
                _scene.BaseSurface.ElevationSources.Add(_elevationSource);
            }
            catch { }
            _scene.InitialViewpoint = new Viewpoint(new MapPoint(116.4, 39.9, SpatialReferences.Wgs84), 10000000);
        }

        private void Update3DBasemap(string basemapType)
        {
            if (_scene == null) return;
            Basemap basemap = null;
            switch (basemapType)
            {
                case "TerrainWithImagery": basemap = new Basemap(BasemapStyle.ArcGISImagery); break;
                case "TerrainWithStreets": basemap = new Basemap(BasemapStyle.ArcGISStreets); break;
                case "TerrainWithDark": basemap = new Basemap(BasemapStyle.ArcGISDarkGray); break;
                case "TerrainOnly": basemap = new Basemap(); break;
                default: basemap = new Basemap(BasemapStyle.ArcGISImagery); break;
            }
            _scene.Basemap = basemap;
        }

        private void SyncGraphicsOverlays3D()
        {
            MySceneView.GraphicsOverlays.Clear();
            var overlayCopy = new GraphicsOverlay();
            overlayCopy.SceneProperties.SurfacePlacement = _currentSurfacePlacement;
            foreach (var g in _drawingOverlay.Graphics) overlayCopy.Graphics.Add(g);
            MySceneView.GraphicsOverlays.Add(overlayCopy);
            var routeCopy = new GraphicsOverlay();
            routeCopy.SceneProperties.SurfacePlacement = _currentSurfacePlacement;
            foreach (var g in _routeOverlay.Graphics) routeCopy.Graphics.Add(g);
            MySceneView.GraphicsOverlays.Add(routeCopy);
        }

        private void cbo3DBasemap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { if (_scene == null || cbo3DBasemap.SelectedItem == null) return; var selectedItem = (ComboBoxItem)cbo3DBasemap.SelectedItem; var basemapType = selectedItem.Tag?.ToString() ?? selectedItem.Content.ToString(); Update3DBasemap(basemapType); }

        private async void TopView_Click(object sender, RoutedEventArgs e)
        { if (!_is3DMode || MySceneView.Scene == null) return; try { var camera = MySceneView.Camera; await MySceneView.SetViewpointCameraAsync(new Camera(camera.Location, 0, 90, 0), TimeSpan.FromSeconds(1)); } catch { } }

        private async void TiltView_Click(object sender, RoutedEventArgs e)
        { if (!_is3DMode || MySceneView.Scene == null) return; try { var camera = MySceneView.Camera; await MySceneView.SetViewpointCameraAsync(new Camera(camera.Location, camera.Heading, 45, 0), TimeSpan.FromSeconds(1)); } catch { } }

        private async void Reset3DView_Click(object sender, RoutedEventArgs e)
        { if (!_is3DMode || MySceneView.Scene == null) return; try { await MySceneView.SetViewpointAsync(new Viewpoint(new MapPoint(116.4, 39.9, SpatialReferences.Wgs84), 10000000), TimeSpan.FromSeconds(1)); } catch { } }

        private async void Rotate3D_Click(object sender, RoutedEventArgs e)
        { if (!_is3DMode || MySceneView.Scene == null) return; try { var camera = MySceneView.Camera; await MySceneView.SetViewpointCameraAsync(new Camera(camera.Location, (camera.Heading + 45) % 360, camera.Pitch, camera.Roll), TimeSpan.FromSeconds(0.5)); } catch { } }

        private void Add3DPoint_Click(object sender, RoutedEventArgs e)
        {
            if (!_is3DMode) { MessageBox.Show("请先切换到3D场景", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var inputDialog = new InputDialog("输入经度,纬度,高度(米):", "116.4,39.9,100");
            if (inputDialog.ShowDialog() == true)
            {
                try
                {
                    var parts = inputDialog.Answer.Split(',');
                    if (parts.Length >= 2)
                    {
                        double lon = double.Parse(parts[0].Trim()); double lat = double.Parse(parts[1].Trim()); double alt = parts.Length >= 3 ? double.Parse(parts[2].Trim()) : 0;
                        var point = new MapPoint(lon, lat, alt, SpatialReferences.Wgs84);
                        var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 16);
                        var graphic = new Graphic(point, markerSymbol);
                        var overlay = MySceneView.GraphicsOverlays.FirstOrDefault();
                        if (overlay == null) { overlay = new GraphicsOverlay(); overlay.SceneProperties.SurfacePlacement = SurfacePlacement.Absolute; MySceneView.GraphicsOverlays.Add(overlay); }
                        overlay.Graphics.Add(graphic);
                        UpdateStatus($"添加3D点: ({lon}, {lat}, {alt}m)");
                    }
                }
                catch (Exception ex) { MessageBox.Show($"添加3D点失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private async void MySceneView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            if (!_is3DMode) return;
            try
            {
                var identifyResult = await MySceneView.IdentifyGraphicsOverlaysAsync(e.Position, 10, false, 1);
                bool identified = false;
                foreach (var overlayResult in identifyResult)
                {
                    foreach (var graphic in overlayResult.Graphics)
                    {
                        if (graphic.Attributes.ContainsKey("_temp")) continue;
                        ShowGraphicProperties3D(graphic);
                        identified = true;
                        break;
                    }
                    if (identified) break;
                }
                if (_isCollectingPoints && _is3DMode)
                {
                    var mapPoint = await MySceneView.ScreenToLocationAsync(e.Position);
                    if (mapPoint != null)
                    {
                        _collectedPoints.Add(mapPoint);
                        var markerSymbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Yellow, 12);
                        var tempGraphic = new Graphic(mapPoint, markerSymbol);
                        tempGraphic.Attributes["_temp"] = true;
                        var overlay = MySceneView.GraphicsOverlays.FirstOrDefault();
                        if (overlay == null) { overlay = new GraphicsOverlay(); overlay.SceneProperties.SurfacePlacement = _currentSurfacePlacement; MySceneView.GraphicsOverlays.Add(overlay); }
                        overlay.Graphics.Add(tempGraphic);
                        UpdateStatus($"添加点 #{_collectedPoints.Count}: ({mapPoint.X:F4}, {mapPoint.Y:F4}, {mapPoint.Z:F1}m)");
                    }
                }
                else if (!identified)
                {
                    var mapPoint = await MySceneView.ScreenToLocationAsync(e.Position);
                    if (mapPoint != null) UpdateStatus($"3D坐标: X={mapPoint.X:F4}, Y={mapPoint.Y:F4}, Z={mapPoint.Z:F1}米");
                }
            }
            catch { }
        }

        private async void MySceneView_GeoViewDoubleTapped(object sender, GeoViewInputEventArgs e)
        {
            if (!_is3DMode) return;
            if (_isCollectingPoints && _is3DMode) CompleteDrawing3D();
            else
            {
                try
                {
                    var mapPoint = await MySceneView.ScreenToLocationAsync(e.Position);
                    if (mapPoint != null)
                    {
                        var cam = MySceneView.Camera;
                        await MySceneView.SetViewpointCameraAsync(new Camera(mapPoint, cam.Heading, cam.Pitch, cam.Location.Z * 0.5), TimeSpan.FromSeconds(0.5));
                    }
                }
                catch { }
            }
        }

        private void CompleteDrawing3D()
        {
            if (!_isCollectingPoints || _collectedPoints.Count < (_collectingFor == "Point" ? 1 : 2)) { UpdateStatus("点数量不足"); return; }
            try
            {
                foreach (var overlay in MySceneView.GraphicsOverlays) { var tempList = overlay.Graphics.Where(g => g.Attributes.ContainsKey("_temp")).ToList(); foreach (var g in tempList) overlay.Graphics.Remove(g); }
                Graphic resultGraphic = null; string resultText = "";
                if (_collectingFor == "Point") { var pt = _collectedPoints[0]; resultGraphic = new Graphic(pt, new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Red, 16)); resultText = "3D点"; }
                else if (_collectingFor == "Line" || _collectingFor == "Measure") { var lineGeom = new Polyline(_collectedPoints); resultGraphic = new Graphic(lineGeom, new SimpleLineSymbol(_collectingFor == "Measure" ? SimpleLineSymbolStyle.Dash : SimpleLineSymbolStyle.Solid, _collectingFor == "Measure" ? System.Drawing.Color.Red : System.Drawing.Color.DodgerBlue, 3)); resultText = _collectingFor == "Measure" ? $"3D距离: {FormatLength3D(GeometryEngine.Length(lineGeom))}" : "3D线"; }
                else if (_collectingFor == "Polygon" || _collectingFor == "Area") { _collectedPoints.Add(_collectedPoints[0]); var polyGeom = new Polygon(_collectedPoints); resultGraphic = new Graphic(polyGeom, new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, System.Drawing.Color.FromArgb(60, 70, 130, 180), new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.DodgerBlue, 2))); resultText = _collectingFor == "Area" ? $"3D面积: {FormatArea3D(GeometryEngine.Area(polyGeom))}" : "3D面"; }
                if (resultGraphic != null)
                {
                    var overlay = MySceneView.GraphicsOverlays.FirstOrDefault();
                    if (overlay == null) { overlay = new GraphicsOverlay(); overlay.SceneProperties.SurfacePlacement = _currentSurfacePlacement; MySceneView.GraphicsOverlays.Add(overlay); }
                    overlay.Graphics.Add(resultGraphic);
                    UpdateStatus($"{resultText} 已添加到3D场景");
                }
                _isCollectingPoints = false; _isMeasuring = false; _collectingFor = ""; _collectedPoints.Clear();
            }
            catch (Exception ex) { UpdateStatus($"3D绘图完成失败: {ex.Message}"); }
        }

        private string FormatLength3D(double meters) { if (meters >= 1000) return $"{meters / 1000:F2} 千米"; return $"{meters:F2} 米"; }
        private string FormatArea3D(double sqMeters) { if (sqMeters >= 1000000) return $"{sqMeters / 1000000:F2} 平方公里"; if (sqMeters >= 10000) return $"{sqMeters / 10000:F2} 公顷"; return $"{sqMeters:F2} 平方米"; }

        private void ShowGraphicProperties3D(Graphic graphic)
        {
            if (graphic?.Geometry == null) return;
            var props = new ObservableCollection<PropertyDisplay>();
            var g = graphic.Geometry;
            props.Add(new PropertyDisplay { FieldName = "图形类型", Value = g.GeometryType.ToString() });
            if (g is MapPoint pt) { props.Add(new PropertyDisplay { FieldName = "经度", Value = pt.X.ToString("F6") }); props.Add(new PropertyDisplay { FieldName = "纬度", Value = pt.Y.ToString("F6") }); props.Add(new PropertyDisplay { FieldName = "高度", Value = $"{pt.Z:F1} 米" }); }
            else if (g is Polyline pl) { int cnt = pl.Parts.FirstOrDefault()?.Points.Count ?? 0; props.Add(new PropertyDisplay { FieldName = "折点数量", Value = cnt.ToString() }); props.Add(new PropertyDisplay { FieldName = "长度", Value = FormatLength3D(GeometryEngine.Length(g)) }); }
            else if (g is Polygon pg) { int cnt = pg.Parts.FirstOrDefault()?.Points.Count ?? 0; props.Add(new PropertyDisplay { FieldName = "折点数量", Value = cnt.ToString() }); props.Add(new PropertyDisplay { FieldName = "面积", Value = FormatArea3D(GeometryEngine.Area(g)) }); props.Add(new PropertyDisplay { FieldName = "周长", Value = FormatLength3D(GeometryEngine.Length(g)) }); }
            if (graphic.Attributes.ContainsKey("extrusionHeight")) props.Add(new PropertyDisplay { FieldName = "拉伸高度", Value = $"{graphic.Attributes["extrusionHeight"]} 米" });
            PropertyItems.ItemsSource = props; txtLayerName.Text = $"3D图形 ({props.Count} 个属性)"; txtPropertyCount.Text = $"{props.Count} 个属性"; PropertyPanel.Visibility = Visibility.Visible;
        }

        #region Web Scene 加载
        private async void LoadWebScene_Click(object sender, RoutedEventArgs e)
        {
            if (!_is3DMode) { MessageBox.Show("请先切换到3D场景", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            string input = txtWebSceneUrl.Text.Trim();
            if (string.IsNullOrEmpty(input)) { MessageBox.Show("请输入 Web Scene URL 或 Item ID", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            await LoadWebSceneAsync(input);
        }

        private async Task LoadWebSceneAsync(string sceneIdOrUrl)
        {
            try
            {
                UpdateStatus("正在加载 Web Scene...");
                Scene webScene;
                if (sceneIdOrUrl.StartsWith("http"))
                {
                    var uri = new Uri(sceneIdOrUrl);
                    var segments = uri.Segments;
                    var itemId = segments.LastOrDefault()?.Trim('/');
                    if (string.IsNullOrEmpty(itemId)) throw new Exception("无法从 URL 解析 Item ID");
                    var portal = await ArcGISPortal.CreateAsync();
                    var item = await PortalItem.CreateAsync(portal, itemId);
                    webScene = new Scene(item);
                }
                else
                {
                    var portal = await ArcGISPortal.CreateAsync();
                    var item = await PortalItem.CreateAsync(portal, sceneIdOrUrl);
                    webScene = new Scene(item);
                }
                await webScene.LoadAsync();
                MySceneView.Scene = webScene;
                _scene = webScene;
                UpdateStatus($"Web Scene 加载成功: {webScene.Item?.Title ?? "未命名"}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载 Web Scene 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Web Scene 加载失败");
            }
        }
        #endregion

        #region Web Map 加载
        private async void LoadWebMap_Click(object sender, RoutedEventArgs e)
        {
            string input = txtWebMapUrl.Text.Trim();
            if (string.IsNullOrEmpty(input)) { MessageBox.Show("请输入 Web Map URL 或 Item ID", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            await LoadWebMapAsync(input);
        }

        private async Task LoadWebMapAsync(string mapIdOrUrl)
        {
            try
            {
                UpdateStatus("正在加载 Web Map...");
                Map webMap;

                if (mapIdOrUrl.StartsWith("http"))
                {
                    var uri = new Uri(mapIdOrUrl);
                    var segments = uri.Segments;
                    var itemId = segments.LastOrDefault()?.Trim('/');
                    if (string.IsNullOrEmpty(itemId)) throw new Exception("无法从 URL 解析 Item ID");

                    var portal = await ArcGISPortal.CreateAsync();
                    var item = await PortalItem.CreateAsync(portal, itemId);
                    webMap = new Map(item);
                }
                else
                {
                    var portal = await ArcGISPortal.CreateAsync();
                    var item = await PortalItem.CreateAsync(portal, mapIdOrUrl);
                    webMap = new Map(item);
                }

                await webMap.LoadAsync();
                MyMapView.Map = webMap;
                UpdateStatus($"Web Map 加载成功: {webMap.Item?.Title ?? "未命名"}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载 Web Map 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Web Map 加载失败");
            }
        }
        #endregion
    }

    public class CityLocation
    {
        public string Name { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public string Description { get; set; }
        public CityLocation(string name, double longitude, double latitude, string description = "")
        { Name = name; Longitude = longitude; Latitude = latitude; Description = description; }
    }
}