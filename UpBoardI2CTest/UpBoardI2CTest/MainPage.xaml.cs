using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.Devices.I2c;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Core;
using System.Threading.Tasks;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x404

namespace UpBoardI2CTest
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>                                  
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        IReadOnlyList<I2cController> controllerList;
        I2cController controller;
        I2cConnectionSettings settings = new I2cConnectionSettings(0x00);
        private string consoleText;
        string[] AXES_LABEL_ACC_GYRO = { "G_X", "G_Y", "G_Z", "A_X", "A_Y", "A_Z" };

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        private ObservableCollection<string> _Message;
        public ObservableCollection<string>Message
        {
            get => _Message;
            set
            {
                _Message = value;
                NotifyPropertyChanged(nameof(Message));
            }
        }


        public MainPage()
        {
            this.InitializeComponent();

            for (UInt16 i=1; i <= 12; i++)
                cbNumData.Items.Add(i);
            Message = new ObservableCollection<string>();
            lstMessage.DataContext = this;
        }
        /*
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public string ConsoleText
        {
            get { return this.consoleText};
            set
            {
                this.consoleText = value;
                
            }
        }
        //public void OnPropertyChanged()
        */

        private void Test()
        {
            Task.Run(() => I2cdetect());
        }


        private async void I2cdetect()
        {


            try
            {
                controllerList = (await I2cController.GetControllersAsync(UpWinApis.UpI2cProvider.GetI2cProvider()));
                controller = controllerList[0];
                byte[] writebuf = { 0x00 };
                byte[] readbuf = new byte[1];

                for (uint i = 0; i < 128; i += 16)
                {
                    for (uint j = 0; j < 16; j++)
                    {
                        settings.SlaveAddress = (int)(i + j);
                        //settings.SharingMode = I2cSharingMode.Shared;
                        try
                        {
                            controller.GetDevice(settings).WriteRead(writebuf, readbuf);
                            //cbSADList.Items.Add(Convert.ToString(i + j, 16));
                            var _msg ="Success to detect: " + Convert.ToString(i + j, 16);

                            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Message.Add(_msg);
                            });
                            
                        }
                        catch
                        {
                            // continue
                            //tbConsole.Text += "#";
                        }
                    }

                }
            }
            catch (Exception e)
            {
                // continue
            }
        }

        private void I2cget(int Slave, byte[] writeBuf, out byte[] readBuf, UInt16 NumData=1)
        {
            settings.SlaveAddress = Slave;
            readBuf = new byte[NumData];

            try
            {
                controller.GetDevice(settings).WriteRead(writeBuf, readBuf);
                tbConsole.Text += "Success to read data: 0x" + Convert.ToString(readBuf[0], 16) + "\n";
            }
            catch (Exception e)
            {
                tbConsole.Text += "error to get data\n";
                return;
            }
            /*
            for (uint i=1; i < NumData; i++)
            {
                controller.GetDevice(settings).Read(readBuf);
                tbConsole.Text += "Success to read data: 0x" + Convert.ToString(readBuf[0], 16) + "\n";
            }
            */
        }

        private void I2cset(int Slave, byte[] writeBuf)
        {
            //I2cController controller = await I2cController.GetDefaultAsync();
            //I2cConnectionSettings settings = new I2cConnectionSettings(Slave);
            //settings.SharingMode = I2cSharingMode.Shared;
            settings.SlaveAddress = Slave;
            controller.GetDevice(settings).Write(writeBuf);
            try
            {
                //controller.GetDevice(settings).Write(writeBuf);
                tbConsole.Text += "Success to write " + writeBuf[1] + " at " + writeBuf[0] + "\n";
            }
            catch (Exception e)
            {
                //Console.WriteLine("error to set data\n");
                tbConsole.Text += "Error to write data\n";
                return;
            }
            
        }

        private void ReadOneAccGyroData()
        {
            int Slave = 0x6B;
            byte CTRL_REG1_G = Convert.ToByte(0x10);
            byte CTRL_REG1_G_DATA = 0x20; // b0010 0000
            byte[] writeBuf = { CTRL_REG1_G, CTRL_REG1_G_DATA };
            byte[] OUT_X_G = { 0x18 };
            byte[] readBuf_temp = new byte[1];
            byte[] readBuf_LSB_MSB = new byte[2];
            Int16 ReadingValue;

            I2cset(Slave, writeBuf);

            I2cget(Slave, OUT_X_G, out byte[] readBuf);
            readBuf_LSB_MSB[0] = readBuf[0];
            controller.GetDevice(settings).Read(readBuf_temp);
            readBuf_LSB_MSB[1] = readBuf_temp[0];
            
            ReadingValue = BitConverter.ToInt16(readBuf_LSB_MSB, 0);

            tbConsole.Text += "G_X: " + Convert.ToString(ReadingValue, 10) + "\n";

            for (uint i = 1; i < 6; i++)
            {
                controller.GetDevice(settings).Read(readBuf_temp);
                readBuf_LSB_MSB[0] = readBuf_temp[0]; // LSB
                controller.GetDevice(settings).Read(readBuf_temp);
                readBuf_LSB_MSB[1] = readBuf_temp[0]; // MSB
                ReadingValue = BitConverter.ToInt16(readBuf_LSB_MSB, 0);
                tbConsole.Text += AXES_LABEL_ACC_GYRO[i] + ": " + Convert.ToString(ReadingValue, 10) + "\n";
            }
        }

        private void PbDetectAddress_Click(object sender, RoutedEventArgs e)
        {
            I2cdetect();
            tbConsole.Text += "Start detecting devices, please wait...\n";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            tbConsole.Text += "Start reading register, please wait...\n";
            byte[] writeBuf = { Convert.ToByte(tbRegRW.Text.ToString()) };
            I2cget(Convert.ToInt32(cbSADList.SelectedValue.ToString(), 16), writeBuf, out byte[] readBuf);
            //tbConsole.Text += "Success to read data: " + Convert.ToString(readBuf[0], 16) + "\n";
        }

        private void PbReadMultiData_Click(object sender, RoutedEventArgs e)
        {
            tbConsole.Text += "Start reading multi-register, please wait...\n";
            byte[] writeBuf = { Convert.ToByte(tbRegRW.Text.ToString()) };
            I2cget(Convert.ToInt32(cbSADList.SelectedValue.ToString(), 16), writeBuf, out byte[] readBuf, Convert.ToUInt16(cbNumData.SelectedValue));
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            tbConsole.Text += "Start writing data to register, please wait...\n";
            byte[] writeBuf = { Convert.ToByte(tbRegRW.Text.ToString()), Convert.ToByte(tbWriteData.Text.ToString())};
            I2cset(Convert.ToInt32(cbSADList.SelectedValue.ToString(), 16), writeBuf);
        }

        private void PbStartRead_Click(object sender, RoutedEventArgs e)
        {
            UInt16 TimesIteration = 50;

            for (uint i = 0; i < TimesIteration; i++)
                ReadOneAccGyroData();

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Test();
        }
    }

    
}
