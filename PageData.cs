using System.IO;
using System.Windows.Media.Imaging;
using System.Drawing;
using System;

namespace PdfImageThresholdingTool
{
    public sealed class PageData
    {
        public System.Windows.Media.ImageSource Thumbnail { get; }
        public bool IsConvertTarget { get; set; }
        public int PageCount { get; }

        public PageData(int pageCount, Bitmap image) {
            PageCount = pageCount;
            IsConvertTarget = false;

            // Image -> ImageSourceへの変換
            if (image != null) {
                using Image thumbnail =
                    image.GetThumbnailImage(120, 120, () => false, IntPtr.Zero);
                using MemoryStream ms = new MemoryStream();
                thumbnail.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                ms.Seek(0, SeekOrigin.Begin);

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = ms;
                bitmapImage.EndInit();

                Thumbnail = bitmapImage;

                bitmapImage = null;
            }
        }
    }
}
