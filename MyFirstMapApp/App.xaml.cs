using System;
using System.Windows;
using Esri.ArcGISRuntime;

namespace MyFirstMapApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                // 设置 API Key（硬编码确保正确）
                string apiKey = "AAPTaHMNa0MN4o4so22wXNOf2sA..O2MK3rHy6tqM87UzMvBqvy3Bp0UFFvdnNiOBhU6RF1GGGSZeyQFabvS22MbKR0-qmvq6wjPdRiQqvgQxXVmcJ1-L2aN2IZQI-L0P_V9bQwx1XnOwdcf5-n30Y4LrLy9Wl87tJYHTzFl9is6WAUwosQi12pfPUxxTDkmm7pFU9PpVlWx4dEYrVW6o2xLsnq2YRqVLsRpPsLospvvLJPVi_XJ9GBi_HY07cGhGd_Lonf40wAqjOxQyAT1_FL6wdkLV";
                ArcGISRuntimeEnvironment.ApiKey = apiKey;
                ArcGISRuntimeEnvironment.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ArcGIS Runtime 初始化失败: {ex.Message}", "错误");
                Current.Shutdown();
            }
        }
    }
}