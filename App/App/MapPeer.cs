/* ----------------------------------------------- 
 * Copyright © 2013 Esri Inc. All Rights Reserved. 
 * ----------------------------------------------- */

using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace App {
    public class MapPeer {
        public PeerInformation PeerInformation { get; set; }
        public StreamSocket StreamSocket { get; set; }
        public DataWriter DataWriter { get; set; }
        public DataReader DataReader { get; set; }
    }
}
