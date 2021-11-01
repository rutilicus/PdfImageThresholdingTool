using System;
using System.Windows;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.Windows.Data;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.Filters;
using PdfSharpCore.Drawing;
using Microsoft.Win32;

namespace PdfImageThresholdingTool
{
    public partial class MainWindow : Window
    {
        private string OriginalFilePath;
        private readonly ObservableCollection<PageData> PageData =
            new ObservableCollection<PageData>();
        private int Threshold = 70;
        private int BoldDistance = 0;
        public MainWindow() {
            InitializeComponent();

            // ページ情報表示グリッドへのソース設定
            imageView.ItemsSource = PageData;
        }

        private void Window_DragOver(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                Path.GetExtension(((string[])e.Data.GetData(DataFormats.FileDrop))[0]) == ".pdf") {
                e.Effects = DragDropEffects.Copy;
            } else {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                OpenPdf(((string[])e.Data.GetData(DataFormats.FileDrop))[0]);
            }
        }

        private void OpenPdf(string filePath) {
            // 一度開いている情報を閉じる
            PageData.Clear();

            // PDFファイルを開き、画像抽出
            OriginalFilePath = filePath;
            using PdfDocument document = PdfReader.Open(
                OriginalFilePath, PdfDocumentOpenMode.Import);

            int pageCount = 0;

            foreach (PdfPage page in document.Pages) {
                using Bitmap pageBitmap = ExtractBitmapFromPage(page);
                if (pageBitmap != null) {
                    PageData.Add(new PageData(pageCount, pageBitmap));
                }

                pageCount++;
            }

            // PDF出力ボタン活性化
            saveButton.IsEnabled = true;
        }

        private Bitmap ExtractBitmapFromPage(PdfPage page) {
            PdfDictionary resources = page.Elements.GetDictionary("/Resources");
            if (resources != null) {
                PdfDictionary xObjects =
                    resources.Elements.GetDictionary("/XObject");
                if (xObjects != null && xObjects.Elements.Count != 0) {
                    foreach (PdfItem item in xObjects.Elements.Values) {
                        if (item is PdfReference reference) {
                            if (reference.Value is PdfDictionary xObject &&
                                xObject.Elements.GetString("/Subtype") == "/Image") {
                                Bitmap created = CreatePageBitmap(xObject);
                                if (created != null) {
                                    return created;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private Bitmap CreatePageBitmap(PdfDictionary image) {
            string filter = image.Elements.GetName("/Filter");
            return filter switch {
                "/DCTDecode" => CreateJpegPageBitmap(image),
                "/FlateDecode" => CreateTiffPageBitmap(image),
                _ => null,
            };
        }

        private Bitmap CreateJpegPageBitmap(PdfDictionary image) {
            using MemoryStream ms = new MemoryStream(image.Stream.Value);
            return (Bitmap)Image.FromStream(ms);
        }

        private Bitmap CreateTiffPageBitmap(PdfDictionary image) {
            int width = image.Elements.GetInteger(PdfImage.Keys.Width);
            int height = image.Elements.GetInteger(PdfImage.Keys.Height);

            byte[] imageData;
            if (image.Stream.TryUnfilter()) {
                imageData = image.Stream.Value;
            } else {
                FlateDecode flate = new FlateDecode();
                imageData = flate.Decode(image.Stream.Value);
            }

            int bitsPerComponent = 0;
            while (imageData.Length - (width * height * bitsPerComponent / 8) != 0) {
                bitsPerComponent++;
            }

            PixelFormat pixelFormat;

            switch (bitsPerComponent) {
                case 1:
                    pixelFormat =
                        PixelFormat.Format1bppIndexed;
                    break;
                case 8:
                    pixelFormat =
                        PixelFormat.Format8bppIndexed;
                    break;
                case 24:
                    pixelFormat =
                        PixelFormat.Format24bppRgb;
                    break;
                default:
                    return null;
            }

            Bitmap bmp = new Bitmap(width, height, pixelFormat);
            BitmapData bmpData =
                bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    pixelFormat);
            int length = (int)Math.Ceiling(width * bitsPerComponent / 8.0);

            for (int i = 0; i < height; i++) {
                int offset = i * length;
                int scanOffset = i * bmpData.Stride;
                Marshal.Copy(
                    imageData, offset,
                    new IntPtr(bmpData.Scan0.ToInt64() + scanOffset), length);
            }

            bmp.UnlockBits(bmpData);

            return bmp;
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        private void MenuItem_Save_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog dialog = new SaveFileDialog {
                Filter = "PDF(*.pdf)|*.pdf",
                RestoreDirectory = true,
            };

            if (!string.IsNullOrEmpty(OriginalFilePath)) {
                dialog.InitialDirectory = Path.GetDirectoryName(OriginalFilePath);
            }

            if (dialog.ShowDialog() == true) {
                CreatePdf(dialog.FileName);
                GC.Collect();
            }
        }

        private void MenuItem_Open_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog {
                Filter = "PDF(*.pdf)|*.pdf",
            };

            if (dialog.ShowDialog() == true) {
                OpenPdf(dialog.FileName);
            }
        }

        private void MenuItem_Settings_Click(object sender, RoutedEventArgs e) {
            SettingsWindow settingsWindow =
                new SettingsWindow(Threshold, BoldDistance);
            settingsWindow.ShowDialog();
            Threshold = settingsWindow.Threshold;
            BoldDistance = settingsWindow.BoldDistance;
        }

        private void MenuItem_Item_Add_Click(object sender, RoutedEventArgs e) {
            foreach (PageData item in imageView.SelectedItems) {
                item.IsConvertTarget = true;
            }
            CollectionViewSource.GetDefaultView(PageData).Refresh();
        }

        private void MenuItem_Item_Remove_Click(object sender, RoutedEventArgs e) {
            foreach (PageData item in imageView.SelectedItems) {
                item.IsConvertTarget = false;
            }
            CollectionViewSource.GetDefaultView(PageData).Refresh();
        }

        private void BoldBinaryData(
            byte[] bold, int width, int height,
            int x, int y, int stride, int remainDistance) {
            bold[y * stride + x / 8] &= (byte)~(0x80 >> (x % 8));

            if (remainDistance == 0) {
                return;
            }

            // 効率悪いのでアルゴリズム改善を将来的には行う
            if (x > 0) {
                BoldBinaryData(
                    bold, width, height,
                    x - 1, y, stride, remainDistance - 1);
            }
            if (x < width - 1) {
                BoldBinaryData(
                    bold, width, height,
                    x + 1, y, stride, remainDistance - 1);
            }
            if (y > 0) {
                BoldBinaryData(
                    bold, width, height,
                    x, y - 1, stride, remainDistance - 1);
            }
            if (y < height - 1) {
                BoldBinaryData(
                    bold, width, height,
                    x, y + 1, stride, remainDistance - 1);
            }
        }

        private void CreatePdf(string filePath) {
            // オリジナルドキュメントについては、当初開いた際に
            // メモリ上に展開したものを再利用することを考えたが、
            // 画像のFilter属性がなぜか取れなかったため再度開きなおす
            using PdfDocument saveDocument = new PdfDocument();
            using PdfDocument document = PdfReader.Open(
                OriginalFilePath, PdfDocumentOpenMode.Import);

            foreach (PageData pageData in PageData) {
                if (!pageData.IsConvertTarget) {
                    // DCTDecodeの劣化防止のため、
                    // 必要なページだけ差し替えるようにする
                    saveDocument.AddPage(document.Pages[pageData.PageCount]);
                } else {
                    using MemoryStream ms = new MemoryStream();
                    using (PdfDocument pageDocument = new PdfDocument()) {
                        // メモリリーク防止のため一度PdfDocumentを作成し、
                        // そこから読み込んだページを追加する

                        PdfPage originalPage = document.Pages[pageData.PageCount];

                        // 2値化Bitmap作成
                        using Bitmap pageBitmap =
                            ExtractBitmapFromPage(originalPage);
                        if (pageBitmap == null) {
                            continue;
                        }
                        int width = pageBitmap.Width;
                        int height = pageBitmap.Height;
                        using Bitmap thresholdingImage = new Bitmap(
                                width, height, PixelFormat.Format1bppIndexed);
                        BitmapData data = thresholdingImage.LockBits(
                            new Rectangle(0, 0, width, height),
                            ImageLockMode.ReadWrite,
                            PixelFormat.Format1bppIndexed);
                        byte[] scan = new byte[(width + 7) / 8];
                        for (int y = 0; y < height; y++) {
                            for (int x = 0; x < width; x++) {
                                if (x % 8 == 0) {
                                    scan[x / 8] = 0;
                                }
                                Color c = pageBitmap.GetPixel(x, y);
                                if (c.GetBrightness() >= Threshold / 100.0) {
                                    scan[x / 8] |= (byte)(0x80 >> (x % 8));
                                }
                            }
                            Marshal.Copy(scan, 0, (IntPtr)((long)data.Scan0 + data.Stride * y), scan.Length);
                        }
                        if (BoldDistance != 0) {
                            byte[] original = new byte[height * data.Stride];
                            byte[] bold = new byte[height * data.Stride];
                            Marshal.Copy(data.Scan0, original, 0, original.Length);
                            Marshal.Copy(data.Scan0, bold, 0, bold.Length);
                            for (int y = 0; y < height; y++) {
                                for (int x = 0; x < width; x++) {
                                    if ((original[y * data.Stride + x / 8] & (byte)(0x80 >> (x % 8))) == 0) {
                                        BoldBinaryData(bold, width, height, x, y, data.Stride, BoldDistance);
                                    }
                                }
                            }
                            Marshal.Copy(bold, 0, data.Scan0, bold.Length);
                        }
                        thresholdingImage.UnlockBits(data);
                        thresholdingImage.SetResolution(
                            pageBitmap.HorizontalResolution,
                            pageBitmap.VerticalResolution);

                        // PdfPage作成
                        // 一応スケールは読み込み画像と同じものを設定しているが、
                        // 一度2値化バイナリで出力するとdpi情報が失われるため、
                        // 新規ページは元ページと同じ幅・高さを再設定する
                        PdfPage page = pageDocument.AddPage();
                        using MemoryStream imageStream = new MemoryStream();
                        thresholdingImage.Save(imageStream, ImageFormat.Png);
                        using XGraphics graphics = XGraphics.FromPdfPage(page);
                        imageStream.Seek(0, SeekOrigin.Begin);
                        using XImage image = XImage.FromStream(() => imageStream);
                        page.Width = originalPage.Width;
                        page.Height = originalPage.Height;
                        graphics.DrawImage(image, 0, 0, page.Width, page.Height);
                        page.Rotate = originalPage.Rotate;
                        pageDocument.Save(ms, false);
                    }

                    using (PdfDocument pageDocument =
                            PdfReader.Open(ms, PdfDocumentOpenMode.Import)) {
                        saveDocument.AddPage(pageDocument.Pages[0]);
                    }
                }
            }

            saveDocument.Save(filePath);
        }
    }
}
