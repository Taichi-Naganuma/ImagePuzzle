using System.IO;
using System.Text.Json;

namespace ImagePuzzle
{
    public class AppSettings
    {
        public string? OutputFolderPath { get; set; }
        public string? Language { get; set; }
        public bool OpenFolderAfterExecution { get; set; }
        public string? LicenseKey { get; set; }

        // Resize settings
        public string ResizeMode { get; set; } = "size"; // "size" or "percent"
        public int ResizeWidth { get; set; } = 800;
        public int ResizeHeight { get; set; } = 800;
        public bool ResizeKeepAspect { get; set; } = true;
        public int ResizePercent { get; set; } = 50;

        // Convert settings
        public string ConvertFormat { get; set; } = "jpg"; // "jpg","png","webp","bmp"
        public int JpgQuality { get; set; } = 85;
        public int WebpQuality { get; set; } = 80;

        // Compress settings
        public int CompressJpgQuality { get; set; } = 75;
        public int CompressPngLevel { get; set; } = 6;

        // Watermark settings
        public string WatermarkType { get; set; } = "text"; // "text" or "image"
        public string WatermarkText { get; set; } = "© 2026";
        public int WatermarkFontSize { get; set; } = 24;
        public string WatermarkColor { get; set; } = "#FFFFFF";
        public int WatermarkOpacity { get; set; } = 70;
        public string WatermarkPosition { get; set; } = "bottomright"; // topleft/topright/bottomleft/bottomright/center
        public string? WatermarkImagePath { get; set; }

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImagePuzzle");
        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
