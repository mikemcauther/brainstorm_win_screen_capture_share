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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace App
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private WiFiDirectDeviceController wifiDirectDeviceController;
        private MapPeer mapPeer;
        public int WIFI_DIRECT_SERVER_SOCKET_PORT = 8988;

        private bool isWifiDirectSupported = false;
        public MainPage()
        {
            this.InitializeComponent();

            // Check if wifi-direct is support
            isWifiDirectSupported = (PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Browse) == PeerDiscoveryTypes.Browse;

            // Clear list
            FoundDevicesList.Items.Clear();

            // Register Wifi direct listener
            wifiDirectDeviceController = new WiFiDirectDeviceController(this);
            wifiDirectDeviceController.DevicesAvailable += new EventHandler(onWifiDevicesAvailable);
            wifiDirectDeviceController.DeviceConnected += new EventHandler(onWifiDirectConnected);
            wifiDirectDeviceController.DeviceDisConnected += new EventHandler(onWifiDirectDisConnected);

            // Get wifi direct devices list
            wifiDirectDeviceController.GetDevices();

        }

        private void onWifiDevicesAvailable(object sender, EventArgs e) {
            WiFiDirectDeviceController controller = (WiFiDirectDeviceController)sender;
            DeviceInformationCollection devInfoCollection = controller.devInfoCollection;

            FoundDevicesList.SelectedIndex = 0;
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
            }
        }

        private async void onWifiDirectConnected(object sender, EventArgs e) {
            WiFiDirectDeviceController controller = (WiFiDirectDeviceController)sender;
            Windows.Devices.WiFiDirect.WiFiDirectDevice wfdDevice = controller.wfdDevice;
            EndpointPair endpointPair = null;

            // Get the EndpointPair collection
            var EndpointPairCollection = controller.wfdDevice.GetConnectionEndpointPairs();
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
        }

        private void onWifiDirectDisConnected(object sender, EventArgs e) {
            this.NotifyUser("WiFiDirect device disconnected", NotifyType.ErrorMessage);

            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Clear the FoundDevicesList
                FoundDevicesList.Items.Clear();
            });

            // Close Socket
            this.ClosePeer(mapPeer);

            this.NotifyUser("DisConnection succeeded", NotifyType.StatusMessage);
        }

        async void Connect(object sender, RoutedEventArgs e)
        {
            this.NotifyUser("", NotifyType.ErrorMessage);

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

            wifiDirectDeviceController.wfdDevice.Dispose();
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

            await this.PeerConnect(clientSocket);
        }

        private async Task PeerConnect(StreamSocket socket) {
            // Store socket
            DataWriter writer = new DataWriter(socket.OutputStream);
            DataReader reader = new DataReader(socket.InputStream);

            mapPeer = new MapPeer() {
                StreamSocket = socket,
                DataReader = reader,
                DataWriter = writer
            };

            // Commence send/recieve loop
            //await this.PeerReceive(mapPeer);
        }

        /*
        private async Task PeerReceive(MapPeer peer) {
            try {
                // Get body size
                uint bytesRead = await peer.DataReader.LoadAsync(sizeof(uint));
                if (bytesRead == 0) {
                    await this.RemovePeer(peer);
                    return;
                }
                uint length = peer.DataReader.ReadUInt32();

                // Get message type
                uint bytesRead1 = await peer.DataReader.LoadAsync(sizeof(uint));
                if (bytesRead1 == 0) {
                    await this.RemovePeer(peer);
                    return;
                }
                uint type = peer.DataReader.ReadUInt32();
                MessageType messageType = (MessageType)Enum.Parse(typeof(MessageType), type.ToString());

                // Get body
                uint bytesRead2 = await peer.DataReader.LoadAsync(length);
                if (bytesRead2 == 0) {
                    await this.RemovePeer(peer);
                    return;
                }

                // Get message
                string message = peer.DataReader.ReadString(length);

                // Process message
                switch (messageType) {
                    case MessageType.PanningExtent:
                        await this.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal,
                            () => {
                                Envelope env = (Envelope)Envelope.FromJson(message);
                                this.ProcessExtent(env);
                            }
                        );
                        break;
                    case MessageType.Ink:
                        await this.Dispatcher.RunAsync(
                            CoreDispatcherPriority.Normal,
                            () => {
                                MapInkLine line = MapInkLine.FromJson(message);
                                this.ProcessInk(line);
                            }
                        );
                        break;
                }
                // Wait for next message
                await this.PeerReceive(peer);
            }
            catch (Exception ex) {
                //Debug.WriteLine("Reading from socket failed: " + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
                //this.RemovePeer(peer);
            }
        }
        */

        private void ClosePeer(MapPeer peer) {
            if (peer.StreamSocket != null) {
                peer.StreamSocket.Dispose();
                peer.StreamSocket = null;
            }
            if (peer.DataWriter != null) {
                peer.DataWriter.Dispose();
                peer.DataWriter = null;
            }
            if (peer.DataReader != null) {
                peer.DataReader.Dispose();
                peer.DataReader = null;
            }
        }

        public void Dispose() {
            if(mapPeer != null) {
                this.ClosePeer(mapPeer);
                mapPeer = null;
            }
        }

        public async  void NotifyUser(string strMessage, NotifyType type)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (type)
                {
                    // Use the status message style.
                    case NotifyType.StatusMessage:
                        StatusBlock.Style = Resources["StatusStyle"] as Style;
                        break;
                    // Use the error message style.
                    case NotifyType.ErrorMessage:
                        StatusBlock.Style = Resources["ErrorStyle"] as Style;
                        break;
                }
                StatusBlock.Text = "\n" + strMessage;
            });
        }
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };
}
