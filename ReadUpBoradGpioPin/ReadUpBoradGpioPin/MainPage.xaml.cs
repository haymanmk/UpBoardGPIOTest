using System;
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
using Windows.Devices;
using Windows.Devices.Gpio;
using Windows.Devices.Gpio.Provider;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x404
internal enum EAPIStatus_t : uint
{
    EAPI_STATUS_SUCCESS = 0,
    EAPI_STATUS_ERROR = 0xFFFFF0FF,
    EAPI_STATUS_INITIALIZED = 0xFFFFFFFE,
    EAPI_STATUS_NOT_INITIALIZED = 0xFFFFFFFF,
};


namespace ReadUpBoradGpioPin
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        UpBridge.Up upb = new UpBridge.Up();
        private event EventHandler UpReadyEvent;
        UpWinApis.UpGpioProvider upGpioProvider = new UpWinApis.UpGpioProvider();
        IGpioControllerProvider upGpioControllerProvider;
        ProviderGpioPinValue gpioPinValue;
        IGpioPinProvider gpioPinProvider;

        public MainPage()
        {
            this.InitializeComponent();
            UpReadyEvent += OnUpReady;
            pDir.Items.Add("IN");
            pDir.Items.Add("OUT");
            pVal.Items.Add("Hi");
            pVal.Items.Add("Lo");

            

            //UpWinApis.UpGpioControllerProvider upWinApisGpio = UpWinApis.UpGpioControllerProvider();
            
            
            //IGpioPinProvider gpioPinProvider = upGpioControllerProvider.OpenPinProvider(6, ProviderGpioSharingMode.Exclusive);

            /*
            if (gpioPinProvider.IsDriveModeSupported(ProviderGpioPinDriveMode.Output))
            {
                gpioPinProvider.SetDriveMode(ProviderGpioPinDriveMode.Output);
                gpioPinValue = gpioPinProvider.Read();
                gpioPinProvider.Write(ProviderGpioPinValue.Low);
                gpioPinValue = gpioPinProvider.Read();
            }
            
            //int upbPinCount = upWinApisGpio.PinCount;
            GpioController gpioController = GpioController.GetDefault();
            GpioPin gpioPin = null;

            if (gpioController != null)
            {
                int gpioPinCount = gpioController.PinCount;
                gpioPin = gpioController.OpenPin(35, GpioSharingMode.Exclusive);
                string gpioPinDriveMode = gpioPin.GetDriveMode().ToString();
                if (gpioPin.IsDriveModeSupported(GpioPinDriveMode.Output))
                {
                    //gpioPin.SetDriveMode(GpioPinDriveMode.Output);
                }
            }
            */
            UpReadyEvent.Invoke(null, null);
        }

        private void OnUpReady(object sender, EventArgs e)
        {
            //Update Info
            UpMfg.Text = upb.BoardGetManufacture();
            UpBoardName.Text = "Board Name:" + upb.BoardGetName();
            UpBiosVer.Text = "BIOS Ver:" + upb.BoardGetBIOSVersion();


            //Check all pins
            int numControllers = upGpioProvider.GetControllers().Count;
            string listControllers = upGpioProvider.GetControllers().ToString();
            upGpioControllerProvider = upGpioProvider.GetControllers().FirstOrDefault();
            int pinCountProvider = upGpioControllerProvider.PinCount;
            //EAPIStatus_t stat = (EAPIStatus_t)upb.GpioGetCaps(0, out uint gpiocnt, out uint disabled);

            if (upGpioControllerProvider != null)
            {
                for (int i = 0; i < pinCountProvider; ++i)
                {
                    //stat = (EAPIStatus_t)upb.GpioGetCaps(i, out uint cnt, out disabled);
                    try
                    {
                        gpioPinProvider = upGpioControllerProvider.OpenPinProvider(i, ProviderGpioSharingMode.SharedReadOnly);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    
                    if (!gpioPinProvider.IsDriveModeSupported(ProviderGpioPinDriveMode.Output))
                        continue;
                    //stat = (EAPIStatus_t)upb.GpioGetDirection(i, 0xFFFFFFFF, out uint dir);
                    if (!gpioPinProvider.IsDriveModeSupported(ProviderGpioPinDriveMode.Input))
                        continue;

                    try
                    {
                        gpioPinValue = gpioPinProvider.Read();
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    pList.Items.Add(((uint)(i)).ToString());
                }
            }
            if (pList.Items.Count > 0)
                pList.SelectedIndex = 0;

            return;
        }

        private void PList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            gpioPinProvider = upGpioControllerProvider.OpenPinProvider(int.Parse(pList.SelectedValue.ToString()), ProviderGpioSharingMode.SharedReadOnly);

            switch (gpioPinProvider.GetDriveMode())
            {
                case ProviderGpioPinDriveMode.Input:
                    pDir.SelectedIndex = 0;
                    pVal.IsEnabled = false;
                    break;
                case ProviderGpioPinDriveMode.Output:
                    pDir.SelectedIndex = 1;
                    pVal.IsEnabled = true;
                    break;
                default:
                    break;
            }

            switch (gpioPinProvider.Read())
            {
                case ProviderGpioPinValue.High:
                    pVal.SelectedIndex = 0;
                    break;
                case ProviderGpioPinValue.Low:
                    pVal.SelectedIndex = 1;
                    break;
                default:
                    break;
            }

        }

        private void PDir_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            gpioPinProvider = upGpioControllerProvider.OpenPinProvider(int.Parse(pList.SelectedValue.ToString()), ProviderGpioSharingMode.SharedReadOnly);

            switch (pDir.SelectedIndex)
            {
                case 0:
                    gpioPinProvider.SetDriveMode(ProviderGpioPinDriveMode.Input);
                    pVal.IsEnabled = false;
                    break;
                case 1:
                    gpioPinProvider.SetDriveMode(ProviderGpioPinDriveMode.Output);
                    pVal.IsEnabled = true;
                    break;
                default:
                    break;
            }

            switch (gpioPinProvider.Read())
            {
                case ProviderGpioPinValue.High:
                    pVal.SelectedIndex = 0;
                    break;
                case ProviderGpioPinValue.Low:
                    pVal.SelectedIndex = 1;
                    break;
                default:
                    break;
            }
        }

        private void PVal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            gpioPinProvider = upGpioControllerProvider.OpenPinProvider(int.Parse(pList.SelectedValue.ToString()), ProviderGpioSharingMode.SharedReadOnly);
         
            switch (pVal.SelectedIndex)
            {
                case 0:
                    gpioPinProvider.Write(ProviderGpioPinValue.High);
                    break;
                case 1:
                    gpioPinProvider.Write(ProviderGpioPinValue.Low);
                    break;
                default:
                    break;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            gpioPinProvider = upGpioControllerProvider.OpenPinProvider(int.Parse(pList.SelectedValue.ToString()), ProviderGpioSharingMode.SharedReadOnly);

            TBIOStat.Text = gpioPinProvider.Read().ToString();
        }
    }
}
