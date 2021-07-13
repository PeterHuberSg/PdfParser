using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;


namespace PdfFilesTextBrowser {


  /// <summary>
  /// UserControl displaying the content of a pdf file
  /// </summary>
  public partial class PdfViewer: UserControl {

    #region Properties
    //      ----------

    public int MaxPages {
      get => (int)GetValue(MaxPagesProperty);
      set => SetValue(MaxPagesProperty, value);
    }


    public static readonly DependencyProperty MaxPagesProperty =
      DependencyProperty.Register("MaxPages", typeof(int), typeof(PdfViewer), new PropertyMetadata(int.MaxValue));


    public string PdfPath {
      get => (string)GetValue(PdfPathProperty);
      set => SetValue(PdfPathProperty, value);
    }


    // Using a DependencyProperty as the backing store for PdfPath.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty PdfPathProperty =
      DependencyProperty.Register("PdfPath", typeof(string), typeof(PdfViewer), 
        new PropertyMetadata(null, propertyChangedCallback: OnPdfPathChanged));


    private static void OnPdfPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
      var pdfViewer = (PdfViewer)d;

      if (!string.IsNullOrEmpty(pdfViewer.PdfPath)) {
        //making sure it's an absolute path
        var path = System.IO.Path.GetFullPath(pdfViewer.PdfPath);

        StorageFile.GetFileFromPathAsync(path).AsTask()
          //load pdf document on background thread
          .ContinueWith(t => PdfDocument.LoadFromFileAsync(t.Result).AsTask()).Unwrap()
          //display on UI Thread
          .ContinueWith(t2 => PdfToImages(pdfViewer, t2.Result), TaskScheduler.FromCurrentSynchronizationContext());
      }
    }
    #endregion

    #region Constructor
    //      -----------

    public PdfViewer() {
      InitializeComponent();
    }
    #endregion


    #region Methods
    //      -------

    private static async Task PdfToImages(PdfViewer pdfViewer, PdfDocument pdfDoc) {
      var items = pdfViewer.PagesContainer.Items;
      items.Clear();

      if (pdfDoc == null) return;

      if (pdfDoc.PageCount>pdfViewer.MaxPages) {
        //display only 1 page
        using var page = pdfDoc.GetPage(0);
        var bitmap = await PageToBitmapAsync(page);
        var image = new Image {
          Source = bitmap,
          HorizontalAlignment = HorizontalAlignment.Center,
          Margin = new Thickness(0, 4, 0, 4),
          MaxWidth = 800
        };
        items.Add(image);

      } else {
        for (uint i = 0; i < pdfDoc.PageCount; i++) {
          using var page = pdfDoc.GetPage(i);
          var bitmap = await PageToBitmapAsync(page);
          var image = new Image {
            Source = bitmap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 4),
            MaxWidth = 800
          };
          items.Add(image);
        }
      }
    }


    private static async Task<BitmapImage> PageToBitmapAsync(PdfPage page) {
      var image = new BitmapImage();

      using (var stream = new InMemoryRandomAccessStream()) {
        await page.RenderToStreamAsync(stream);

        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream.AsStream();
        image.EndInit();
      }

      return image;
    }
    #endregion
  }
}
