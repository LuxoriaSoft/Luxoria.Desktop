using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Luxoria
{
    public struct Color : IEquatable<Color>
    {
        private uint rgba;

        public Color(uint rgba) => this.rgba = rgba;

        public Color(byte r, byte g, byte b, byte a = 255)
            => rgba = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;

        public Color(float r, float g, float b, float a = 1.0f)
            : this((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255)) { }

        public Color(string hex)
        {
            if (!Regex.IsMatch(hex, @"^#?([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$"))
                throw new ArgumentException("Invalid hex color format");

            hex = hex.StartsWith("#") ? hex.Substring(1) : hex;
            hex += hex.Length == 6 ? "FF" : string.Empty;

            rgba = uint.Parse(hex, NumberStyles.HexNumber);
        }

        public uint Rgba => rgba;
        public byte R => (byte)((rgba >> 24) & 0xFF);
        public byte G => (byte)((rgba >> 16) & 0xFF);
        public byte B => (byte)((rgba >> 8) & 0xFF);
        public byte A => (byte)(rgba & 0xFF);

        public (float H, float S, float B) ToHSB()
        {
            float r = R / 255f;
            float g = G / 255f;
            float b = B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float h, s, v = max;

            float d = max - min;
            s = max == 0 ? 0 : d / max;

            if (max == min)
            {
                h = 0;
            }
            else
            {
                if (max == r)
                {
                    h = (g - b) / d + (g < b ? 6 : 0);
                }
                else if (max == g)
                {
                    h = (b - r) / d + 2;
                }
                else
                {
                    h = (r - g) / d + 4;
                }
                h /= 6;
            }

            return (h * 360, s, v);
        }

        public static Color FromHSL(float h, float s, float l, float alpha = 1.0f)
        {
            h = h % 360;
            s = Math.Clamp(s, 0.0f, 1.0f);
            l = Math.Clamp(l, 0.0f, 1.0f);

            float c = (1 - Math.Abs(2 * l - 1)) * s;
            float x = c * (1 - Math.Abs((h / 60 % 2) - 1));
            float m = l - c / 2;

            float r = 0, g = 0, b = 0;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            r = (r + m) * 255;
            g = (g + m) * 255;
            b = (b + m) * 255;

            return new Color((byte)r, (byte)g, (byte)b, (byte)(alpha * 255));
        }

        public static Color FromHSB(float h, float s, float b, float alpha = 1.0f)
        {
            h = h % 360;
            s = Math.Clamp(s, 0.0f, 1.0f);
            b = Math.Clamp(b, 0.0f, 1.0f);

            float c = s * b;
            float x = c * (1 - Math.Abs((h / 60 % 2) - 1));
            float m = b - c;

            float r = 0, g = 0, bB = 0;

            if (h < 60) { r = c; g = x; bB = 0; }
            else if (h < 120) { r = x; g = c; bB = 0; }
            else if (h < 180) { r = 0; g = c; bB = x; }
            else if (h < 240) { r = 0; g = x; bB = c; }
            else if (h < 300) { r = x; g = 0; bB = c; }
            else { r = c; g = 0; bB = x; }

            r = (r + m) * 255;
            g = (g + m) * 255;
            bB = (bB + m) * 255;

            return new Color((byte)Math.Round(r), (byte)Math.Round(g), (byte)Math.Round(bB), (byte)(alpha * 255));
        }



        public (float H, float S, float L) ToHSL()
        {
            float r = R / 255f;
            float g = G / 255f;
            float b = B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float h, s, l = (max + min) / 2;

            if (max == min)
            {
                h = s = 0;
            }
            else
            {
                float d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r)
                {
                    h = (g - b) / d + (g < b ? 6 : 0);
                }
                else if (max == g)
                {
                    h = (b - r) / d + 2;
                }
                else
                {
                    h = (r - g) / d + 4;
                }
                h /= 6;
            }

            return (h * 360, s, l);
        }

        public bool Equals(Color other) => rgba == other.rgba;
        public override bool Equals(object obj) => obj is Color other && Equals(other);
        public override int GetHashCode() => rgba.GetHashCode();
        public static bool operator ==(Color left, Color right) => left.Equals(right);
        public static bool operator !=(Color left, Color right) => !left.Equals(right);
        public override string ToString() => $"Color(R: {R}, G: {G}, B: {B}, A: {A})";
    }

    public class ImageUploader
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task UploadWriteableBitmapAsync(string url, WriteableBitmap writeableBitmap, string galleryId, string fileName)
        {
            try
            {
                using (var content = new MultipartFormDataContent())
                {
                    // Add the relatedToGallery field
                    var galleryIdContent = new StringContent(galleryId);
                    galleryIdContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        Name = "\"relatedToGallery\""
                    };
                    content.Add(galleryIdContent);

                    using (var stream = new MemoryStream())
                    {
                        BitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                        encoder.Save(stream);
                        stream.Seek(0, SeekOrigin.Begin);

                        var fileContent = new StreamContent(stream);
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                        fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                        {
                            Name = "\"file\"",
                            FileName = fileName,
                        };

                        content.Add(fileContent);
                        Console.WriteLine($"Content-Type: {content.Headers.ContentType}");

                        var response = await client.PostAsync(url, content);
                        var responseContent = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Image uploaded successfully.");
                        }
                        else
                        {
                            Console.WriteLine($"Image upload failed. Status code: {response.StatusCode}");
                            Console.WriteLine($"Response content: {responseContent}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

        public partial class MainWindow : Window
    {
        string API_URL = "http://localhost:3000";
            
        private BitmapImage originalImage;
        private BitmapImage compressedImage;
        private double currentSaturationFactor = 1;
        private double currentTintFactor = 0;
        private double currentExposureFactor = 0;
        private string fileName = "";
        private Gallery selectedGallery;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg;*.ARW)|*.png;*.jpeg;*.jpg;*.ARW|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                originalImage = new BitmapImage(new Uri(openFileDialog.FileName));
                fileName = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);

                compressedImage = new BitmapImage();
                compressedImage.BeginInit();
                compressedImage.UriSource = new Uri(openFileDialog.FileName);
                compressedImage.DecodePixelWidth = originalImage.PixelWidth / 10;
                compressedImage.DecodePixelHeight = originalImage.PixelHeight / 10;
                compressedImage.EndInit();

                img.Source = compressedImage;
            }
        }

        private void BasicExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (originalImage != null)
            {
                WriteableBitmap exportImage = new WriteableBitmap(originalImage);

                ApplyAdjustmentsToImage(exportImage);

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "JPEG Image (*.jpeg;*.jpg)|*.jpeg;*.jpg|PNG Image (*.png)|*.png|Bitmap Image (*.bmp)|*.bmp",
                    DefaultExt = "jpg",
                    FilterIndex = 1
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    BitmapEncoder encoder;
                    switch (saveFileDialog.FilterIndex)
                    {
                        case 1:
                            encoder = new JpegBitmapEncoder();
                            break;
                        case 2:
                            encoder = new PngBitmapEncoder();
                            break;
                        case 3:
                            encoder = new BmpBitmapEncoder();
                            break;
                        default:
                            encoder = new PngBitmapEncoder();
                            break;
                    }

                    encoder.Frames.Add(BitmapFrame.Create(exportImage));

                    using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }
                }
            }
        }

        private void ApplyAdjustmentsToImage(WriteableBitmap image)
        {
            int width = image.PixelWidth;
            int height = image.PixelHeight;
            int stride = width * ((image.Format.BitsPerPixel + 7) / 8);
            byte[] pixelData = new byte[height * stride];
            image.CopyPixels(pixelData, stride, 0);

            for (int i = 0; i < pixelData.Length; i += 4)
            {
                byte b = pixelData[i];
                byte g = pixelData[i + 1];
                byte r = pixelData[i + 2];
                byte a = pixelData[i + 3];

                Color originalColor = new Color(r, g, b, a);
                Color adjustedColor = AdjustColor(originalColor);

                pixelData[i] = adjustedColor.B;
                pixelData[i + 1] = adjustedColor.G;
                pixelData[i + 2] = adjustedColor.R;
                pixelData[i + 3] = adjustedColor.A;
            }

            image.WritePixels(new Int32Rect(0, 0, width, height), pixelData, stride, 0);
        }

        private void AdjustImage()
        {
            if (compressedImage == null) return;

            int width = compressedImage.PixelWidth;
            int height = compressedImage.PixelHeight;
            int stride = width * ((compressedImage.Format.BitsPerPixel + 7) / 8);
            byte[] pixelData = new byte[height * stride];
            compressedImage.CopyPixels(pixelData, stride, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * stride + x * 4;
                    Color color = new Color(pixelData[index + 2], pixelData[index + 1], pixelData[index], pixelData[index + 3]);
                    color = AdjustColor(color);
                    pixelData[index + 2] = color.R;
                    pixelData[index + 1] = color.G;
                    pixelData[index] = color.B;
                    pixelData[index + 3] = color.A;
                }
            }

            WriteableBitmap writeableBitmap = new WriteableBitmap(compressedImage);
            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, stride, 0);
            img.Source = writeableBitmap;
        }

        private Color AdjustColor(Color color)
        {
            var hsl = color.ToHSB();
            hsl.H += (float)currentTintFactor;
            hsl.S *= (float)currentSaturationFactor;
            hsl.B = Math.Min(1.0f, hsl.B * (float)Math.Pow(2, currentExposureFactor));

            return Color.FromHSB(hsl.H, hsl.S, hsl.B);
        }

        private void SaturationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            currentSaturationFactor = e.NewValue / 50;
            AdjustImage();
        }

        private void TintSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            currentTintFactor = e.NewValue;
            AdjustImage();
        }

        private void ExposureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            currentExposureFactor = e.NewValue;
            AdjustImage();
        }

        private void CompressorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateCompressedImage((int)e.NewValue);
        }

        private void UpdateCompressedImage(int compressionFactor)
        {
            if (originalImage == null) return;

            compressedImage = new BitmapImage();
            compressedImage.BeginInit();
            compressedImage.UriSource = new Uri(originalImage.UriSource.AbsoluteUri);
            compressedImage.DecodePixelWidth = originalImage.PixelWidth / compressionFactor;
            compressedImage.DecodePixelHeight = originalImage.PixelHeight / compressionFactor;
            compressedImage.EndInit();

            img.Source = compressedImage;
            AdjustImage();
        }

        private async void WebExportButton_Click(object sender, RoutedEventArgs e, String galleryId, string fileName)
        {
            string url = API_URL + "/api/upload";

            MessageBox.Show("Exporting image to web... | GalleryID: " + galleryId);

            if (originalImage != null)
            {
                WriteableBitmap exportImage = new WriteableBitmap(originalImage);

                ApplyAdjustmentsToImage(exportImage);

                await ImageUploader.UploadWriteableBitmapAsync(url, exportImage, galleryId, fileName);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ExportOptionsDialog(fileName);
            if (dialog.ShowDialog() == true)
            {
                switch (dialog.SelectedExportType)
                {
                    case "Web Export":
                        WebExportButton_Click(sender, e, dialog.SelectedGallery.Id, fileName);
                        break;
                    case "Basic Export":
                        BasicExportButton_Click(sender, e);
                        break;
                }
            }
        }

    }
}