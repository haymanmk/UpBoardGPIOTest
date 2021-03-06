﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.Devices.I2c;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Core;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.ApplicationModel.Core;
using System.Runtime.CompilerServices;
using Windows.System.Threading;

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
        public string consoleText;
        string[] AXES_LABEL_ACC_GYRO_M = { "G_X", "G_Y", "G_Z", "A_X", "A_Y", "A_Z", "M_X", "M_Y", "M_Z" };

        public ObservableCollection<string> message = new ObservableCollection<string>();
        public ObservableCollection<string> listSAD = new ObservableCollection<string>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string callerName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(callerName));
        }

        public MainPage()
        {
            this.InitializeComponent();

            for (UInt16 i=1; i <= 12; i++)
                cbNumData.Items.Add(i);
        }

        private async void I2cdetectAsync()
        {
            try
            {
                controllerList = (await I2cController.GetControllersAsync(UpWinApis.UpI2cProvider.GetI2cProvider()));
                controller = controllerList[0];
            }
            catch
            {

            }

            IAsyncAction asyncAction = ThreadPool.RunAsync(
                (workItem) =>
                {
                    try
                    {
                        
                        byte[] writebuf = { 0x00 };
                        byte[] readbuf = new byte[1];
                        Queue queueSAD = new Queue();
                        int slaveAddress;

                        for (uint i = 0; i < 128; i += 16)
                        {
                            for (uint j = 0; j < 16; j++)
                            {
                                slaveAddress = (int)(i + j);
                                settings.SlaveAddress = slaveAddress;
                                //settings.SharingMode = I2cSharingMode.Shared;
                                try
                                {
                                    controller.GetDevice(settings).WriteRead(writebuf, readbuf);
                                    queueSAD.Enqueue(Convert.ToString(slaveAddress, 16));
                                    var _msg = "Success to detect: " + Convert.ToString(slaveAddress, 16);

                                    // update UI by using the UI thread
                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                        CoreDispatcherPriority.High,
                                        new DispatchedHandler(() =>
                                        {
                                            //message.Add(_msg);
                                            tbConsole.Text += "\n" + _msg + "\n";
                                            listSAD.Add(queueSAD.Dequeue().ToString());
                                        }));
                                    
                                }
                                catch (Exception e)
                                {
                                    // continue
                                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                        CoreDispatcherPriority.High,
                                        new DispatchedHandler(() =>
                                        {
                                            //message.Add("--");
                                            tbConsole.Text += "#";
                                        }));
                                }
                            }

                        }
                    }
                    catch (Exception e)
                    {
                        // continue
                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                        CoreDispatcherPriority.High,
                                        new DispatchedHandler(() =>
                                        {
                                            //message.Add("An exception occurred during running I2cdetectAsync(): " + e.ToString());
                                            tbConsole.Text += "An exception occurred during running I2cdetectAsync(): " + e.ToString();
                                        }));
                        
                    }
                });
            pbDetectAddress.IsEnabled = true;
        }

        private void I2cget(int Slave, byte[] writeBuf, out byte[] readBuf, UInt16 NumData=1)
        {
            settings.SlaveAddress = Slave;
            readBuf = new byte[NumData];

            try
            {
                controller.GetDevice(settings).WriteRead(writeBuf, readBuf);
                //tbConsole.Text += "Success to read data: 0x" + Convert.ToString(readBuf[0], 16) + "\n";
            }
            catch (Exception e)
            {
                //tbConsole.Text += "error to get data\n";
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

        private async void I2cset(int Slave, byte[] writeBuf)
        {
            //I2cController controller = await I2cController.GetDefaultAsync();
            //I2cConnectionSettings settings = new I2cConnectionSettings(Slave);
            //settings.SharingMode = I2cSharingMode.Shared;
            settings.SlaveAddress = Slave;
            controller.GetDevice(settings).Write(writeBuf);
            try
            {
                //controller.GetDevice(settings).Write(writeBuf);
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    new DispatchedHandler(() =>
                    {
                        tbConsole.Text += "Success to write " + writeBuf[1] + " at " + writeBuf[0] + "\n";
                    }));
                
            }
            catch (Exception e)
            {
                //Console.WriteLine("error to set data\n");
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    new DispatchedHandler(() =>
                    {
                        tbConsole.Text += "Error to write data\n";
                        tbConsole.Text += e.ToString();
                    }));
                tbConsole.Text += "Error to write data\n";
                return;
            }
            
        }

        private void ReadOneAccGyroData(int Slave)
        {
            //int Slave = 0x6B;
            //byte CTRL_REG1_G = Convert.ToByte(0x10);
            //byte CTRL_REG1_G_DATA = 0x20; // b0010 0000
            //byte[] writeBuf = { CTRL_REG1_G, CTRL_REG1_G_DATA };
            byte[] OUT_X_G = { 0x18 };
            byte[] OUT_X_XL = { 0x28 };
            byte[] writeBuf = OUT_X_XL;
            byte[] readBuf_temp = new byte[1];
            byte[] readBuf_LSB_MSB = new byte[2];
            Int16 ReadingValue;
            float ActualValue;
            Queue queueData = new Queue();

            //I2cset(Slave, writeBuf);

            while (IsFIFOEmpty(Slave))
            {
                // wait here until there is data loaded in FIFO.
            }

            I2cget(Slave, writeBuf, out byte[] readBuf, 6);
            //readBuf_LSB_MSB[0] = readBuf[0];
            //controller.GetDevice(settings).Read(readBuf_temp);
            //readBuf_LSB_MSB[1] = readBuf_temp[0];

            for (int i=0; i<3; i++)
            {
                ReadingValue = BitConverter.ToInt16(readBuf, 2*i);
                ActualValue = Convert2ActualValue(ReadingValue, AXES_LABEL_ACC_GYRO_M[3+i]);
                queueData.Enqueue($"{AXES_LABEL_ACC_GYRO_M[3 + i]}: {Convert.ToString(ActualValue)}, ");

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    tbConsole.Text += queueData.Dequeue().ToString();
                }));
            }

            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    tbConsole.Text += "\n";
                    ScrollToBottom(tbConsole);
                }));
        }

        private float Convert2ActualValue(Int16 ReadingBinary, string SensorType)
        {
            float ActualValue = 0;

            switch (SensorType[0])
            {
                case 'G':
                    ActualValue = (float)(7.4770 * ReadingBinary); //mdps/LSB
                    break;
                case 'A':
                    ActualValue = (float)(0.061 * ReadingBinary); //mg/LSB
                    break;
                case 'M':
                    ActualValue = (float)(0.4883 * ReadingBinary); //mgauss/LSB
                    break;
                default:
                    break;
            }
            return ActualValue;
        }

        private void InitSensor_Acc_Gyro(int Slave)
        {
            // Power on Acc and Gyro
            //int Slave = 0x6B;
            //byte CTRL_REG1_G = Convert.ToByte(0x10);
            //byte CTRL_REG1_G_DATA = 0b00100000; // b0010 0000
            byte CTRL_REG6_XL = 0x20;
            byte CTRL_REG6_XL_DATA = 0b00100000;
            byte[] writeBuf = { CTRL_REG6_XL, CTRL_REG6_XL_DATA };

            I2cset(Slave, writeBuf);
        }

        private void InitSensor_Mag(int Slave)
        {
            // Initialize magnetometer
            byte CTRL_REG1_M = Convert.ToByte(0x20);
            byte CTRL_REG1_M_DATA = 0b00010000; // 0b0001 0000
            byte CTRL_REG3_M = 0x22;
            byte CTRL_REG3_M_DATA = 0b00000000; // Continuous-conversion mode
            byte[] writeBuf = { CTRL_REG3_M, CTRL_REG3_M_DATA };

            I2cset(Slave, writeBuf);

            // Select full-scale
            byte CTRL_REG2_M = Convert.ToByte(0x21);
            byte CTRL_REG2_M_DATA = 0b11000000; // CTRL_REG2_M[7:6]: 00-4gauss, 01-8gauss, 10-12gauss, 11-16gauss
            writeBuf[0] = CTRL_REG2_M;
            writeBuf[1] = CTRL_REG2_M_DATA;

            I2cset(Slave, writeBuf);
        }

        private bool IsMDataAvailable(int Slave)
        {
            byte STATUS_REG_M = 0x27;
            byte[] writeBuf = { STATUS_REG_M };

            I2cget(Slave, writeBuf, out byte[] readBuf);

            return ((readBuf[0] & 0b00001000) > 0);
        }

        private void ReadOneMagData(int Slave)
        {
            byte OUT_X_L_M = 0x27;
            byte[] writeBuf = { OUT_X_L_M };
            byte[] readBuf_temp = new byte[1];
            byte[] readBuf_LSB_MSB = new byte[2];
            Int16 ReadingValue;
            float ActualValue;
            Queue queueData = new Queue();

            //I2cset(Slave, writeBuf);

            while (!IsMDataAvailable(Slave))
            {
                // wait here until there is a new data loaded in buffer.
            }

            I2cget(Slave, writeBuf, out byte[] readBuf, 6);

            for (int i = 0; i < 3; i++)
            {
                ReadingValue = BitConverter.ToInt16(readBuf, 2 * i);
                ActualValue = Convert2ActualValue(ReadingValue, AXES_LABEL_ACC_GYRO_M[6+i]);
                queueData.Enqueue($"{AXES_LABEL_ACC_GYRO_M[6 + i]}: {Convert.ToString(ActualValue)}, ");

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    tbConsole.Text += queueData.Dequeue().ToString();
                }));
            }

            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    tbConsole.Text += "\n";
                    ScrollToBottom(tbConsole);
                }));
        }

        private void EnableFIFOMode(int Slave)
        {
            byte CTRL_REG9 = 0x23;
            byte CTRL_REG9_DATA = 0b00000010;
            byte[] writeBuf = { CTRL_REG9, CTRL_REG9_DATA };
            I2cset(Slave, writeBuf);

        }

        private void RestFIFOMode(int Slave)
        {
            byte FIFO_CTRL = Convert.ToByte(0x2E);
            byte FIFO_CTRL_DATA = Convert.ToByte(0b00000000); //Bypass mode.
            byte[] writeBuf = { FIFO_CTRL, FIFO_CTRL_DATA };
            I2cset(Slave, writeBuf);

        }
        private void OnFIFOMode(int Slave)
        {
            byte FIFO_CTRL = Convert.ToByte(0x2E);
            byte FIFO_CTRL_DATA = Convert.ToByte(0b11000000); //Continuous mode.
            byte[] writeBuf = { FIFO_CTRL, FIFO_CTRL_DATA };
            I2cset(Slave, writeBuf);

        }

        private bool IsFIFOEmpty(int Slave)
        {
            byte[] FIFO_SRC = { 0x2F };
            I2cget(Slave, FIFO_SRC, out byte[] readBuf);
            return ((readBuf[0] & 0b00111111) == 0);
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

        private void PbDetectAddress_Click(object sender, RoutedEventArgs e)
        {
            pbDetectAddress.IsEnabled = false;
            I2cdetectAsync();
            tbConsole.Text += "Start detecting devices, please wait...\n";
        }

        private void PbReadSingleData_Click(object sender, RoutedEventArgs e)
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

        private void PbWriteSingleData_Click(object sender, RoutedEventArgs e)
        {
            tbConsole.Text += "Start writing data to register, please wait...\n";
            byte[] writeBuf = { Convert.ToByte(tbRegRW.Text.ToString()), Convert.ToByte(tbWriteData.Text.ToString())};
            I2cset(Convert.ToInt32(cbSADList.SelectedValue.ToString(), 16), writeBuf);
        }

        private void PbStartRead_Click(object sender, RoutedEventArgs e)
        {
            UInt16 TimesIteration = 50;
            int Slave = 0x6B;

            IAsyncAction asyncAction = ThreadPool.RunAsync(
                (workItem) => {
                    // Initialize acc and gyro sensors
                    InitSensor_Acc_Gyro(Slave);

                    EnableFIFOMode(Slave);

                    RestFIFOMode(Slave);

                    OnFIFOMode(Slave);

                    for (uint i = 0; i < TimesIteration; i++)
                        ReadOneAccGyroData(Slave);
                });
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            I2cdetectAsync();
        }

        private void PbReadMSensor_Click(object sender, RoutedEventArgs e)
        {
            UInt16 TimesIteration = 50;
            int Slave = 0x1E;

            IAsyncAction asyncAction = ThreadPool.RunAsync(
                (workItem) => {
                    // Initialize acc and gyro sensors
                    InitSensor_Mag(Slave);

                    for (uint i = 0; i < TimesIteration; i++)
                        ReadOneMagData(Slave);
                });
        }
    }

    
}
