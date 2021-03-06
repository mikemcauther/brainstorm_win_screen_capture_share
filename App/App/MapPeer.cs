﻿/* ----------------------------------------------- 
 * Copyright © 2013 Esri Inc. All Rights Reserved. 
 * ----------------------------------------------- */

using System;
using System.Diagnostics;
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
            _rootPage.NotifyUser("MapPeer will close socket !", NotifyType.StatusMessage);
            _dataReader.Dispose();
            _dataWriter.Dispose();
            _streamSocket.Dispose();
        }

        public async Task WritePng(StorageFile file)
        {

            Debug.Write(" WritePng() thread = " + Environment.CurrentManagedThreadId);
            try
            {
                await Store(_dataWriter,file);

                _rootPage.NotifyUser("Png has been sent !", NotifyType.KeepMessage);
            }
            catch (Exception ex)
            {
                _rootPage.NotifyUser("WritePng() threw exception: " + ex.Message, NotifyType.KeepMessage);
            }
        }

        private async Task Store(DataWriter writer, object stFile) {

            bool isNeedContinue = true;
            long streamPosition = 0;
            uint streamSize = 0;


            Debug.Write(" Store() thread = " + Environment.CurrentManagedThreadId);
            while(isNeedContinue)
            {
                using (var stream = await (stFile as StorageFile).OpenStreamForReadAsync())
                {
                    int len = 0;
                    streamSize = (uint)stream.Length;
                    stream.Position = streamPosition;

                    long memAlloc = streamSize - streamPosition < BUFFER_LENGTH ? streamSize - streamPosition : BUFFER_LENGTH;
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
                    catch (Exception ex)
                    {
                        _rootPage.NotifyUser("Failed to store {0} bytes: " + writer.UnstoredBufferLength + " threw exception : " + ex.Message, NotifyType.KeepMessage);
                        Debug.WriteLine("");
                    }
                }
                // There is a leak somewhere that causes the stored stream
                // to be cached instead of being properly disposed.
                GC.Collect();

                if (streamPosition < streamSize)
                {
                    isNeedContinue = true;
                } else {
                    isNeedContinue = false;
                }
            }
        }
    }
}
