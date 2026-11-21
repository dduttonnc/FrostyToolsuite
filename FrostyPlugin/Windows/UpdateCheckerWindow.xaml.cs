using System;
using System.Net.Http;
using System.Windows;

namespace Frosty.Core.Windows
{
    /// <summary>
    /// Interaction logic for UpdateCheckerWindow.xaml
    /// </summary>
    public partial class UpdateCheckerWindow
    {
        public UpdateCheckerWindow()
        {
            InitializeComponent();

            Window mainWin = Application.Current.MainWindow;
            if (mainWin != null)
            {
                Icon = mainWin.Icon;
            }

            infoTextBox.Text = $"You are using an outdated version of {App.Title}\n\nWould you like to download the latest version?";
            infoTextBox.TextAlignment = TextAlignment.Center;

            ChangeLog();
        }

        private async void ChangeLog()
        {
            try
            {
                string changeLogRaw = await new HttpClient().GetStringAsync(UpdateCheckerUtils.ChangeLogUrl);

                // [DAI fix] everything newer than this build's entry ("v<version> - <build date>"), or all of it
                int index = changeLogRaw.IndexOf(" - " + App.BuildVersion, StringComparison.Ordinal);
                if (index >= 0)
                    index = changeLogRaw.LastIndexOf('\n', index) + 1;

                string changeLog = index > 0 ? changeLogRaw.Substring(0, index) : changeLogRaw;

                changelogTextBox.Text = changeLog;
            }
            catch
            {
                changelogTextBox.Text = "Unable to retrieve changelog.";
            }
        }

        private void yesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void noButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
