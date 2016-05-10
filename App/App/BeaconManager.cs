using System;
using System.Collections.ObjectModel;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.UI.Core;

namespace ImageSharing.Beacon
{
    public class BeaconManager
    {
        public event EventHandler DevicesAvailable;
        public ObservableCollection<Beacon> BluetoothBeacons { get; set; } = new ObservableCollection<Beacon>();
       
        private readonly BluetoothLEAdvertisementWatcher _watcher;

        public BeaconManager()
        {
            // Create the Bluetooth LE watcher from the Windows 10 UWP
            _watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };

            // Start ble watching
            _watcher.Received += WatcherOnReceived;
            _watcher.Stopped += WatcherOnStopped;
            if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            {
                //SetStatusOutput(_resourceLoader.GetString("WatchingForBeacons"));
            }
        }

        public void startScan()
        {
            _watcher.Start();
        }


        private void AddNewBleAdv(BluetoothLEAdvertisementReceivedEventArgs btAdv)
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

        private void WatcherOnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            try
            {
                AddNewBleAdv(eventArgs);
            }
            catch (ArgumentException e)
            {
                // Ignore for real-life scenarios.
                // In some very rare cases, analyzing the data can result in an
                // Argument_BufferIndexExceedsCapacity. Ignore the error here,
                // assuming that the next received frame advertisement will be
                // correct.
                //Debug.WriteLine(e);
            }
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
            DevicesAvailable(this,EventArgs.Empty);
        }
        //[-] Ryan, Beacon

        public void Dispose()
        {
            _watcher.Stop();
            _watcher.Received -= WatcherOnReceived;
            _watcher.Stopped -= WatcherOnStopped;
        }
    }
}
