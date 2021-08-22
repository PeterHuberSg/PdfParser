using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using PdfParserLib;
using Ookii.Dialogs;
using System.Threading;

namespace PdfFilesTextBrowser {


  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow: Window {

    #region Properties
    //      ----------

    /// <summary>
    /// Currently running MainWindow
    /// </summary>
    public static MainWindow? Current;
    #endregion


    #region Constructor
    //      -----------

    readonly TextViewer bytesTextViwer;
    //readonly Stack<PdfSourceRichTextBox.PdfRefRun> pdfRefRunTrace;
    const int maxPages = 20;


    public MainWindow() {
      //pdfRefRunTrace = new Stack<PdfSourceRichTextBox.PdfRefRun>();
      InitializeComponent();

      Current = this;
      bytesTextViwer = new(this);

      //DirectoryTextBox.Text = @"D:\";
      //DirectoryTextBox.Text = @"C:\Users\peter\OneDrive\OneDriveData\";
      //DirectoryTextBox.Text = @"C:\Users\peter\OneDrive\OneDriveData\Invest\CS";
      //DirectoryTextBox.Text = @"C:\Users\peter\OneDrive\OneDriveData\Invest\DBS";

      //FileTextBox.Text = @"C:\Users\peter\Source\Repos\PdfParser\XRefUpdater\PdfTestSample.pdf";
      //FileTextBox.Text = @"C:\Users\peter\Source\Repos\PdfParser\XRefUpdater\H3 Simple Text String Example Updated.pdf";
      //FileTextBox.Text = @"C:\Users\peter\Source\Repos\PdfParser\PdfParserTest\H3 Simple Text String Example.pdf";
      //FileTextBox.Text = @"C:\Users\peter\Source\Repos\PdfParser\PdfParserTest\file-sample_150kB.pdf";
      //FileTextBox.Text = @"C:\Users\peter\OneDrive\OneDriveData\Invest\CS\1224799-50_closing_statement_2016-01-01.PDF";
      //FileTextBox.Text = @"D:\PDF32000_2008.pdf";
      //FileTextBox.Text = @"D:\1.15443971L-2019-10-16.pdf";
      //FileTextBox.Text = @"D:\Abmelung Horgen.pdf";
      //FileTextBox.Text = @"C:\Users\peter\OneDrive\OneDriveData\AHV\Huber Juerg Peter - rf_2013_7566952682341_.pdf";
      //FileTextBox.Text = @"C:\Users\peter\OneDrive\OneDriveData\AHV\AnmeldungAHV2019.pdf";
      //FileTextBox.Text = @"C:\Users\peter\OneDrive\OneDriveData\BDSM\climbing_hitches.pdf";
      //FileTextBox.Text = @"C:\Users\peter\OneDrive\OneDriveData\AHV\AnmeldungAHV2019.pdf";
      //FileTextBox.Text = @"C:\Users\Peter\OneDrive\OneDriveData\Invest\DBS\DBS 202004.pdf";
      //FileTextBox.Text = @"C:\Users\Peter\OneDrive\OneDriveData\Invest\DBS\DBS 202104.pdf";
      //FileTextBox.Text = @"D:\PDF32000_2008.pdf";
      //FileTextBox.Text = @"D:\Abmelung Horgen.pdf";
      FileTextBox.Text = @"D:\comparison-of-private-hospital-ips.pdf";
    

      //xref stream

      if (DirectoryTextBox.Text.Length>0 || FileTextBox.Text.Length>0) {
        navigate(isNext: true);
      }

      KeyUp += mainWindow_KeyUp;
      DirectoryButton.Click += directoryButton_Click;
      FileButton.Click += fileButton_Click;
      PreviousButton.Click += previousButton_Click;
      NextButton.Click += nextButton_Click;
      FindButton.Click += findButton_Click;
      BackButton.Click += backButton_Click;
      PagesTabControl.SelectionChanged += PagesTabControl_SelectionChanged;
    }
    #endregion


    #region Child Windows
    //      -------------

    readonly HashSet<Window> registeredWindows = new HashSet<Window>();


    public void Register(Window window) {
      registeredWindows.Add(window);
    }


    public void Unregister(Window window) {
      registeredWindows.Remove(window);
    }


    private void closeRegisteredWindows() {
      foreach (var window in registeredWindows.ToArray()) {//have to loop over copy, because windows will remove themselves
        window.Close();
      }
    }
    #endregion


    #region File Navigation
    //      ---------------

    private void directoryButton_Click(object sender, RoutedEventArgs e) {
      var openFolderDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
      if (DirectoryTextBox.Text.Length>0) {
        openFolderDialog.SelectedPath = DirectoryTextBox.Text;
      } else if(FileTextBox.Text.Length>0) {
        var fileInfo = new FileInfo(FileTextBox.Text);
        openFolderDialog.SelectedPath = fileInfo.DirectoryName;
      }
      if (openFolderDialog.ShowDialog()==true) {
        DirectoryTextBox.Text = openFolderDialog.SelectedPath;
        FileTextBox.Text = "";
      }
    }


    private void fileButton_Click(object sender, RoutedEventArgs e) {
      var openFileDialog = new OpenFileDialog();
      openFileDialog.Filter = "pdf file|*.pdf";
      if (FileTextBox.Text.Length>0) {
        openFileDialog.FileName = FileTextBox.Text;
      } else if (DirectoryTextBox.Text.Length>0) {
        openFileDialog.FileName = DirectoryTextBox.Text;
      }
      if (openFileDialog.ShowDialog()==true) {
        FileTextBox.Text = openFileDialog.FileName;
        var fileInfo = new FileInfo(FileTextBox.Text);
        DirectoryTextBox.Text = fileInfo.DirectoryName;
        navigate(isNext: true);
      }
    }


    private void nextButton_Click(object sender, RoutedEventArgs e) {
      navigate(isNext: true);
    }


    private void previousButton_Click(object sender, RoutedEventArgs e) {
      navigate(isNext: false);
    }
    #endregion


    #region Keystrokes and FindWindow
    //      -------------------------

    FindWindow? findWindow;

    
    private void mainWindow_KeyUp(object sender, KeyEventArgs e) {
      var key = e.Key == Key.System ? e.SystemKey : e.Key;

      if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        && key == Key.F) 
      {
        e.Handled = true;
        if (findWindow is null) {
          OpenFindWindow();
        } else {
          findWindow.Focus();
        }
      }

      if (key==Key.Enter) {
        if (findWindow!=null) {
          e.Handled = true;
          findWindow.FindNext();
        }
      } else if (key==Key.Escape) {
        if (findWindow!=null) {
          e.Handled = true;
          findWindow.Close();
          findWindow = null;
        }
      }
    }


    public void OpenFindWindow() {
      if (findWindow is null) {
        findWindow = new FindWindow(this, PagesTabControl, null, removeFindWindow);
        findWindow.Show();
      } else {
        findWindow.Focus();
      }
    }


    private void removeFindWindow() {
      findWindow = null;
    }


    private void findButton_Click(object sender, RoutedEventArgs e) {
      OpenFindWindow();
    }
    #endregion


    #region BackButton
    //      ----------

    //internal void AddToTrace(PdfSourceRichTextBox.PdfRefRun pdfRefRun) {
    //  if (pdfRefRunTrace.Count==0) {
    //    BackStatusBarItem.Visibility = Visibility.Visible;
    //  }
    //  pdfRefRunTrace.Push(pdfRefRun);
    //}


    //Todo: Add Back Button functionality
    private void backButton_Click(object sender, RoutedEventArgs e) {
      //var pdfObjectRun = pdfRefRunTrace.Pop()!;
      //pdfObjectRun.SetFocus();
      //if (pdfRefRunTrace.Count==0) {
      //  pdfRefRunTrace.Clear();
      //  BackStatusBarItem.Visibility = Visibility.Collapsed;
      //}
    }


    string fileString = "";
    string directoryString = "";
    readonly byte[] streamBuffer = new byte[10_000_000];
    readonly StringBuilder stringBuilder = new StringBuilder();
    readonly Queue<FileInfo> files = new Queue<FileInfo>();
    readonly Stack<DirectoryInfo> dirs = new Stack<DirectoryInfo>();
    readonly List<FileInfo> allFiles = new List<FileInfo>();
    int currentFileIndex;
    bool isShowStartFile;
    #endregion


    #region Page Controller
    //      ---------------

    PdfParser? pdfParser;
    TabItem? pageControllerTabItem;
    int pageNo;
    TextBox? pageNoTextBox;
    Button? endButton;


    private TabItem createPageControllerTabItem() {
      var tabItemStackPanel = new StackPanel { Orientation=Orientation.Horizontal };

      var labelTextBlock = new TextBlock { Text="Page:" };
      tabItemStackPanel.Children.Add(labelTextBlock);

      var zeroButton = new Button { Content = "0", MinWidth=FontSize*1.5 };
      zeroButton.Click += ZeroButton_Click;
      tabItemStackPanel.Children.Add(zeroButton);

      var minus2Button = new Button { Content = "--", MinWidth=FontSize*1.5 };
      minus2Button.Click += Minus2Button_Click;
      tabItemStackPanel.Children.Add(minus2Button);

      var minusButton = new Button { Content = "-", MinWidth=FontSize*1.5 };
      minusButton.Click += MinusButton_Click;
      tabItemStackPanel.Children.Add(minusButton);

      pageNoTextBox = new TextBox { MinWidth=30 };
      pageNoTextBox.TextChanged += PageNoTextBox_TextChanged;
      tabItemStackPanel.Children.Add(pageNoTextBox);

      var plusButton = new Button { Content = "+", MinWidth=FontSize*1.5 };
      plusButton.Click += PlusButton_Click;
      tabItemStackPanel.Children.Add(plusButton);

      var plus2Button = new Button { Content = "++", MinWidth=FontSize*1.5 };
      plus2Button.Click += Plus2Button_Click;
      tabItemStackPanel.Children.Add(plus2Button);

      endButton = new Button { MinWidth=FontSize*1.5 };
      endButton.Click += EndButton_Click;
      tabItemStackPanel.Children.Add(endButton);

      var pageTabItem = new TabItem {
        Header = tabItemStackPanel
      };
      return pageTabItem;
    }


    private void PageNoTextBox_TextChanged(object sender, TextChangedEventArgs e) {
      if (pdfParser is null) return;

      if (uint.TryParse(pageNoTextBox?.Text, out var newPageNo)) {
        if (newPageNo>=pdfParser!.Pages.Count) return;

        pageNo = (int)newPageNo;
        fillTabItemContent(pageControllerTabItem!, pdfParser!.Pages[(int)pageNo]);
        MainPdfViewer.ShowPage(pageNo);
      }

    }


    private void ZeroButton_Click(object sender, RoutedEventArgs e) {
      pageNo = 0;
      pageNoTextBox!.Text = pageNo.ToString();
      pageControllerTabItem!.Focus();
    }


    private void Minus2Button_Click(object sender, RoutedEventArgs e) {
      if (pageNo<=0) return;

      pageNo = pageNo<=9 ? 0 : pageNo-10;
      pageNoTextBox!.Text = pageNo.ToString();
      pageControllerTabItem!.Focus();
    }


    private void MinusButton_Click(object sender, RoutedEventArgs e) {
      if (pageNo<=0) return;

      pageNo--;
      pageNoTextBox!.Text = pageNo.ToString();
      pageControllerTabItem!.Focus();
    }


    private void PlusButton_Click(object sender, RoutedEventArgs e) {
      var newPageNo = pageNo + 1;
      if (newPageNo>=pdfParser!.Pages.Count) return;

      pageNo = newPageNo;
      pageNoTextBox!.Text = pageNo.ToString();
      pageControllerTabItem!.Focus();
    }


    private void Plus2Button_Click(object sender, RoutedEventArgs e) {
      var newPageNo = pageNo + 10;

      pageNo = newPageNo>=pdfParser!.Pages.Count ? pdfParser!.Pages.Count - 1 :  newPageNo;
      pageNoTextBox!.Text = pageNo.ToString();
      pageControllerTabItem!.Focus();
    }


    private void EndButton_Click(object sender, RoutedEventArgs e) {
      pageNo =pdfParser!.Pages.Count - 1;
      pageNoTextBox!.Text = pageNo.ToString();
      pageControllerTabItem!.Focus();
    }


    bool isPageControllerShown;
    #endregion


    #region Navigation and Pdf Page Display
    //      -------------------------------


    TabItem? bytesTabItem;


    private async void navigate(bool isNext) {
      System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
    "MainWindow navigate() started");
      //pdfRefRunTrace.Clear();
      BackStatusBarItem.Visibility = Visibility.Collapsed;

      //check if user has changed file or directory
      if (FileTextBox.Text!="" && fileString!=FileTextBox.Text) {
        var fileInfo = new FileInfo(FileTextBox.Text);
        if (!fileInfo.Exists) {
          MessageBox.Show($"Could not find file '{FileTextBox.Text}'.", "Pdf file not found");
          return;
        }
        fileString = FileTextBox.Text;
        directoryString = fileInfo.DirectoryName!;
        DirectoryTextBox.Text = directoryString;
        files.Clear();
        dirs.Clear();
        allFiles.Clear();
        currentFileIndex = 0;
        dirs.Push(new DirectoryInfo(directoryString));
        isShowStartFile = true;

      } else if (DirectoryTextBox.Text!="" && directoryString!=DirectoryTextBox.Text) {
        var directoryInfo = new DirectoryInfo(DirectoryTextBox.Text);
        if (!directoryInfo.Exists) {
          MessageBox.Show($"Could not find directory '{DirectoryTextBox.Text}'.", "Directory not found");
          return;
        }
        directoryString = DirectoryTextBox.Text;
        files.Clear();
        dirs.Clear();
        allFiles.Clear();
        currentFileIndex = 0;
        dirs.Push(directoryInfo);
      }

      var haveAllFilesBeenFound = false;
      if (currentFileIndex<allFiles.Count-1 || (isNext==false) || (files.Count==0 && dirs.Count==0)) {
        //show already read files
        if (isNext) {
          currentFileIndex++;
          if (currentFileIndex>=allFiles.Count) {
            currentFileIndex = 0;
          }
        } else {
          currentFileIndex--;
          if (currentFileIndex<0) {
            currentFileIndex = allFiles.Count-1;
          }
        }

      } else {
        while (files.Count==0 && ! haveAllFilesBeenFound) {
          if (dirs.Count==0) {
            if (allFiles.Count==0) {
              MessageBox.Show($"There are no pdf files in '{directoryString}' and its subdirectories.", "No pdf file found");
              return;
            } else {
              currentFileIndex = 0;
              haveAllFilesBeenFound = true;
            }
          } else {
            //read next directory
            var dir = dirs.Pop();
            foreach (var subDir in dir.GetDirectories().OrderByDescending(d => d.Name)) {
              dirs.Push(subDir);
            }

            foreach (var subfile in dir.GetFiles("*.pdf")) {
              if (isShowStartFile) {
                if (subfile.FullName==fileString) {
                  isShowStartFile = false;
                  files.Enqueue(subfile);
                } else {
                  allFiles.Add(subfile);
                }
              } else {
                files.Enqueue(subfile);
              }
            }
          }
        }

        if (!haveAllFilesBeenFound) {
          currentFileIndex = allFiles.Count;
          allFiles.Add(files.Dequeue());
        }
      }

      //remove old pages
      var file = allFiles[currentFileIndex];
      FileTextBox.Text = file.FullName;
      fileString = FileTextBox.Text;
      PagesTabControl.Items.Clear();
      closeRegisteredWindows();

      //display pdf
      try {
        MainPdfViewer.Visibility = Visibility.Visible;
        PdfTextBox.Visibility = Visibility.Collapsed;
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
          "MainWindow MainPdfViewer.Load() started");
        MainPdfViewer.Load(file.FullName);
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
          "MainWindow  MainPdfViewer.Load() returned");

      } catch (Exception ex) {
        MainPdfViewer.Visibility = Visibility.Collapsed;
        PdfTextBox.Visibility = Visibility.Visible;
        PdfTextBox.Text = ex.ToDetailString();
      }

      //parse pdf
      try {
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
          "MainWindow.navigate() await pdfParser = new PdfParser()");
        pdfParser = await Task.Run<PdfParser?>(() => {
          return new PdfParser(file.FullName, "", "|", streamBuffer, stringBuilder);
        });
      } catch (Exception ex) {
        System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
          $"MainWindow.navigate() await pdfParser = new PdfParser(); Exception: {ex.Message}");
        var exceptionTabItem = new TabItem {
          Header = "E_xception"
        };
        var bytes = "";
        if (ex is PdfException pdfException) {
          bytes = Environment.NewLine + Environment.NewLine + pdfException.Tokeniser.ShowBufferContent();
        }
        var exceptionTextBox = new TextBox {
          Text = ex.ToDetailString() + bytes,
          VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
          HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
          IsReadOnly = true
        };
        exceptionTabItem.Content = exceptionTextBox;
        PagesTabControl.Items.Add(exceptionTabItem);
        PagesTabControl.SelectedIndex = 0;
        return;
      }
      System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
        "MainWindow.navigate() await pdfParser = new PdfParser() completed");

      var pageIndex = 0;
      if (pdfParser!.Pages.Count>maxPages) {
        isPageControllerShown = true;
        if (pageControllerTabItem is null) {
          pageControllerTabItem = createPageControllerTabItem();
        }
        pageNo = 0;
        pageNoTextBox!.Text = pageNo.ToString();
        endButton!.Content = (pdfParser.Pages.Count-1).ToString();
        PagesTabControl.Items.Add(pageControllerTabItem);

      } else {
        isPageControllerShown = false;
        foreach (var page in pdfParser.Pages) {
          var underline = "";
          if (pageIndex<10) {
            underline = "_";
          }
          var pageTabItem = new TabItem {
            Header = underline + pageIndex++
          };
          fillTabItemContent(pageTabItem, page);
          PagesTabControl.Items.Add(pageTabItem);
        }
      }

      var infoTabItem = new TabItem {
        Header = "_Info"
      };
      var tokeniser = pdfParser.Tokeniser;
      var infotext = "PDF Version: " + tokeniser.PdfVersion;
      if (tokeniser.DocumentInfo!=null) {
        infotext += Environment.NewLine + Environment.NewLine + "Document Info: " + tokeniser.DocumentInfo;
      }
      if (tokeniser.DocumentID!=null) {
        infotext += Environment.NewLine + Environment.NewLine + "Document ID: " + tokeniser.DocumentID;
      }
      infotext += Environment.NewLine + Environment.NewLine + "Pages: " + tokeniser.Pages.Count;
      infotext += Environment.NewLine + Environment.NewLine + "Fonts: ";
      foreach (var objectId_Token in tokeniser.Tokens) {
        if (objectId_Token.Value is DictionaryToken objectDictionaryToken) {
          if (objectDictionaryToken.Type=="Font") {
            var pdfFont = (PdfFont)objectDictionaryToken.PdfObject!;
            infotext += Environment.NewLine +  Environment.NewLine + "Font (" + pdfFont.ObjectId?.ToShortString() + ')' + objectDictionaryToken.ToString();
            if (pdfFont.ToUnicodeHeader!=null) {
              infotext += Environment.NewLine + "ToUnicodeHeader: " + pdfFont.ToUnicodeHeader;
            }
            if (pdfFont.CMap!=null) {
              foreach (var code_char in pdfFont.CMap) {
                infotext += Environment.NewLine + $"{code_char.Key}: '{code_char.Value}'";
              }
            }
            if (pdfFont.Exception!=null) {
              infotext += Environment.NewLine + new string('+', 80);
              infotext += Environment.NewLine + pdfFont.Exception;
              infotext += Environment.NewLine + new string('+', 80);
              infoTabItem.Background = Brushes.Khaki;
            }

            infotext += Environment.NewLine;
          }
        }
      }
      if (tokeniser.Metadata!=null) {
        infotext += Environment.NewLine + Environment.NewLine + "Meta data: " + tokeniser.Metadata;
      }
      var textBoxInfo = new TextBox {
        Text = infotext,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        IsReadOnly = true
      };
      infoTabItem.Content = textBoxInfo;
      PagesTabControl.Items.Add(infoTabItem);

      bytesTabItem = new TabItem {
        Header = "_Bytes"
      };
      //var bytesContextMenu = new ContextMenu();
      //bytesContextMenu.Items.Add(new MenuItem { Command = System.Windows.Input.ApplicationCommands.SelectAll});
      //bytesContextMenu.Items.Add(new MenuItem { Command = System.Windows.Input.ApplicationCommands.Copy});
      //bytesContextMenu.Items.Add(new MenuItem { Command = System.Windows.Input.ApplicationCommands.Cut });
      //bytesContextMenu.Items.Add(new MenuItem { Command = System.Windows.Input.ApplicationCommands.Paste });
      //bytesContextMenu.Items.Add(new MenuItem { Command = System.Windows.Input.ApplicationCommands.Undo });
      //bytesContextMenu.Items.Add(new MenuItem { Command = System.Windows.Input.ApplicationCommands.Redo });
      ///*
      //(int)Shortcut.CtrlS, Show Stream
      //(int)Shortcut.CtrlA, Select all
      //(int)Shortcut.CtrlC, Copy 
      //(int)Shortcut.CtrlX, Cut
      //(int)Shortcut.CtrlV, Paste
      //Shortcut.CtrlZ, Undo
      //(int)Shortcut.CtrlY, Redo
      //*/

      //bytesTextBox = new TextBox {
      //  Text = pdfParser.Tokeniser.ShowBufferContent(),
      //  VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
      //  HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
      //  ContextMenu = bytesContextMenu,
      //  IsReadOnly = true
      //  };
      //////////////////////////////////////////////////////////////////////////


      //var pdfSourceRichTextBox = new PdfSourceRichTextBox(pdfParser.Tokeniser, stringBuilder, this);
      //bytesTabItem.Content = pdfSourceRichTextBox;
      //bytesTabItem.Content = pdfSourceRichTextBox;
      //PagesTabControl.Items.Add(bytesTabItem);

      //PagesTabControl.SelectedIndex = 0;
      //////////////////////////////////////////////////////////////////////////

      System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
        $"MainWindow.navigate(): await bytesTextViwer.LoadAsync() started");
      await bytesTextViwer.LoadAsync(tokeniser);
      System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {Thread.CurrentThread.ManagedThreadId} " +
        $"MainWindow.navigate(): await bytesTextViwer.LoadAsync() completed");
      bytesTabItem.Content = bytesTextViwer;
      PagesTabControl.Items.Add(bytesTabItem);

      PagesTabControl.SelectedIndex = 0;
      System.Diagnostics.Debug.WriteLine($"{DateTime.Now:mm.ss.ffff} {System.Threading.Thread.CurrentThread.ManagedThreadId} " +
    "MainWindow navigate() completed");
    }


    Brush? originalPageTabItemBackground;


    private void fillTabItemContent(TabItem pageTabItem, PdfPage page) {
      var hasException = false;
      stringBuilder.Clear();
      var isFirstContent = true;
      var hasContent = false;
      foreach (var content in page.Contents) {
        if (isFirstContent) {
          isFirstContent = false;
        } else {
          stringBuilder.AppendLine(new string('-', 80));
        }
        if (content.Text?.Length>0) {
          hasContent = true;
          stringBuilder.AppendLine(content.Text);
        }
        if (content.Exception!=null) {
          hasException = true;
          hasContent = true;
          stringBuilder.AppendLine(new string('+', 80));
          stringBuilder.AppendLine(content.Exception);
          stringBuilder.AppendLine(new string('+', 80));
        }
        if (content.Error!=null) {
          hasException = true;
          hasContent = true;
          stringBuilder.AppendLine(new string('+', 80));
          stringBuilder.AppendLine(content.Error);
          stringBuilder.AppendLine(new string('+', 80));
        }
      }

      if (page.Exception!=null) {
        hasContent = true;
        hasException = true;
        stringBuilder.AppendLine(new string('+', 80));
        stringBuilder.AppendLine(page.Exception);
        stringBuilder.AppendLine(new string('+', 80));
      }
      var textBox = new TextBox {
        Text = hasContent ? stringBuilder.ToString() : $"This pdf page has no text conten. Is it just a scan ?",
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        IsReadOnly = true
      };

      if (originalPageTabItemBackground is null) {
        originalPageTabItemBackground = pageTabItem.Background;
      }
      if (hasException) {
        pageTabItem.Background = Brushes.Khaki;
      } else {
        pageTabItem.Background = originalPageTabItemBackground;
      }
      pageTabItem.Content = textBox;
    }


    internal void SetBytesTab(bool isError) {
      if (isError) {
        bytesTabItem!.Background = Brushes.Khaki;
      } else {
        bytesTabItem!.Background = originalPageTabItemBackground;
      }
      ;
    }


    private void PagesTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
      if (isPageControllerShown || PagesTabControl.SelectedIndex<0 || PagesTabControl.SelectedIndex>=(pdfParser?.Pages.Count??0)) return;

      MainPdfViewer.ShowPage(PagesTabControl.SelectedIndex);
    }
    #endregion
  }
}
