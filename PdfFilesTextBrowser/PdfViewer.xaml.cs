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

    public string? PdfFilePath { get; private set; }


    public int? PageNo { get; private set; }


    //public int MaxPages {
    //  get => (int)GetValue(MaxPagesProperty);
    //  set => SetValue(MaxPagesProperty, value);
    //}


    //public static readonly DependencyProperty MaxPagesProperty =
    //  DependencyProperty.Register("MaxPages", typeof(int), typeof(PdfViewer), new PropertyMetadata(int.MaxValue));


    //public string PdfPath {
    //  get => (string)GetValue(PdfPathProperty);
    //  set => SetValue(PdfPathProperty, value);
    //}


    //// Using a DependencyProperty as the backing store for PdfPath.  This enables animation, styling, binding, etc...
    //public static readonly DependencyProperty PdfPathProperty =
    //  DependencyProperty.Register("PdfPath", typeof(string), typeof(PdfViewer), 
    //    new PropertyMetadata(null, propertyChangedCallback: OnPdfPathChanged));


    //private static async void OnPdfPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
    //  var pdfViewer = (PdfViewer)d;
    //  pdfViewer.pdfDoc = null;
    //  pdfViewer.PagesContainer.Items.Clear();//empty displays until new page gets displayed, which might be seconds later

    //  if (!string.IsNullOrEmpty(pdfViewer.PdfPath)) {
    //    //making sure it's an absolute path
    //    var path = System.IO.Path.GetFullPath(pdfViewer.PdfPath);

    //    System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
    //      $"PdfViewer.OnPdfPathChanged(): started");
    //    //StorageFile.GetFileFromPathAsync(path).AsTask()
    //    //  //load pdf document on background thread
    //    //  .ContinueWith(t => PdfDocument.LoadFromFileAsync(t.Result).AsTask())
    //    //  .Unwrap()
    //    //  //display on UI Thread
    //    //  .ContinueWith(t2 => PdfToImages(pdfViewer, t2.Result), TaskScheduler.FromCurrentSynchronizationContext());
    //    var pdfStorageFile = await StorageFile.GetFileFromPathAsync(path);
    //    pdfViewer.pdfDoc = await PdfDocument.LoadFromFileAsync(pdfStorageFile);
    //    System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
    //      $"PdfViewer.OnPdfPathChanged(): Completed");
    //  }
    //}
    #endregion


    #region Constructor
    //      -----------

    public PdfViewer() {
      InitializeComponent();
    }
    #endregion


    #region Methods
    //      -------

    bool isBusy;
    string? loadNextPdfFilePath;


    public void Load(string pdfFilePath) {
      if (string.IsNullOrEmpty(pdfFilePath) || this.PdfFilePath==pdfFilePath) return;
      //make sure it's an absolute path
      loadNextPdfFilePath = Path.GetFullPath(pdfFilePath);

      if (!isBusy) {
        executeLoadOrShowPage();
      }
    }


    int? nextPageNo;


    public void ShowPage(int pageNo) {
      if (pageNo<0) {

      }
      if (PageNo==pageNo) return;

      nextPageNo = pageNo;

      if (!isBusy) {
        executeLoadOrShowPage();
      }
    }


    PdfDocument? pdfDoc;


    private async void executeLoadOrShowPage() {
      if (isBusy || (loadNextPdfFilePath is null && nextPageNo is null)) {
        System.Diagnostics.Debugger.Break();
        return;
      }

      isBusy = true;
      do {
        if (loadNextPdfFilePath is not null) {
          pdfDoc = null;
          PdfFilePath = null;
          PagesContainer.Items.Clear();//empty displays until new page gets displayed, which might be seconds later

          System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
            $"PdfViewer.executeLoad({loadNextPdfFilePath}): started");
          try {
            var pdfStorageFile = await StorageFile.GetFileFromPathAsync(loadNextPdfFilePath);
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
              $"PdfViewer.executeLoad({loadNextPdfFilePath}): gotFile executed");
            pdfDoc = await PdfDocument.LoadFromFileAsync(pdfStorageFile);
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
              $"PdfViewer.executeLoad({loadNextPdfFilePath}): pdfDoc created");
            PdfFilePath = loadNextPdfFilePath;
          } catch (Exception ex) {
            System.Diagnostics.Debugger.Break();
            isBusy = false;
            nextPageNo = null;
            return;
          } finally {
            loadNextPdfFilePath = null;
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
              $"PdfViewer.executeLoad({loadNextPdfFilePath}): Completed");
          }
          if (nextPageNo is null) {
            nextPageNo = 0;
          }
        }

        if (nextPageNo is not null) {
          System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
            $"PdfViewer.ShowPage(pageNo: {nextPageNo}): started");
          PagesContainer.Items.Clear();

          if (pdfDoc == null) return;

          try {
            using var page = pdfDoc.GetPage((uint)nextPageNo);
            PageNo = nextPageNo;
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
              $"PdfViewer.ShowPage(pageNo: {PageNo} bitmap = await PageToBitmapAsync(): started");
            var bitmap = await PageToBitmapAsync(page);
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
              $"PdfViewer.ShowPage(pageNo: {PageNo} bitmap = await PageToBitmapAsync(): completed");
            var image = new Image {
              Source = bitmap,
              HorizontalAlignment = HorizontalAlignment.Center,
              Margin = new Thickness(0, 4, 0, 4),
              MaxWidth = 800
            };
            PagesContainer.Items.Add(image);
          } catch (Exception ex) {
            System.Diagnostics.Debugger.Break();
            isBusy = false;
            return;
          } finally {
            nextPageNo = null;
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
              $"PdfViewer.ShowPage(pageNo: {PageNo}): completed");
          }
        }
      } while (loadNextPdfFilePath is not null || nextPageNo is not null);

      isBusy = false;
    }

    //private static async Task PdfToImages(PdfViewer pdfViewer, PdfDocument pdfDoc) {
    //  var items = pdfViewer.PagesContainer.Items;
    //  items.Clear();

    //  if (pdfDoc == null) return;

    //  if (pdfDoc.PageCount>pdfViewer.MaxPages) {
    //    //display only 1 page
    //    using var page = pdfDoc.GetPage(0);
    //    var bitmap = await PageToBitmapAsync(page);
    //    var image = new Image {
    //      Source = bitmap,
    //      HorizontalAlignment = HorizontalAlignment.Center,
    //      Margin = new Thickness(0, 4, 0, 4),
    //      MaxWidth = 800
    //    };
    //    items.Add(image);

    //  } else {
    //    for (uint i = 0; i < pdfDoc.PageCount; i++) {
    //      using var page = pdfDoc.GetPage(i);
    //      var bitmap = await PageToBitmapAsync(page);
    //      var image = new Image {
    //        Source = bitmap,
    //        HorizontalAlignment = HorizontalAlignment.Center,
    //        Margin = new Thickness(0, 4, 0, 4),
    //        MaxWidth = 800
    //      };
    //      items.Add(image);
    //    }
    //  }
    //}


    //private static async Task PdfToImages(PdfViewer pdfViewer, PdfDocument pdfDoc) {
    //  System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
    //    $"PdfViewer.PdfToImages(): started");
    //  pdfViewer.pdfDoc = pdfDoc;
    //  var items = pdfViewer.PagesContainer.Items;
    //  items.Clear();

    //  if (pdfDoc == null) return;

    //  pdfViewer.PageNo = 0;
    //  using var page = pdfDoc.GetPage(pdfViewer.PageNo);
    //  System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
    //    $"PdfViewer.PdfToImages() bitmap = await PageToBitmapAsync(): started");
    //  var bitmap = await PageToBitmapAsync(page);
    //  System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
    //    $"PdfViewer.PdfToImages() bitmap = await PageToBitmapAsync(): returned");
    //  var image = new Image {
    //    Source = bitmap,
    //    HorizontalAlignment = HorizontalAlignment.Center,
    //    Margin = new Thickness(0, 4, 0, 4),
    //    MaxWidth = 800
    //  };
    //  items.Add(image);
    //  System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
    //    $"PdfViewer.PdfToImages(): started");
    //}


    private static async Task<BitmapImage> PageToBitmapAsync(PdfPage page) {
      System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
        $"PdfViewer.PageToBitmapAsync(page: {page.Index}) started");
      var image = new BitmapImage();

      using (var stream = new InMemoryRandomAccessStream()) {
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
          $"PdfViewer.PageToBitmapAsync(page: {page.Index}): page.RenderToStreamAsync() started");
        await page.RenderToStreamAsync(stream);
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
          $"PdfViewer.PageToBitmapAsync(page: {page.Index}): page.RenderToStreamAsync() completed");

        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream.AsStream();
        image.EndInit();
      }

      System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
        $"PdfViewer.PageToBitmapAsync(page: {page.Index}) completed");
      return image;
    }
    #endregion
  }
}
