/* ----------------------------------------------- 
 * Copyright © 2013 Esri Inc. All Rights Reserved. 
 * ----------------------------------------------- */

using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Networking.Proximity;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace App {
    public class SocketReaderWriter  : IDisposable {
        DataReader _dataReader;
        DataWriter _dataWriter;
        StreamSocket _streamSocket;
        private MainPage _rootPage;

        public SocketReaderWriter(StreamSocket socket, MainPage mainPage)
        {
            _dataReader = new DataReader(socket.InputStream);
            _dataReader.UnicodeEncoding = UnicodeEncoding.Utf8;
            _dataReader.ByteOrder = ByteOrder.LittleEndian;

            _dataWriter = new DataWriter(socket.OutputStream);
            _dataWriter.UnicodeEncoding = UnicodeEncoding.Utf8;
            _dataWriter.ByteOrder = ByteOrder.LittleEndian;

            _streamSocket = socket;
            _rootPage = mainPage;
        }

        public void Dispose()
        {
            _dataReader.Dispose();
            _dataWriter.Dispose();
            _streamSocket.Dispose();
        }

        public async void WriteMessage(string message)
        {
            try
            {
                _dataWriter.WriteUInt32(_dataWriter.MeasureString(message));
                _dataWriter.WriteString(message);
                await _dataWriter.StoreAsync();
                _rootPage.NotifyUser("Sent message: " + message, NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                _rootPage.NotifyUser("WriteMessage threw exception: " + ex.Message, NotifyType.KeepMessage);
            }
        }

        public void WritePng(string filePath)
        {

            try
            {
                /*
                Image image = Image.FromFile(filePath);
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] imageBuffer = ms.GetBuffer();
                    _dataWriter.WriteBytes(imageBuffer);
                    await _dataWriter.StoreAsync();
                    ms.Close();
                }
                */
                _rootPage.NotifyUser("Png has been sent !", NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                _rootPage.NotifyUser("WritePng() threw exception: " + ex.Message, NotifyType.KeepMessage);
            }
        }

        public async Task WritePng(StorageFile file)
        {

            try
            {
                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    // Read byte array from file
                    using (DataReader reader = new DataReader(stream.GetInputStreamAt(0)))
                    {
                        await reader.LoadAsync((uint)stream.Size);
                        byte[] Bytes = new byte[stream.Size];

                        // Write into socket
                        _dataWriter.WriteBytes(Bytes);
                        await _dataWriter.StoreAsync();
                    }
                }

                _rootPage.NotifyUser("Png has been sent !", NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                _rootPage.NotifyUser("WritePng() threw exception: " + ex.Message, NotifyType.KeepMessage);
            }
        }
    }
}
