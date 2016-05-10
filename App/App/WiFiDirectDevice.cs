//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using App;
using System.Threading.Tasks;

namespace ImageSharing.WiFiDirect
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WiFiDirectDeviceController
    {
        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as rootPage.NotifyUser()
        private MainPage rootPage;        
        public event EventHandler DevicesAvailable;
        public event EventHandler DeviceConnected;
        public event EventHandler DeviceDisConnected;
        public DeviceInformationCollection devInfoCollection;
        public Windows.Devices.WiFiDirect.WiFiDirectDevice wfdDevice;

        public WiFiDirectDeviceController(MainPage mainPage)
        {
            rootPage = mainPage;
            wfdDevice = null;
        }

        // This gets called when we receive a disconnect notification
        private void DisconnectNotification(object sender, object arg)
        {
            rootPage.NotifyUser("WiFiDirect device disconnected", NotifyType.ErrorMessage);

            devInfoCollection = null;
            wfdDevice = null;
            DeviceDisConnected(this,EventArgs.Empty);
        }

        public bool isConnectingWifiP2p()
        {
            if(wfdDevice != null) {
                return  true;
            } else {
                return  false;
            }
        }

        public DeviceInformation findMatchedDevice(String wifiP2pMac)
        {
            foreach (var devInfo in devInfoCollection)
            {
                if (devInfo.Id.ToUpper().Contains(wifiP2pMac.ToUpper()))
                {
                    return devInfo;
                }
            }
            return null;
        }

        public async Task Connect(DeviceInformation chosenDevInfo)
        {
            rootPage.NotifyUser("", NotifyType.ErrorMessage);
            try
            {
                rootPage.NotifyUser("Connecting to " + chosenDevInfo.Name + "(" + chosenDevInfo.Id + ")" + "....", NotifyType.StatusMessage);

                // Set connect config 
                WiFiDirectConnectionParameters parameter = new WiFiDirectConnectionParameters();
                parameter.PreferredPairingProcedure = WiFiDirectPairingProcedure.Invitation;
                parameter.GroupOwnerIntent = 0;

                // Connect to the selected WiFiDirect device
                wfdDevice = await Windows.Devices.WiFiDirect.WiFiDirectDevice.FromIdAsync(chosenDevInfo.Id,parameter);

                if (wfdDevice == null)
                {
                    rootPage.NotifyUser("Connection to " + chosenDevInfo.Name + " failed.", NotifyType.StatusMessage);
                    return;
                }

                // Register for Connection status change notification
                wfdDevice.ConnectionStatusChanged += new TypedEventHandler<Windows.Devices.WiFiDirect.WiFiDirectDevice, object>(DisconnectNotification);     

                DeviceConnected(this,EventArgs.Empty);
            }
            catch (Exception err)
            {
                rootPage.NotifyUser("Connection to " + chosenDevInfo.Name + " failed: " + err.Message, NotifyType.ErrorMessage);
            }
        }

        public async Task GetDevices()
        {                   
            try
            {
                rootPage.NotifyUser("Enumerating WiFiDirect devices...", NotifyType.StatusMessage);
                devInfoCollection = null;

                String deviceSelector = Windows.Devices.WiFiDirect.WiFiDirectDevice.GetDeviceSelector(WiFiDirectDeviceSelectorType.AssociationEndpoint);
                devInfoCollection = await DeviceInformation.FindAllAsync(deviceSelector);
                DevicesAvailable(this,EventArgs.Empty);
            }
            catch (Exception err)
            {
                rootPage.NotifyUser("Enumeration failed: " + err.Message, NotifyType.ErrorMessage);
            }

        }
    }
}
