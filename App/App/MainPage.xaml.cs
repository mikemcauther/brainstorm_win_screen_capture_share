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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace App
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly ObservableCollection<MapPeer> _peers = new ObservableCollection<MapPeer>();
        public MainPage()
        {
            this.InitializeComponent();
            // Toggles - checked
            this.ToggleButtonConnect.Checked += this.ToggleButton_Checked;
            // Toggles - unchecked
            this.ToggleButtonConnect.Unchecked += this.ToggleButton_Unchecked;
        }
        private async void ToggleButton_Checked(object sender, RoutedEventArgs e) {
            if (sender == this.ToggleButtonConnect) {
                // Open toast
                //this.PopupNotifications.IsOpen = true;

                // Check if wifi-direct is support
                bool supported = (PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Browse) == PeerDiscoveryTypes.Browse;
                if (!supported) {
                    //await ProximityMapEnvironment.Default.Log("This device does not supported Wifi Direct", false);
                    await Task.Delay(2000);
                    //this.PopupNotifications.IsOpen = false;
                    return;
                }
                // Start listening for proximate peers
                PeerFinder.Start();

                // Be available for future connections
                PeerFinder.ConnectionRequested += this.PeerConnectionRequested;

                // Find peers
                await this.ConnectToPeers();
            }
            
        }
        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e) {
            if (sender == this.ToggleButtonConnect) {
                // Start listening for proximate peers
                PeerFinder.Stop();

                // Be available for future connections
                PeerFinder.ConnectionRequested -= this.PeerConnectionRequested;

                // Dispose of connection and reconnect
                this.Dispose();

                // Update toast
                //await ProximityMapEnvironment.Default.Log("Disconnecting...", true);
                //this.PopupNotifications.IsOpen = false;
            }
        }
        private async Task ConnectToPeers() {
            // Find peers
            //await ProximityMapEnvironment.Default.Log("Search for peers...", true);
            IReadOnlyList<PeerInformation> peers = await PeerFinder.FindAllPeersAsync();

            // No peers found?
            if (peers == null || peers.Count == 0) { return; }

            // Connect to each peer
            foreach (PeerInformation peer in peers) {
                // Log
                //await ProximityMapEnvironment.Default.Log(string.Format("Connecting to: {0}", peer.DisplayName), true);

                // Connect to remote peer
                StreamSocket socket = null;
                try {
                    socket = await PeerFinder.ConnectAsync(peer);
                }
                catch (Exception ex) {
                    //Debug.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
                }
                if (socket == null) {
                    //await ProximityMapEnvironment.Default.Log("Connection failed", true);
                    continue;
                }

                await this.PeerConnect(peer, socket);
            }
        }
        private async void PeerConnectionRequested(object sender, ConnectionRequestedEventArgs e) {
            try {
                // Log
                //await ProximityMapEnvironment.Default.Log(string.Format("Connecting to: {0}", e.PeerInformation.DisplayName), true);

                // Get socket
                StreamSocket socket = null;
                try {
                    socket = await PeerFinder.ConnectAsync(e.PeerInformation);
                }
                catch (Exception ex) {
                    //Debug.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
                }

                if (socket == null) {
                    await Task.Delay(TimeSpan.FromSeconds(1d));
                    //await ProximityMapEnvironment.Default.Log("Search for peers...", true);
                    return;
                }

                // Accept connection
                await this.PeerConnect(e.PeerInformation, socket);
            }
            catch (Exception ex) {
                //Debug.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }
        private async Task PeerConnect(PeerInformation peer, StreamSocket socket) {
            // Store socket
            DataWriter writer = new DataWriter(socket.OutputStream);
            DataReader reader = new DataReader(socket.InputStream);

            MapPeer mapPeer = new MapPeer() {
                PeerInformation = peer,
                StreamSocket = socket,
                DataReader = reader,
                DataWriter = writer
            };
            this._peers.Add(mapPeer);

            // Listening
            //await ProximityMapEnvironment.Default.Log("Listening...", true);

            // Commence send/recieve loop
            await this.PeerReceive(mapPeer);
        }
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
                /*
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
                */
                // Wait for next message
                await this.PeerReceive(peer);
            }
            catch (Exception ex) {
                //Debug.WriteLine("Reading from socket failed: " + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
                //this.RemovePeer(peer);
            }
        }
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
        private async Task RemovePeer(MapPeer peer) {
            this.ClosePeer(peer);
            if (this._peers.Contains(peer)) {
                this._peers.Remove(peer);
            }

            //
            //await ProximityMapEnvironment.Default.Log("Search for peers...", true);
        }
        public void Dispose() {
            foreach (MapPeer peer in this._peers) {
                this.ClosePeer(peer);
            }
            this._peers.Clear();
        }
    }
}
