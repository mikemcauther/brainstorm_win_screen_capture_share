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
            

            // Construct the Universal Bluetooth Beacon manager
            _beaconManager = new BeaconManager();
            _beaconManager.DevicesAvailable += new EventHandler(onBleDevicesAvailable);

            _beaconManager.startScan();
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

        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _beaconManager.Dispose();
        }

        private void onWifiDevicesAvailable(object sender, EventArgs e) {
            if(wifiDirectDeviceController.isConnectingWifiP2p()) {
                return;
            } 
            updateUIList();
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

            CoreApplication.Exit();
        }

        private void onBleDevicesAvailable(object sender, EventArgs e) {
            if(wifiDirectDeviceController.isConnectingWifiP2p()) {
                return;
            } 

            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                updateUIList();
            });
        }

        async void Connect(object sender, RoutedEventArgs e)
        {
            tryConnect();
        }

        private void updateUIList()
        {
            // Clear list
            FoundDevicesList.Items.Clear();

            if (_beaconManager.BluetoothBeacons.Count == 0)
            {
                this.NotifyUser("No Beacon found.", NotifyType.StatusMessage);
            }
            else
            {
                for(var i = 0; i < _beaconManager.BluetoothBeacons.Count; i ++)
                {
                    Beacon beacon = _beaconManager.BluetoothBeacons[i];
                    if(beacon == null)
                    {
                        continue;
                    }
                    var matchedWifiDevice = wifiDirectDeviceController.findMatchedDevice(beacon.MacAddr);

                    if(matchedWifiDevice == null) {
                        FoundDevicesList.Items.Add("Unknown Wifi Mac");
                    } else {
                        FoundDevicesList.Items.Add(matchedWifiDevice.Name);
                        beacon.WifiP2pDevice = matchedWifiDevice;
                    }
                }
                    
                FoundDevicesList.SelectedIndex = 0;
                if(FoundDevicesList.Items.Count == 1)
                {
                    tryConnect();
                }
            }
        }

        private async void tryConnect()
        {
            // If nothing is selected, return
            if (FoundDevicesList.SelectedIndex == -1)
            {
                this.NotifyUser("Please select a device", NotifyType.StatusMessage);
                return;
            }
            else
            {
                Beacon beacon = _beaconManager.BluetoothBeacons[FoundDevicesList.SelectedIndex];
                await wifiDirectDeviceController.Connect(beacon.WifiP2pDevice);
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

    }

    public enum NotifyType
    {
        StatusMessage,
        KeepMessage,
        ErrorMessage
    };
}
