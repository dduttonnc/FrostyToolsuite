using Frosty.Core;
using FrostySdk;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace FrostyModManager.Controls
{
    public partial class SplashHeader : UserControl
    {
        public SplashHeader()
        {
            InitializeComponent();
            versionTextBlock.Text = Frosty.Core.App.Version;
        }

        public void ShowProfile()
        {
            profileTextBlock.Text = ProfilesLibrary.DisplayName;
            bannerImage.Source = LoadBanner(ProfilesLibrary.Banner);
        }

        private static BitmapImage LoadBanner(byte[] banner)
        {
            if (banner == null || banner.Length == 0)
                return null;
            BitmapImage bmp = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(banner))
            {
                bmp.BeginInit();
                bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = null;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();
            return bmp;
        }
    }
}
