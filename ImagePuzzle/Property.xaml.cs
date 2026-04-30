using System.Windows;
using System.Windows.Controls;

namespace ImagePuzzle
{
    public partial class Property : Window
    {
        public Property()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = AppSettings.Load();
            ResizeWidthBox.Text = s.ResizeWidth.ToString();
            ResizeHeightBox.Text = s.ResizeHeight.ToString();
            ResizePercentBox.Text = s.ResizePercent.ToString();
            ResizeKeepAspectBox.IsChecked = s.ResizeKeepAspect;
            ResizeSizeMode.IsChecked = s.ResizeMode == "size";
            ResizePercentMode.IsChecked = s.ResizeMode == "percent";

            foreach (ComboBoxItem item in ConvertFormatBox.Items)
                if (item.Tag?.ToString() == s.ConvertFormat) { ConvertFormatBox.SelectedItem = item; break; }
            if (ConvertFormatBox.SelectedItem == null) ConvertFormatBox.SelectedIndex = 0;
            JpgQualityBox.Text = s.JpgQuality.ToString();
            WebpQualityBox.Text = s.WebpQuality.ToString();

            CompressJpgBox.Text = s.CompressJpgQuality.ToString();
            CompressPngBox.Text = s.CompressPngLevel.ToString();

            WatermarkTextBox.Text = s.WatermarkText;
            WatermarkFontSizeBox.Text = s.WatermarkFontSize.ToString();
            WatermarkOpacityBox.Text = s.WatermarkOpacity.ToString();
            foreach (ComboBoxItem item in WatermarkPositionBox.Items)
                if (item.Tag?.ToString() == s.WatermarkPosition) { WatermarkPositionBox.SelectedItem = item; break; }

            OpenFolderCheckBox.IsChecked = s.OpenFolderAfterExecution;
            LanguageComboBox.SelectedIndex = (s.Language == LocalizationService.English) ? 1 : 0;
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            var s = AppSettings.Load();

            s.ResizeMode = ResizePercentMode.IsChecked == true ? "percent" : "size";
            s.ResizeWidth = int.TryParse(ResizeWidthBox.Text, out int w) && w > 0 ? w : 800;
            s.ResizeHeight = int.TryParse(ResizeHeightBox.Text, out int h) && h > 0 ? h : 800;
            s.ResizeKeepAspect = ResizeKeepAspectBox.IsChecked == true;
            s.ResizePercent = int.TryParse(ResizePercentBox.Text, out int pct) && pct > 0 ? pct : 50;

            s.ConvertFormat = (ConvertFormatBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "jpg";
            s.JpgQuality = int.TryParse(JpgQualityBox.Text, out int jq) ? Math.Clamp(jq, 1, 100) : 85;
            s.WebpQuality = int.TryParse(WebpQualityBox.Text, out int wq) ? Math.Clamp(wq, 1, 100) : 80;

            s.CompressJpgQuality = int.TryParse(CompressJpgBox.Text, out int cj) ? Math.Clamp(cj, 1, 100) : 75;
            s.CompressPngLevel = int.TryParse(CompressPngBox.Text, out int cp) ? Math.Clamp(cp, 0, 9) : 6;

            s.WatermarkText = WatermarkTextBox.Text;
            s.WatermarkFontSize = int.TryParse(WatermarkFontSizeBox.Text, out int fs) && fs > 0 ? fs : 24;
            s.WatermarkOpacity = int.TryParse(WatermarkOpacityBox.Text, out int op) ? Math.Clamp(op, 0, 100) : 70;
            s.WatermarkPosition = (WatermarkPositionBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "bottomright";

            s.OpenFolderAfterExecution = OpenFolderCheckBox.IsChecked == true;
            s.Save();
            Hide();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                string lang = item.Tag?.ToString() == "en" ? LocalizationService.English : LocalizationService.Japanese;
                var s = AppSettings.Load();
                s.Language = lang;
                s.Save();
                LocalizationService.Apply(lang);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
