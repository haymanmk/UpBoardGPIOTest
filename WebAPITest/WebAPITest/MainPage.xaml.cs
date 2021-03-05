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
using Windows.UI.Core;
using Windows.ApplicationModel.Core;
using Windows.Web.Http;
using Windows.Storage.Streams;

// 空白頁項目範本已記錄在 https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x404

namespace WebAPITest
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {

        HttpClient httpClient = new HttpClient();

        public MainPage()
        {
            this.InitializeComponent();

            __Configure();
        }

        private void __Configure()
        {
            var headers = httpClient.DefaultRequestHeaders;

            string header = "ie";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            header = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }
            AppendText("Configuration success");

            tbURI.Text = "https://cloud.iexapis.com/stable/stock/aapl/quote/latestPrice?token=pk_5848c1c76cec488093ed2f609540f40c";
            tbData.Text = "{\"panellist\": [\"B615232865000037\"]}";
}

        private async Task Get()
        {
            Uri uri = GetUri();
            if (uri == null)
            {
                return;
            }

            //Send the GET request asynchronously and retrieve the response as a string.
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                httpResponse = await httpClient.GetAsync(uri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                await AppendText(httpResponse.StatusCode.ToString());
                await AppendText(httpResponseBody);
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
                await AppendText(httpResponseBody);
            }
        }

        private async Task Send()
        {
            Uri uri = GetUri();
            if (uri == null)
            {
                return;
            }

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            HttpResponseMessage result = await httpClient.SendRequestAsync(httpRequestMessage);
            await AppendText(result.ToString());

        }

        private async Task Post()
        {
            Uri uri = GetUri();
            if (uri == null)
            {
                return;
            }
            try
            {
                // Construct the JSON to post.
                HttpStringContent content = new HttpStringContent(
                    tbData.Text.ToString(),
                    UnicodeEncoding.Utf8,
                    "application/json");

                // Post the JSON and wait for a response.
                HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(
                    uri,
                    content);

                // Make sure the post succeeded, and write out the response.
                httpResponseMessage.EnsureSuccessStatusCode();
                var httpResponseBody = await httpResponseMessage.Content.ReadAsStringAsync();
                await AppendText(httpResponseBody);
            }
            catch (Exception ex)
            {
                await AppendText(ex.ToString());
            }

        }

        private Uri GetUri()
        {
            if (tbURI == null)
            {
                NotifyDialog("Please define a URI.");
                return null;
            }

            Uri uri = new Uri(tbURI.Text.ToString());

            return uri;
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

        private async Task AppendText(string Msg)
        {
            if (!Msg.EndsWith("\r\n"))
            {
                Msg += "\r\n";
            }
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                new DispatchedHandler(() =>
                {
                    tbDebugMsg.Text += Msg;
                    ScrollToBottom(tbDebugMsg);
                }
                ));
        }

        private void ScrollToBottom(TextBox textBox)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(textBox, 0);

            try
            {
                for (int i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
                {
                    object obj = VisualTreeHelper.GetChild(grid, i);
                    if (!(obj is ScrollViewer)) continue;
                    ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f, true);
                    break;
                }
            }
            catch (Exception ex)
            {
                // continue
            }
        }

        private async void BtGet_Click(object sender, RoutedEventArgs e)
        {
            await Get();
        }

        private async void BtSend_Click(object sender, RoutedEventArgs e)
        {
            await Send();
        }

        private async void BtPost_Click(object sender, RoutedEventArgs e)
        {
            await Post();
        }
    }
}
