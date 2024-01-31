using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Timers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using WinRT.Interop;
using System.Net.Http;
using WinUIEx.Messaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LocationProvider
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private bool isStarted = false;
        private Coordinate coord;
        private Coordinate lastSentCoord;
        private readonly WindowMessageMonitor monitor;
        private readonly SimConnectManager simConnectManager;
        private readonly Timer deviceUpdateTimer;

        // For device update only
        private readonly HttpClient deviceUpdateClient;

        public MainWindow()
        {
            this.InitializeComponent();

            Title = "Location Provider";
            IntPtr hWnd = GetWindowHandle();
            WinUIEx.HwndExtensions.SetIcon(hWnd, @"lp-icon.ico");
            AppWindow.Resize(new SizeInt32(1200, 800));
            ExtendsContentIntoTitleBar = true;

            startButton.Style = (Style)rootPanel.Resources["AccentButtonStyle"];
            startButton.Content = "Start";

            monitor = new WindowMessageMonitor(this);
            monitor.WindowMessageReceived += OnWindowMessageReceived;

            simConnectManager = new SimConnectManager();
            simConnectManager.SetWindowHandle(hWnd);
            simConnectManager.DataUpdated += SimConnectManager_DataUpdated;
            simConnectManager.DataTransmitTerminated += SimConnectManager_DataTransmitTerminated;

            deviceUpdateTimer = new Timer();
            deviceUpdateTimer.Elapsed += DeviceUpdateTimer_Elapsed;
            deviceUpdateTimer.Interval = 2000;
            deviceUpdateTimer.Start();

            deviceUpdateClient = new HttpClient { Timeout = TimeSpan.FromSeconds(0.3) };
        }

        private void SimConnectManager_DataUpdated(SimConnectManager.RetrievedData data)
        {
            coord = new Coordinate(data.latitude, data.longitude);
            Log.Information("Coordinate updated: " + coord.Latitude + ", " + coord.Longitude);
        }

        private async void SimConnectManager_DataTransmitTerminated()
        {
            startButton.IsEnabled = false;
            Log.Information("SimConnect transmit terminated");
            await ShowMessage("Information", "Simulator closed. Device location will be restored.");
            await SendOffToServer();
            ResetLayout();
        }

        private async void DeviceUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!isStarted || !simConnectManager.IsReceiving)
            {
                return;
            }

            var targetCoord = coord;

            if (targetCoord == lastSentCoord)
            {
                // Generate an offset between -0.00001 and 0.00001
                Random r = new();
                double latOffset = -0.00001 + r.NextDouble() * 0.00002;
                double lonOffset = -0.00001 + r.NextDouble() * 0.00002;
                targetCoord = new Coordinate(coord.Latitude + latOffset, coord.Longitude + lonOffset);
            }

            string strLat = targetCoord.Latitude.ToString("0.000000");
            string strLon = targetCoord.Longitude.ToString("0.000000");
            lastSentCoord = targetCoord;

            try
            {
                await GetAsync(deviceUpdateClient, $"http://127.0.0.1:12924/set?lat={strLat}&lon={strLon}");
                Log.Information($"Sent {strLat}, {strLon}");
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to send {strLat}, {strLon}: {ex.Message}");
            }
        }

        private IntPtr GetWindowHandle()
        {
            return WindowNative.GetWindowHandle(this);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isStarted)
            {
                startButton.Style = null;
                startButton.IsEnabled = false;

                try
                {
                    simConnectManager.Connect();
                    Log.Information("Connected to SimConnect");
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to setup SimConnect: " + ex.Message);
                    await ShowMessage("Error", "Error occurred when connecting to SimConnect.\n\n" + "Message: " + ex.Message);
                    ResetLayout();
                    return;
                }

                try
                {
                    var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    await GetAsync(client, "http://127.0.0.1:12924/on");
                    Log.Information("Sent /on");
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to send /on: " + ex.Message);
                    await ShowMessage("Error", "Error occurred when sending setup request.\n\n" + "Message: " + ex.Message);
                    simConnectManager.Disconnect();
                    ResetLayout();
                    return;
                }

                startButton.Content = "Stop";
                startButton.IsEnabled = true;
                isStarted = true;
            }
            else
            {
                startButton.IsEnabled = false;
                await SendOffToServer();
                simConnectManager.Disconnect();
                ResetLayout();
            }
        }

        private async void OnWindowMessageReceived(object sender, WindowMessageEventArgs e)
        {
            try
            {
                if (e.Message.MessageId == simConnectManager.GetUserSimConnectWinEvent())
                {
                    simConnectManager.ReceiveSimConnectMessage();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Caught an exception when handling a window message: " + ex.Message);
                startButton.IsEnabled = false;
                await SendOffToServer();
                simConnectManager.Disconnect();
                ResetLayout();
            }
        }

        private async Task SendOffToServer()
        {
            try
            {
                var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                await GetAsync(client, "http://127.0.0.1:12924/off");
                Log.Information("Sent /off");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to send /off: " + ex.Message);
                await ShowMessage("Error", "Error occurred when sending cleanup request.\n" +
                    "Device location may not be restored.\n\n" +
                    "Message: " + ex.Message);
            }
        }

        private async Task ShowMessage(string title, string message)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = this.Content.XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };

            await dialog.ShowAsync();
        }

        private void ResetLayout()
        {
            isStarted = false;
            startButton.Style = (Style)rootPanel.Resources["AccentButtonStyle"];
            startButton.Content = "Start";
            startButton.IsEnabled = true;
        }

        public async Task<string> GetAsync(HttpClient client, string url)
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            return await response.EnsureSuccessStatusCode()
                .Content.ReadAsStringAsync();
        }
    }
}
