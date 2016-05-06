using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ImageSharing.WiFiDirect;
using Windows.Networking;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.ApplicationModel.Core;

//Beacon
using ImageSharing.Beacon;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

using System.Diagnostics; // Debug

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace App
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private WiFiDirectDeviceController wifiDirectDeviceController;
        private SocketReaderWriter socketRW;
        public int WIFI_DIRECT_SERVER_SOCKET_PORT = 8988;
        private string filePath;
        private StorageFile storageFile;

        // Bluetooth Beacons
        private readonly BluetoothLEAdvertisementWatcher _watcher;
        private readonly BeaconManager _beaconManager;

        private bool isWifiDirectSupported = false;
        public MainPage()
        {
            this.InitializeComponent();

            // Check if wifi-direct is support
            isWifiDirectSupported = (PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Browse) == PeerDiscoveryTypes.Browse;

            // Register Wifi direct listener
            wifiDirectDeviceController = new WiFiDirectDeviceController(this);
            wifiDirectDeviceController.DevicesAvailable += new EventHandler(onWifiDevicesAvailable);
            wifiDirectDeviceController.DeviceConnected += new EventHandler(onWifiDirectConnected);
            wifiDirectDeviceController.DeviceDisConnected += new EventHandler(onWifiDirectDisConnected);

            // Get wifi direct devices list
            wifiDirectDeviceController.GetDevices();

            
            // Create the Bluetooth LE watcher from the Windows 10 UWP
            _watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };

            // Construct the Universal Bluetooth Beacon manager
            _beaconManager = new BeaconManager();

        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            filePath = e.Parameter as string;

            string str = e.Parameter as string;
            // Parse file path pattern : bswdprotocol:C:/Users/bon/Desktop/MyScreenCapture/QOPCTYPU-0.png
            //index after bswdprotocol:
            //filePath = str.Substring(str.IndexOf("bswdprotocol:") + 13);
            this.NotifyUser("got file path = " + filePath, NotifyType.KeepMessage);

            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");
            storageFile = await openPicker.PickSingleFileAsync();
            if (storageFile != null)
            {
                // Application now has read/write access to the picked file
                this.NotifyUser("got file path = " + storageFile.Name, NotifyType.KeepMessage);
            }
            else
            {
                this.NotifyUser("Get file Operation cancelled.", NotifyType.StatusMessage);
            }

            // Start ble watching
            _watcher.Received += WatcherOnReceived;
            _watcher.Stopped += WatcherOnStopped;
            _watcher.Start();
            if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            {
                //SetStatusOutput(_resourceLoader.GetString("WatchingForBeacons"));
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _watcher.Stop();
            _watcher.Received -= WatcherOnReceived;
        }

        private void onWifiDevicesAvailable(object sender, EventArgs e) {
            WiFiDirectDeviceController controller = (WiFiDirectDeviceController)sender;
            DeviceInformationCollection devInfoCollection = controller.devInfoCollection;

            // Clear list
            FoundDevicesList.Items.Clear();

            if (devInfoCollection.Count == 0)
            {
                this.NotifyUser("No WiFiDirect devices found.", NotifyType.StatusMessage);
            }
            else
            {
                foreach (var devInfo in devInfoCollection)
                {
                    FoundDevicesList.Items.Add(devInfo.Name);
                }
                FoundDevicesList.SelectedIndex = 0;
            }
        }

        private async void onWifiDirectConnected(object sender, EventArgs e) {
            WiFiDirectDeviceController controller = (WiFiDirectDeviceController)sender;
            Windows.Devices.WiFiDirect.WiFiDirectDevice wfdDevice = controller.wfdDevice;
            EndpointPair endpointPair = null;

            // Get the EndpointPair collection
            var EndpointPairCollection = wfdDevice.GetConnectionEndpointPairs();
            if (EndpointPairCollection.Count > 0)
            {
                endpointPair = EndpointPairCollection[0];
            }
            else
            {
                return;
            }

            PCIpAddress.Text = "PC's IP Address: " + endpointPair.LocalHostName.ToString();
            DeviceIpAddress.Text =  "Device's IP Address: " + endpointPair.RemoteHostName.ToString();

            await this.ConnectToPeers(endpointPair);
            this.NotifyUser("Connection succeeded", NotifyType.StatusMessage);

            await socketRW.WritePng(storageFile);
            // Close Socket after sending one png file
            socketRW.Dispose();

            CoreApplication.Exit();
        }

        private void onWifiDirectDisConnected(object sender, EventArgs e) {
            WiFiDirectDeviceController controller = (WiFiDirectDeviceController)sender;
            this.NotifyUser("WiFiDirect device disconnected", NotifyType.ErrorMessage);

            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Clear the FoundDevicesList
                FoundDevicesList.Items.Clear();
            });

            // Close Socket
            if(socketRW != null) {
                socketRW.Dispose();
                socketRW = null;
            }

            this.NotifyUser("DisConnection succeeded", NotifyType.StatusMessage);

            // Scan again
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                controller.GetDevices();
            });
        }

        async void Connect(object sender, RoutedEventArgs e)
        {
            // If nothing is selected, return
            if (FoundDevicesList.SelectedIndex == -1)
            {
                this.NotifyUser("Please select a device", NotifyType.StatusMessage);
                return;
            }
            else
            {
                await wifiDirectDeviceController.Connect(FoundDevicesList.SelectedIndex);
            }
        }

        void Disconnect(object sender, RoutedEventArgs e)
        {
            this.NotifyUser("WiFiDirect device disconnected.", NotifyType.StatusMessage);

            if(wifiDirectDeviceController.wfdDevice != null) {
                wifiDirectDeviceController.wfdDevice.Dispose();
            }
        }

        private async Task ConnectToPeers(EndpointPair endpointPair) {
            // Connect to remote peer
            StreamSocket clientSocket = new StreamSocket();
            try {
                await clientSocket.ConnectAsync(endpointPair.RemoteHostName, WIFI_DIRECT_SERVER_SOCKET_PORT.ToString());
            }
            catch (Exception ex) {
                //Debug.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
                this.NotifyUser(ex.Message + Environment.NewLine + ex.StackTrace, NotifyType.StatusMessage);
            }
            if (clientSocket == null) {
                //await ProximityMapEnvironment.Default.Log("Connection failed", true);
                return;
            }

            socketRW = new SocketReaderWriter(clientSocket,this);
        }

        public void Dispose() {
            this.NotifyUser("App Will be closed", NotifyType.KeepMessage);
            if(socketRW != null) {
                socketRW.Dispose();
                socketRW = null;
            }
            if(wifiDirectDeviceController.wfdDevice != null) {
                wifiDirectDeviceController.wfdDevice.Dispose();
            }
        }

        public async  void NotifyUser(string strMessage, NotifyType type)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch(type){
                    case NotifyType.StatusMessage:
                    case NotifyType.ErrorMessage:
                        StatusBlock.Text = "\n" + strMessage;
                        break;
                    case NotifyType.KeepMessage:
                        StatusBlockForKeep.Text = "\n" + strMessage;
                        break;
                }
            });
        }

        //[+] Ryan, Beacon
        void UpdateBeacon(object sender, RoutedEventArgs e)
        {
            // Clear list
            BeaconsList.Items.Clear();

            if (_beaconManager.BluetoothBeacons.Count == 0)
            {
                this.NotifyUser("No Beacon found.", NotifyType.StatusMessage);
            }
            else
            {
                foreach (var beacon in _beaconManager.BluetoothBeacons)
                {
                    BeaconsList.Items.Add(beacon.BluetoothAddress);
                }
                BeaconsList.SelectedIndex = 0;
            }
        }

        void ConnectBeacon(object sender, RoutedEventArgs e)
        {
            //int selectedIndex = BeaconsList.SelectedIndex;
            Object selectedItem = BeaconsList.SelectedItem;
            foreach (var beacon in _beaconManager.BluetoothBeacons)
            {
                if (beacon.BluetoothAddress.Equals(selectedItem))
                {
                    Debug.WriteLine("BluetoothAddress:" + beacon.BluetoothAddress);
                    Debug.WriteLine("MacAdd:" + beacon.MacAdd);
                    BeaconContent.Text = beacon.MacAdd;
                }
            }
        }

        private async void WatcherOnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    _beaconManager.ReceivedAdvertisement(eventArgs);
                }
                catch (ArgumentException e)
                {
                    // Ignore for real-life scenarios.
                    // In some very rare cases, analyzing the data can result in an
                    // Argument_BufferIndexExceedsCapacity. Ignore the error here,
                    // assuming that the next received frame advertisement will be
                    // correct.
                    Debug.WriteLine(e);
                }
            });
            
        }

        private void WatcherOnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            string errorMsg = null;
            if (args != null)
            {
                switch (args.Error)
                {
                    case BluetoothError.Success:
                        errorMsg = "WatchingSuccessfullyStopped";
                        break;
                    case BluetoothError.RadioNotAvailable:
                        errorMsg = "ErrorNoRadioAvailable";
                        break;
                    case BluetoothError.ResourceInUse:
                        errorMsg = "ErrorResourceInUse";
                        break;
                    case BluetoothError.DeviceNotConnected:
                        errorMsg = "ErrorDeviceNotConnected";
                        break;
                    case BluetoothError.DisabledByPolicy:
                        errorMsg = "ErrorDisabledByPolicy";
                        break;
                    case BluetoothError.NotSupported:
                        errorMsg = "ErrorNotSupported";
                        break;
                }
            }
            if (errorMsg == null)
            {
                // All other errors - generic error message
            }
        }
        //[-] Ryan, Beacon
    }

    public enum NotifyType
    {
        StatusMessage,
        KeepMessage,
        ErrorMessage
    };
}
