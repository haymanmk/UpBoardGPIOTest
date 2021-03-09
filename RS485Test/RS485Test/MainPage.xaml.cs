using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks; //Task
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.SerialCommunication;
using Windows.Devices.Enumeration;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using System.Collections.ObjectModel;
using System.Threading;
using Windows.Storage.Streams;
using System.Text;


// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x404

namespace RS485Test
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public ObservableCollection<SerialDevice> collectionSerialDevice = new ObservableCollection<SerialDevice>();
        SerialDevice serialDevice;
        private CancellationTokenSource dataReadCancellationTokenSource;
        DataReader dataReader;
        Boolean IsSerialReadingStart;

        const UInt16 POLYNOMIAL = 0xA001;

        public class OrientalMotorCommandShortcut
        {
            public string Name { get; set; }
            public string Command { get; set; }

            public OrientalMotorCommandShortcut() { }
            public OrientalMotorCommandShortcut(string name)
            {
                Name = name;
            }
        }

        List<OrientalMotorCommandShortcut> orientalMotorCommandShortcuts = new List<OrientalMotorCommandShortcut>
        {
            new OrientalMotorCommandShortcut {Name = "Continuous Run", Command = "0110048000020400001388"},
            new OrientalMotorCommandShortcut {Name = "FW-POS ON", Command = "0106007D4000"},
            new OrientalMotorCommandShortcut {Name = "FW-POS OFF", Command = "0106007D0000"},
        };

        public MainPage()
        {
            this.InitializeComponent();

            __Configure();
        }

        private void __Configure()
        {
            // Initialize items in ComboBox
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

            foreach (OrientalMotorCommandShortcut _cmd in orientalMotorCommandShortcuts)
            {
                cbCommandShortcut.Items.Add(_cmd);
            }
            cbCommandShortcut.DisplayMemberPath = "Name";
            cbCommandShortcut.SelectedValuePath = "Command";

            // instantiate cancellation token to stop the thread for data reading in input stream.
            dataReadCancellationTokenSource = new CancellationTokenSource();

            IsSerialReadingStart = false;
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

                    if (serialDevice != null)
                    {
                        // Found a valid serial device.
                        Msg = $"Succeed to detect {serialDevice.PortName.ToString()}\n";
                        await AppendText(Msg);

                        cbCOM.Items.Add(serialDevice.PortName);
                        collectionSerialDevice.Add(serialDevice);
                        //serialDevice.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    // Couldn't instantiate the device
                    AppendText(ex.ToString());
                }
            }
        }

        private async void SetupSerialDevice()
        {
            if (collectionSerialDevice.Count <= 0) return;

            for (int i = 0; i < collectionSerialDevice.Count; i++)
            {
                serialDevice = collectionSerialDevice[i];
                if (serialDevice.PortName.ToString() == cbCOM.SelectedValue.ToString())
                {

                    serialDevice.ReadTimeout = TimeSpan.FromMilliseconds(100);
                    serialDevice.WriteTimeout = TimeSpan.FromMilliseconds(100);
                    serialDevice.BaudRate = (uint)cbBaudRate.SelectedValue;
                    serialDevice.DataBits = (ushort)cbDataBits.SelectedValue;
                    serialDevice.Parity = (SerialParity)cbParity.SelectedItem;
                    serialDevice.StopBits = (SerialStopBitCount)cbStopBits.SelectedItem;
                    serialDevice.Handshake = SerialHandshake.None;

                    StartListen();

                    await AppendText($"Succeed to set up serial port {serialDevice.PortName.ToString()}");

                    return;
                }
            }
            return;
        }

        private async void SendBytes(byte[] bytes)
        {
            // send bytes
            DataWriter dw = new DataWriter(serialDevice.OutputStream);
            
            dw.ByteOrder = ByteOrder.BigEndian;
            dw.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            dw.WriteBytes(bytes);
            await dw.StoreAsync();
            dw.DetachStream(); // release the resource of DataWriter stream to avoid the occupation espicially when there are more than one funcitons which queue in to use this stream.
            dw = null;
        }

        private void StartListen()
        {
            listen();
        }

        private void StopSerial()
        {
            IsSerialReadingStart = false;
            if (serialDevice != null) serialDevice.Dispose();

            if (dataReadCancellationTokenSource != null) dataReadCancellationTokenSource.Cancel();
        }

        private void ResetReadCancellationTokenSource()
        {
            //create a new cancellation token source
            dataReadCancellationTokenSource = new CancellationTokenSource();

            //fire a cancellation callback to notify user
            dataReadCancellationTokenSource.Token.Register(() => NotifyDialog("Serial port has been stoppped."));

            IsSerialReadingStart = false;
        }

        private async void listen()
        {
            IsSerialReadingStart = true;

            if (serialDevice == null)
            {
                NotifyDialog("WARNING: Serial is not available.");
                return;
            }

            dataReader = new DataReader(serialDevice.InputStream);

            while (IsSerialReadingStart)
            {
                try
                {
                    await ReadData(dataReadCancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    AppendText(ex.ToString());
                }
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

            byte[] receivedBytes = new byte[inBufferCnt];

            dataReader.ReadBytes(receivedBytes);// ReadString(inBufferCnt);

            foreach (byte __data in receivedBytes)
            {
                receivedStrings += $"{__data:X} ";
            }
            await AppendText(receivedStrings);
        }

        private UInt16 CalculateCRC16(byte[] data)
        {
            if (data.Length < 1)
            {
                NotifyDialog("The size of Input Data shall be larger than 1 while running CalculateCRC16.");
                return 0;
            }

            UInt16 xValue = 0xFFFF;

            foreach (byte __data in data)
            {
                xValue ^= __data;

                xValue = UpdateCRC(xValue);

                AppendText($"CRC16 Processing...0x{xValue:X}");
            }

            return xValue;
        }

        public UInt16 UpdateCRC(UInt16 data)
        {
            UInt16 xValue = data;
            for (byte i = 0; i < 8; i++)
            {
                if ((xValue & 0x0001) != 0)
                {
                    xValue = (UInt16)((xValue >> 1) ^ POLYNOMIAL);
                }
                else
                {
                    xValue >>= 1;
                }
            }

            return xValue;
        }

        private async Task AppendText(string Msg)
        {
            if (!Msg.EndsWith("\r\n")) Msg += "\r\n";

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
            try
            {
                for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
                {
                    object obj = VisualTreeHelper.GetChild(grid, i);
                    if (!(obj is ScrollViewer)) continue;
                    ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f, true);
                    break;
                }
            }
            catch (Exception ex)
            {
                // Continue
            }
        }

        private async void NotifyDialog(string msg)
        {
            ContentDialog contentDialog = new ContentDialog
            {
                Title = "Notification",
                Content = msg,
                CloseButtonText = "OK"
            };

            ContentDialogResult result = await contentDialog.ShowAsync();
        }

        private byte[] ParserString2Bytes(string __string)
        {
            //byte[] bytes = Encoding.ASCII.GetBytes(__string);
            int lengthString = __string.Length;
            byte[] bytes = new byte[lengthString/2];
            for (int i=0; i<lengthString/2; i++)
            {
                bytes[i] = Convert.ToByte(__string.Substring(i*2, 2), 16);
            }
            
            return bytes;
        }

        private void AddByte2Bytes(ref byte[] bytes, UInt16 addUInt16)
        {
            Array.Resize<byte>(ref bytes, bytes.Length + 2);

            byte _i = 1;
            int lengthBytes = bytes.Length;

            foreach (byte _data in BitConverter.GetBytes(addUInt16))
            {
                bytes[lengthBytes - _i - 1] = _data;
                _i--;
            }
        }

        private async void BtFindCOM_Click(object sender, RoutedEventArgs e)
        {
            await FindSerialDevices();
        }

        private void BtSetCOM_Click(object sender, RoutedEventArgs e)
        {
            SetupSerialDevice();
        }

        private void BtStopCOM_Click(object sender, RoutedEventArgs e)
        {
            StopSerial();
        }

        private void BtSend_Click(object sender, RoutedEventArgs e)
        {
            byte[] bytes = ParserString2Bytes(tbCommand.Text.ToString());
            UInt16 crc16 = CalculateCRC16(bytes);

            AddByte2Bytes(ref bytes, crc16);
            if (crc16 > 0)
            {
                SendBytes(bytes);
            }

            tbCommand.Text = "";
        }

        private void CbCommandShortcut_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            tbCommand.Text = cbCommandShortcut.SelectedValue.ToString();
        }
    }
}
