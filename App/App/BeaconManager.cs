using System.Collections.ObjectModel;
using Windows.Devices.Bluetooth.Advertisement;

namespace ImageSharing.Beacon
{
    public class BeaconManager
    {
        public ObservableCollection<Beacon> BluetoothBeacons { get; set; } = new ObservableCollection<Beacon>();

        public void ReceivedAdvertisement(BluetoothLEAdvertisementReceivedEventArgs btAdv)
        {
            if (btAdv == null) return;

            // Check if we already know this bluetooth address
            foreach (var bluetoothBeacon in BluetoothBeacons)
            {
                if (bluetoothBeacon.BluetoothAddress == btAdv.BluetoothAddress)
                {
                    // We already know this beacon
                    // Update / Add info to existing beacon
                    bluetoothBeacon.UpdateBeacon(btAdv);
                    return;
                }
            }

            // Beacon was not yet known - add it to the list.
            var newBeacon = new Beacon(btAdv);

            if (newBeacon.IsWhiteBoard == true)
            {
                BluetoothBeacons.Add(newBeacon);
            }
        }
    }
}
