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
        private int BUFFER_LENGTH = 1024;
        private long streamPosition = 0;
        private uint streamSize = 0;

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

        public async Task WritePng(StorageFile file)
        {

            try
            {

                streamSize = (uint)(await (file as StorageFile).OpenStreamForReadAsync()).Length;
                streamPosition = 0;
                await Store(_dataWriter,file);

                _rootPage.NotifyUser("Png has been sent !", NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                _rootPage.NotifyUser("WritePng() threw exception: " + ex.Message, NotifyType.KeepMessage);
            }
        }

        private async Task Store(DataWriter writer, object stFile) {

            Stream stream;

            stream = await (stFile as StorageFile).OpenStreamForReadAsync();

            using (stream)
            {
                int len = 0;
                stream.Position = streamPosition;

                long memAlloc = stream.Length - streamPosition < BUFFER_LENGTH ? stream.Length - streamPosition : BUFFER_LENGTH;
                byte[] buffer = new byte[memAlloc];

                while (writer.UnstoredBufferLength < memAlloc)
                {
                    len = stream.Read(buffer, 0, buffer.Length);
                    if (len > 0)
                    {
                        writer.WriteBytes(buffer);
                        streamPosition += len;
                    }
                }

                try
                {
                    await writer.StoreAsync();
                }
                catch
                {
                    _rootPage.NotifyUser("Failed to store {0} bytes: " + writer.UnstoredBufferLength, NotifyType.KeepMessage);
                }
            }
            // There is a leak somewhere that causes the stored stream
            // to be cached instead of being properly disposed.
            GC.Collect();

            if (streamPosition < streamSize)
            {
                await Store(writer, stFile);
            }
        }
    }
}
