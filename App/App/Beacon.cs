using System;
using System.Collections.Generic;
using System.Diagnostics; // Debug
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;

namespace ImageSharing.Beacon
{
    public class Beacon
    {
        private Boolean _isWhiteBoard;
        public Boolean IsWhiteBoard
        {
            get { return _isWhiteBoard; }
            set
            {
                _isWhiteBoard = value;
            }
        }
        private String _macAdd;
        public String MacAddr
        {
            get { return _macAdd; }
            set
            {
                _macAdd = value;
            }
        }

        private DeviceInformation _wifiP2pDevice;
        public DeviceInformation WifiP2pDevice
        {
            get { return _wifiP2pDevice; }
            set
            {
                _wifiP2pDevice = value;
            }
        }

        private ulong _bluetoothAddress;
        public ulong BluetoothAddress
        {
            get { return _bluetoothAddress; }
            set
            {
                if (_bluetoothAddress == value) return;
                _bluetoothAddress = value;
            }
        }

        private short _rssi;
        public short Rssi
        {
            get { return _rssi; }
            set
            {
                if (_rssi == value) return;
                _rssi = value;
            }
        }

        private DateTimeOffset _timestamp;
        public DateTimeOffset Timestamp
        {
            get { return _timestamp; }
            set
            {
                if (_timestamp == value) return;
                _timestamp = value;
            }
        }

        public Beacon(BluetoothLEAdvertisementReceivedEventArgs btAdv)
        {
            BluetoothAddress = btAdv.BluetoothAddress;
            UpdateBeacon(btAdv);
        }

        public void UpdateBeacon(BluetoothLEAdvertisementReceivedEventArgs btAdv)
        {
            if (btAdv == null) return;

            if (btAdv.BluetoothAddress != BluetoothAddress)
            {
                //throw new BeaconException("Bluetooth address of beacon does not match - not updating beacon information");
            }

            Rssi = btAdv.RawSignalStrengthInDBm;
            Timestamp = btAdv.Timestamp;

            //Debug.WriteLine($"Beacon advertisment detected (Strength: {Rssi}): Address: {BluetoothAddress}");

            // Check if beacon advertisement contains any actual usable data
            if (btAdv.Advertisement == null)
            {
                //Debug.WriteLine("btAdv.Advertisement == null");
                return;
            }

            if (btAdv.Advertisement.ServiceUuids.Any())
            {
                foreach (var serviceUuid in btAdv.Advertisement.ServiceUuids)
                {
                    //Debug.WriteLine("Service UUIDs:" + serviceUuid);
                }
            }
            else
            {
                //Debug.WriteLine("Bluetooth LE device does not send Service UUIDs");
            }

            // Manufacturer data - currently unused
            if (btAdv.Advertisement.ManufacturerData.Any())
            {
                foreach (var manufacturerData in btAdv.Advertisement.ManufacturerData)
                {
                    // Print the company ID + the raw data in hex format
                    //var manufacturerDataString = $"0x{manufacturerData.CompanyId.ToString("X")}: {BitConverter.ToString(manufacturerData.Data.ToArray())}";
                    //Debug.WriteLine("Manufacturer data: " + manufacturerDataString);
                    var manufacturerDataArry = manufacturerData.Data.ToArray();
                    // [+]Ryan
                    /*
                    Debug.Write("manufacturerDataArry : ");
                    foreach (var temp in manufacturerDataArry)
                    {
                        Debug.Write(temp + " ");
                    }
                    Debug.WriteLine("");
                    */

                    if (manufacturerDataArry[0] == 2 && manufacturerDataArry[1] == 21 &&
                        manufacturerDataArry[2] == 0 && manufacturerDataArry[3] == 0 &&
                        manufacturerDataArry[4] == 0 && manufacturerDataArry[5] == 0 &&
                        manufacturerDataArry[18] == 0 && manufacturerDataArry[19] == 1 &&
                        manufacturerDataArry[20] == 0 && manufacturerDataArry[21] == 3 &&
                        manufacturerDataArry[22] == 204)
                    {
                        String macAdd = "";
                        for (int i = 0; i < 12; i++)
                        {
                            char temp = (char)manufacturerDataArry[i + 6];
                            if (i > 0 && i % 2 == 0)
                            {
                                macAdd = macAdd + ":";
                            }
                            macAdd = macAdd + temp;
                        }

                        IsWhiteBoard = true;
                        MacAddr = macAdd;

                        //Debug.WriteLine("This beacon is QisdaBeacon");
                        //Debug.WriteLine("macAdd : " + macAdd);
                    }
                    // [-]Ryan
                }
            }
        }
    }
}
