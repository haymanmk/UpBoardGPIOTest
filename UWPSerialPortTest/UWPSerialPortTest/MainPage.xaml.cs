﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Windows.Devices.Enumeration; // for serial port
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.ApplicationModel.Core; // Dispatcher
using Windows.UI.Core;
using System.Threading.Tasks; //Task
using System.Threading;
using System.Text;
using Windows.System.Threading;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x404

namespace UWPSerialPortTest
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public ObservableCollection<SerialDevice> collectionSerialDevice = new ObservableCollection<SerialDevice>();

        private TypedEventHandler<SerialDevice, PinChangedEventArgs> DataReceivedHandler;

        SerialDevice serialDevice;

        private CancellationTokenSource dataReadCancellationTokenSource;

        DataReader dataReader;

        public MainPage()
        {
            this.InitializeComponent();

            cbBaudRate.Items.Add((uint)9600);
            cbBaudRate.Items.Add((uint)19200);
            cbBaudRate.Items.Add((uint)115200);

            for (ushort i = 5; i <= 8; i++)
                cbDataBits.Items.Add(i);

            cbParity.Items.Add(SerialParity.None);       //0
            cbParity.Items.Add(SerialParity.Odd);        //1
            cbParity.Items.Add(SerialParity.Even);       //2
            cbParity.Items.Add(SerialParity.Mark);       //3
            cbParity.Items.Add(SerialParity.Space);      //4

            cbStopBits.Items.Add(SerialStopBitCount.One);            //0
            cbStopBits.Items.Add(SerialStopBitCount.OnePointFive);   //1
            cbStopBits.Items.Add(SerialStopBitCount.Two);            //2
        }

        private async Task FindSerialDevices()
        {
            Queue<string> queueText = new Queue<string>();
            string Msg;
            DeviceInformationCollection serialDeviceInfos = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());

            foreach (DeviceInformation serialDeviceInfo in serialDeviceInfos)
            {
                try
                {
                    SerialDevice serialDevice = await SerialDevice.FromIdAsync(serialDeviceInfo.Id);
                    //Msg = $"Serial device ID: {serialDeviceInfo.Id}\n";
                    //AppendText(Msg);
                    if (serialDevice != null)
                    {
                        // Found a valid serial device.
                        Msg = $"Succeed to detect {serialDevice.PortName.ToString()}\n";
                        AppendText(Msg);

                        cbListCOM.Items.Add(serialDevice.PortName);
                        collectionSerialDevice.Add(serialDevice);
                        if (cbBaudRate.FindName(serialDevice.BaudRate.ToString()) == null)
                            cbBaudRate.Items.Add((uint)serialDevice.BaudRate);
                        cbBaudRate.SelectedItem = (uint)serialDevice.BaudRate;
                        if (cbDataBits.FindName(serialDevice.DataBits.ToString()) == null)
                            cbDataBits.Items.Add((ushort)serialDevice.DataBits);
                        cbDataBits.SelectedItem = (ushort)serialDevice.DataBits;
                        cbParity.SelectedIndex = (int)serialDevice.Parity;
                        cbStopBits.SelectedIndex = (int)serialDevice.StopBits;

                        // Reading a byte from the serial device.
                        //DataReader dr = new DataReader(serialDevice.InputStream);
                        //int readByte = dr.ReadByte();

                        // Writing a byte to the serial device.
                        //DataWriter dw = new DataWriter(serialDevice.OutputStream);
                        //dw.WriteByte(0x42);
                    }
                    //serialDevice.Dispose();
                }
                catch (Exception)
                {
                    // Couldn't instantiate the device
                }
            }
        }

        private async void SetupSerialDevice()
        {
            if (collectionSerialDevice.Count <= 0) return;

            dataReadCancellationTokenSource = new CancellationTokenSource();

            //SerialDevice serialDevice;
            for (int i=0; i<collectionSerialDevice.Count; i++)
            {
                serialDevice = collectionSerialDevice[i];
                if (serialDevice.PortName.ToString() == cbListCOM.SelectedValue.ToString())
                {

                    serialDevice.ReadTimeout = TimeSpan.FromMilliseconds(100);
                    serialDevice.BaudRate = (uint)cbBaudRate.SelectedValue;
                    serialDevice.DataBits = (ushort)cbDataBits.SelectedValue;
                    serialDevice.Parity = (SerialParity)cbParity.SelectedItem;
                    serialDevice.StopBits = (SerialStopBitCount)cbStopBits.SelectedItem;
                    //DataReceivedHandler = new TypedEventHandler<SerialDevice, PinChangedEventArgs>(this.PinChangedCallback);
                    //serialDevice.PinChanged += PinChangedCallback;

                    // say Hi
                    DataWriter dw = new DataWriter(serialDevice.OutputStream);
                    dw.WriteString("Hi");
                    //dw.WriteBytes(Encoding.ASCII.GetBytes("Hi"));
                    await dw.StoreAsync();
                    dw.DetachStream(); // release the resource of DataWriter stream to avoid the occupation espicially when there are more than one funcitons which queue in to use this stream.
                    dw = null;

                    listen();

                    return;
                }
            }
            return;
        }

        private async Task AppendText(string Msg)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    tbConsole.Text += Msg;
                    ScrollToBottom(tbConsole);
                }
                ));
        }

        private void ScrollToBottom(TextBox textBox)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(textBox, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f, true);
                break;
            }
        }

        private void PinChangedCallback(object sender, PinChangedEventArgs e)
        {
            switch (e.PinChange)
            {
                case SerialPinChange.DataSetReady:
                    //ReadData();
                    break;
                default:
                    break;
            }
        }

        private async void listen()
        {
            dataReader = new DataReader(serialDevice.InputStream);

            while (true)
            {
                await ReadData(dataReadCancellationTokenSource.Token);
            }

        }

        private async Task ReadData(CancellationToken cancellationToken)
        {
            uint sizeToReadEachTime = 64;
            uint inBufferCnt = 0;
            string receivedStrings = "";

            cancellationToken.ThrowIfCancellationRequested();
            dataReader.InputStreamOptions = InputStreamOptions.Partial;
            dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            dataReader.ByteOrder = Windows.Storage.Streams.ByteOrder.LittleEndian;
            inBufferCnt = await dataReader.LoadAsync(sizeToReadEachTime).AsTask(cancellationToken);

            receivedStrings = dataReader.ReadString(inBufferCnt);
            await AppendText(receivedStrings);
            
            /*
            using (var dataReader = new DataReader(serialDevice.InputStream))
            {
                cancellationToken.ThrowIfCancellationRequested();

                dataReader.InputStreamOptions = InputStreamOptions.Partial;
                dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                dataReader.ByteOrder = Windows.Storage.Streams.ByteOrder.LittleEndian;

                //uint dataReady = await BytesToRead(dataReader);
                

                var receivedStrings = "";

                while (true)
                {
                    inBufferCnt = await dataReader.LoadAsync(sizeToReadEachTime).AsTask(cancellationToken);
                    receivedStrings += dataReader.ReadString(inBufferCnt);
                    await AppendText(receivedStrings);
                    receivedStrings = "";
                }

                /*
                do
                {
                    receivedStrings += dataReader.ReadString(dataReader.UnconsumedBufferLength);
                } while (dataReader.UnconsumedBufferLength > 0);
                
                
            }*/

        }

        private void MainLoop()
        {
            // Start running a main loop
            IAsyncAction asyncAction = ThreadPool.RunAsync(
                async (workItem) =>
                {
                    using (var dataReader = new DataReader(serialDevice.InputStream))
                    {
                        dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                        var receivedStrings = "";
                        uint dataLength;

                        while (true)
                        {
                            // do other things here


                            //check if data has been loaded in the buffer for serial port input stream
                            do
                            {
                                await dataReader.LoadAsync(1);
                                dataLength = dataReader.UnconsumedBufferLength;
                                if (dataReader.UnconsumedBufferLength > 0)
                                {
                                    receivedStrings += dataReader.ReadString(dataReader.UnconsumedBufferLength);
                                }
                                if (receivedStrings.EndsWith("\r\n")) break;

                            } while (dataLength > 0);
                            
                            await AppendText(receivedStrings);
                            receivedStrings = "";
                        }
                    }
                        
                });

        }

        private async void PbDetectCOM_Click(object sender, RoutedEventArgs e)
        {
            tbConsole.Text += "Start finding COM port...\n";
            await FindSerialDevices();
        }

        private void PbSetupCOM_Click(object sender, RoutedEventArgs e)
        {
            tbConsole.Text += "Setting up serial port...";
            ScrollToBottom(tbConsole);
            SetupSerialDevice();
            //DataReceivedHandler = new TypedEventHandler<SerialDevice, PinChangedEventArgs>(PinChangedCallback);
            //serialDevice.PinChanged += PinChangedCallback;// DataReceivedHandler;
        }
    }
}
