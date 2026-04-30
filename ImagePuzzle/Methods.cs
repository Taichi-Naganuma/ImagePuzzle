using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Windows;
using static ImagePuzzle.MainWindow;
using ISPoint = SixLabors.ImageSharp.Point;
using ISSize = SixLabors.ImageSharp.Size;
using ISResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;
using ISSystemFonts = SixLabors.Fonts.SystemFonts;
using ISHorizontalAlignment = SixLabors.Fonts.HorizontalAlignment;
using ISVerticalAlignment = SixLabors.Fonts.VerticalAlignment;

namespace ImagePuzzle
{
    public class Methods
    {
        private readonly Property _property;
        private readonly ItemService _itemService = new();

        public Methods(Property property)
        {
            _property = property;
        }

        public List<MethodItem> AddMethod()
        {
            return new List<MethodItem>
            {
                new MethodItem { DisplayNameKey = "Method_Resize",   DisplayName = LocalizationService.Get("Method_Resize"),   ExecuteAsync = ResizeAsync },
                new MethodItem { DisplayNameKey = "Method_Convert",  DisplayName = LocalizationService.Get("Method_Convert"),  ExecuteAsync = ConvertAsync },
                new MethodItem { DisplayNameKey = "Method_Compress", DisplayName = LocalizationService.Get("Method_Compress"), ExecuteAsync = CompressAsync },
                new MethodItem { DisplayNameKey = "Method_Watermark",DisplayName = LocalizationService.Get("Method_Watermark"),ExecuteAsync = WatermarkAsync },
            };
        }

        private MainWindow? GetMainWindow() =>
            Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

        private async Task ResizeAsync(IProgress<string> progress)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var files = mw.FileItems.Where(f => f.Path != null).Select(f => f.Path!).ToList();
            var settings = AppSettings.Load();

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    progress.Report(Path.GetFileName(file));
                    using var image = Image.Load(file);

                    if (settings.ResizeMode == "percent")
                    {
                        int w = (int)(image.Width * settings.ResizePercent / 100.0);
                        int h = (int)(image.Height * settings.ResizePercent / 100.0);
                        image.Mutate(x => x.Resize(w, h));
                    }
                    else
                    {
                        var resizeOpts = new ResizeOptions
                        {
                            Size = new ISSize(settings.ResizeWidth, settings.ResizeHeight),
                            Mode = settings.ResizeKeepAspect ? ISResizeMode.Max : ISResizeMode.Stretch
                        };
                        image.Mutate(x => x.Resize(resizeOpts));
                    }

                    string ext = Path.GetExtension(file);
                    string baseName = Path.GetFileNameWithoutExtension(file) + "_resized";
                    string outPath = _itemService.SaveImage(mw.FolderPath!, baseName, ext);
                    SaveWithOriginalFormat(image, outPath, ext, settings);
                    Application.Current.Dispatcher.Invoke(() => _itemService.AddAfterItem(outPath));
                }
            });
        }

        private async Task ConvertAsync(IProgress<string> progress)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var files = mw.FileItems.Where(f => f.Path != null).Select(f => f.Path!).ToList();
            var settings = AppSettings.Load();

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    progress.Report(Path.GetFileName(file));
                    using var image = Image.Load(file);
                    string newExt = "." + settings.ConvertFormat;
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string outPath = _itemService.SaveImage(mw.FolderPath!, baseName, newExt);
                    SaveWithFormat(image, outPath, settings.ConvertFormat, settings);
                    Application.Current.Dispatcher.Invoke(() => _itemService.AddAfterItem(outPath));
                }
            });
        }

        private async Task CompressAsync(IProgress<string> progress)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var files = mw.FileItems.Where(f => f.Path != null).Select(f => f.Path!).ToList();
            var settings = AppSettings.Load();

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    progress.Report(Path.GetFileName(file));
                    using var image = Image.Load(file);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string baseName = Path.GetFileNameWithoutExtension(file) + "_compressed";
                    string outPath = _itemService.SaveImage(mw.FolderPath!, baseName, ext);
                    SaveCompressed(image, outPath, ext, settings);
                    Application.Current.Dispatcher.Invoke(() => _itemService.AddAfterItem(outPath));
                }
            });
        }

        private async Task WatermarkAsync(IProgress<string> progress)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var files = mw.FileItems.Where(f => f.Path != null).Select(f => f.Path!).ToList();
            var settings = AppSettings.Load();

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    progress.Report(Path.GetFileName(file));
                    using var image = Image.Load<Rgba32>(file);

                    if (settings.WatermarkType == "text" && !string.IsNullOrEmpty(settings.WatermarkText))
                        ApplyTextWatermark(image, settings);
                    else if (settings.WatermarkType == "image" && File.Exists(settings.WatermarkImagePath))
                        ApplyImageWatermark(image, settings);

                    string ext = Path.GetExtension(file);
                    string baseName = Path.GetFileNameWithoutExtension(file) + "_watermarked";
                    string outPath = _itemService.SaveImage(mw.FolderPath!, baseName, ext);
                    SaveWithOriginalFormat(image, outPath, ext, settings);
                    Application.Current.Dispatcher.Invoke(() => _itemService.AddAfterItem(outPath));
                }
            });
        }

        private static void ApplyTextWatermark(Image<Rgba32> image, AppSettings settings)
        {
            var families = ISSystemFonts.Families.ToList();
            if (!families.Any()) return;
            var font = families.First().CreateFont(settings.WatermarkFontSize);
            float alpha = settings.WatermarkOpacity / 100f;
            var color = Color.FromRgba(255, 255, 255, (byte)(alpha * 255));

            var textOptions = new RichTextOptions(font)
            {
                HorizontalAlignment = ISHorizontalAlignment.Right,
                VerticalAlignment = ISVerticalAlignment.Bottom,
                Origin = GetWatermarkOrigin(image, settings)
            };

            image.Mutate(x => x.DrawText(textOptions, settings.WatermarkText, color));
        }

        private static void ApplyImageWatermark(Image<Rgba32> image, AppSettings settings)
        {
            using var watermark = Image.Load<Rgba32>(settings.WatermarkImagePath!);
            float alpha = settings.WatermarkOpacity / 100f;
            watermark.Mutate(x => x.Opacity(alpha));
            var point = GetWatermarkPoint(image, watermark.Size, settings);
            image.Mutate(x => x.DrawImage(watermark, point, 1f));
        }

        private static System.Numerics.Vector2 GetWatermarkOrigin(Image image, AppSettings settings)
        {
            int margin = 10;
            return settings.WatermarkPosition switch
            {
                "topleft"     => new(margin, margin),
                "topright"    => new(image.Width - margin, margin),
                "bottomleft"  => new(margin, image.Height - margin),
                "center"      => new(image.Width / 2f, image.Height / 2f),
                _             => new(image.Width - margin, image.Height - margin),
            };
        }

        private static ISPoint GetWatermarkPoint(Image image, ISSize wmSize, AppSettings settings)
        {
            int margin = 10;
            return settings.WatermarkPosition switch
            {
                "topleft"    => new ISPoint(margin, margin),
                "topright"   => new ISPoint(image.Width - wmSize.Width - margin, margin),
                "bottomleft" => new ISPoint(margin, image.Height - wmSize.Height - margin),
                "center"     => new ISPoint((image.Width - wmSize.Width) / 2, (image.Height - wmSize.Height) / 2),
                _            => new ISPoint(image.Width - wmSize.Width - margin, image.Height - wmSize.Height - margin),
            };
        }

        private static void SaveWithOriginalFormat(Image image, string path, string ext, AppSettings settings)
        {
            SaveWithFormat(image, path, ext.TrimStart('.').ToLowerInvariant(), settings);
        }

        private static void SaveWithFormat(Image image, string path, string format, AppSettings settings)
        {
            switch (format)
            {
                case "jpg":
                case "jpeg":
                    image.Save(path, new JpegEncoder { Quality = settings.JpgQuality });
                    break;
                case "png":
                    image.Save(path, new PngEncoder());
                    break;
                case "webp":
                    image.Save(path, new WebpEncoder { Quality = settings.WebpQuality });
                    break;
                case "bmp":
                    image.Save(path, new BmpEncoder());
                    break;
                default:
                    image.Save(path, new JpegEncoder { Quality = settings.JpgQuality });
                    break;
            }
        }

        private static void SaveCompressed(Image image, string path, string ext, AppSettings settings)
        {
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    image.Save(path, new JpegEncoder { Quality = settings.CompressJpgQuality });
                    break;
                case ".png":
                    image.Save(path, new PngEncoder { CompressionLevel = (PngCompressionLevel)settings.CompressPngLevel });
                    break;
                case ".webp":
                    image.Save(path, new WebpEncoder { Quality = settings.WebpQuality });
                    break;
                default:
                    image.Save(path, new JpegEncoder { Quality = settings.CompressJpgQuality });
                    break;
            }
        }
    }
}
