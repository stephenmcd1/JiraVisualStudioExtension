using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace JiraVisualStudioExtension.Options
{
    public partial class CredentialOptionsControl : UserControl
    {
        public CredentialOptionsControl()
        {
            InitializeComponent();
        }

        public void Initialize(string userName, string apiToken, string subdomain)
        {
            UserNameBox.Text = userName ?? "";
            ApiTokenBox.Password = apiToken ?? "";
            SubdomainBox.Text = subdomain ?? "";
            StatusText.Text = "";
        }

        public string UserName => UserNameBox.Text;
        public string ApiToken => ApiTokenBox.Password;
        public string Subdomain => SubdomainBox.Text;

        private async void TestConnectionButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var userName = UserName;
            var apiToken = ApiToken;
            var subdomain = Subdomain;

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(apiToken) || string.IsNullOrWhiteSpace(subdomain))
            {
                StatusText.Foreground = Brushes.Red;
                StatusText.Text = "All fields are required.";
                return;
            }

            TestConnectionButton.IsEnabled = false;
            StatusText.Foreground = System.Windows.SystemColors.ControlTextBrush;
            StatusText.Text = "Connecting...";

            try
            {
                var displayName = await Task.Run(() => TestLogOn(userName, apiToken, subdomain));
                if (displayName != null)
                {
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 0));
                    StatusText.Text = $"Connected successfully as {displayName}.";
                }
                else
                {
                    StatusText.Foreground = Brushes.Red;
                    StatusText.Text = "Connection failed. Check your credentials and subdomain.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Foreground = Brushes.Red;
                StatusText.Text = $"Connection failed: {ex.Message}";
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
            }
        }

        private static string TestLogOn(string userName, string apiToken, string subdomain)
        {
            using var client = new WebClient();
            client.BaseAddress = $"https://{subdomain}.atlassian.net/";
            client.Headers[HttpRequestHeader.Authorization] =
                $"Basic {Convert.ToBase64String(Encoding.Default.GetBytes($"{userName}:{apiToken}"))}";
            client.Encoding = Encoding.UTF8;

            try
            {
                var resp = JToken.Parse(client.DownloadString("rest/api/3/myself"));
                return (string)resp["displayName"];
            }
            catch
            {
                return null;
            }
        }
    }
}
