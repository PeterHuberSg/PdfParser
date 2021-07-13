using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PdfParserLib;


namespace PdfFilesTextBrowser {


  /// <summary>
  /// Interaction logic for PdfStreamWindow.xaml
  /// </summary>
  public partial class PdfStreamWindow: Window {


    public ObjectId ObjectId;
    readonly Tokeniser tokeniser;
    readonly StringBuilder sb;


    readonly TextViewer textViewer;
    static readonly FontFamily courierNewFontFamily = new FontFamily("Courier New");


    public PdfStreamWindow(Tokeniser tokeniser, StringBuilder sb, ObjectId objectId, TextViewer textViewer) {
      this.tokeniser = tokeniser;
      this.sb = sb;
      ObjectId = objectId;
      this.textViewer = textViewer;
      Owner = textViewer.OwnerWindow;
      Width = Owner.ActualWidth/2;
      Height = Owner.ActualHeight * 0.9;
      //var location = Owner.PointToScreen(new Point(Owner.Width*0, Owner.Height*0));
      Left  = Owner.Left + Owner.ActualWidth*.49;
      Top   = Owner.Top + Owner.ActualHeight*0.05;
      InitializeComponent();

      CharRadioButton.Click += charRadioButton_Click;
      HexRadioButton.Click +=hexRadioButton_Click;
      FindButton.Click += findButton_Click;
      Closed += pdfStreamWindow_Closed;

      displayContent();
      Show();
    }


    private void charRadioButton_Click(object sender, RoutedEventArgs e) {
      displayChar(isInitialising: false);
    }


    private void hexRadioButton_Click(object sender, RoutedEventArgs e) {
      displayHex();
    }


    internal void Update(ObjectId objectId) {
      ObjectId = objectId;
      displayContent();
    }


    ReadOnlyMemory<byte> bytesMemory;


    private void displayContent() {
      Title = $"{ObjectId} Stream";
      MainStatusBar.IsEnabled = false;
      if (tokeniser.IsDecryptionError) {
        StreamTextBox.Text = "Decryption error";
        return;
      }

      try {
        var tokenStreamNullable = tokeniser.GetStream(ObjectId);
        if (tokenStreamNullable is null) {
          StreamTextBox.Text = sb.ToString();
          return;
        }

        DictionaryToken? dictionaryToken;
        (dictionaryToken, bytesMemory) = tokenStreamNullable.Value;
        MainStatusBar.IsEnabled = true;
        if (dictionaryToken!=null) {
          if (dictionaryToken.PdfObject is PdfContent) {
            displayContentStream(dictionaryToken);
            return;
          }
          if (dictionaryToken.Type=="ObjStm") {
            displayOjectStream(dictionaryToken);
            return;
          }
        }
        displayChar(isInitialising: true);
      } catch (Exception ex) {
        MainStatusBar.IsEnabled = false;
        StreamTextBox.Text = "Can not display stream content because of exception: " + ex.Message;
      }
    }


    #region Char Hex
    //      --------

    private void displayChar(bool isInitialising) {
      var bytes = bytesMemory.Span;
      sb.Clear();
      var charCount = 0;
      var hexCount = 0;
      for (int bytesIndex = 0; bytesIndex < bytes.Length; bytesIndex++) {
        var b = bytes[bytesIndex];
        var c = PdfEncodings.PdfEncoding[b];
        if (c==0xFFFF) {
          sb.Append('\'');
          sb.Append(b.ToString());
          sb.Append('\'');
          hexCount++;
        } else {
          sb.Append(c);
          if (c==' ' || (c>='a' && c<='z') || (c>='A' && c<='Z') || (c>='0' && c<='9') || c=='\n' || c=='\r' ||
            c=='/' || c=='(' || c==')' || c=='[' || c==']'  || c=='<' || c=='>') {
            charCount++;
          } else {
            hexCount++;
          }
        }
      }

      if (isInitialising && hexCount>charCount) {
        displayHex();
      } else {
        CharRadioButton.IsChecked = true;
        StreamTextBox.Text = sb.ToString();
        sb.Clear();
      }
    }


    private void displayHex() {
      var bytes = bytesMemory.Span;
      StreamTextBox.FontFamily = courierNewFontFamily;
      HexRadioButton.IsChecked = true;
      sb.Clear();
      sb.Append("0000 ");
      for (int bytesIndex = 0; bytesIndex < bytes.Length; bytesIndex++) {
        var b = bytes[bytesIndex];
        sb.Append(b.ToString("X2"));
        sb.Append(' ');
        if (bytesIndex%512==511) {
          sb.AppendLine();
          sb.AppendLine();
          sb.Append((bytesIndex+1).ToString("X4") + ' ');
        } else if (bytesIndex%32==31) {
          sb.AppendLine();
          sb.Append((bytesIndex+1).ToString("X4") + ' ');
        } else if (bytesIndex%16==15) {
          sb.Append("  ");
        } else if (bytesIndex%8==7) {
          sb.Append(' ');
        }
      }
      StreamTextBox.Text = sb.ToString();
      sb.Clear();
    }
    #endregion


    #region Content Stream
    //      --------------

    enum contentStateEnum {
      parse,
      BI,
      ID,
      BT,
    }


    private void displayContentStream(DictionaryToken contentDictionaryToken) {
      var fonts = ((PdfContent)contentDictionaryToken.PdfObject!).Fonts;
      var bytes = bytesMemory.Span;

      //check how many cr and lf are in the first chunk of characters. If there are none, the Content is in a object stream and
      //EOLs should get added to improve readability
      var chunkLength = Math.Min(4*80+10, bytes.Length);
      var lfCount = 0;
      var crCount = 0;
      for (int i = 0; i<chunkLength; i++) {
        var b = bytes[i];
        if (b=='\n') lfCount++;
        if (b=='\r') crCount++;
      }
      lfCount = Math.Max(lfCount, crCount);
      var areEOLsNeeded = lfCount==0 || (chunkLength/lfCount>80);

      sb.Clear();
      if (bytes.Length<10) {
        for (int bytesIndex = 0; bytesIndex < bytes.Length-1; bytesIndex++) {
          append(sb, bytes[bytesIndex]);
        }
        StreamTextBox.Text = sb.ToString();
        sb.Clear();
        return;
      }

      var fontSb = new StringBuilder();
      var contentState = contentStateEnum.parse;
      var startBTIndex = int.MinValue;
      var aqppendDelayedEOLCount = 0;
      char[]? fontEncoding = null;
      byte b0; 
      var b1 = (byte)' ';//"add" space before buffer in case the very first 2 characters are like BI or BT
      var b2 = bytes[0];
      var b3 = bytes[1];
      append(sb, b2);
      append(sb, b3);
      for (int bytesIndex = 2; bytesIndex < bytes.Length; bytesIndex++) {
        b0 = b1;
        b1 = b2;
        b2 = b3;
        b3 = bytes[bytesIndex];

        if (contentState==contentStateEnum.ID) {
          //checking if that ever occurs
          if ((b1=='\n' || b1=='\r') && (b2!='E' || b3!='I')) {
            System.Diagnostics.Debugger.Break();
          }
        } 
        
        if (contentState==contentStateEnum.BT) {
          //process T, i.e. strings one byte at a time
          if (b3=='(') {
            //inside a string
            sb.Append('(');
            var encoding = fontEncoding ?? PdfEncodings.Standard;
            while (true) {
              b3 = bytes[++bytesIndex];
              if (b3==')') {
                //sb.Append(')'); not needed, b3 will be later added to sb
                break;
              }
              var c = encoding[b3];
              if (c==0xFFFF) {
                sb.Append('\'' + b3.ToString("x") + '\'');
              } else {
                sb.Append(c);
              }
            }

          } else if (b3=='T' && bytes[bytesIndex+1]=='f') {
            //font definition found
            int searchBackIndex;
            var isFontFound = false;
            for (searchBackIndex = bytesIndex-1; searchBackIndex > startBTIndex; searchBackIndex--) {
              if (bytes[searchBackIndex]=='/') {
                fontSb.Clear();
                searchBackIndex++;
                for (; searchBackIndex < bytesIndex; searchBackIndex++) {
                  var b = bytes[searchBackIndex];
                  if (Tokeniser.IsWhiteSpace(b)) {
                    var fontString = fontSb.ToString();
                    fontEncoding = fonts[fontString].Encoding8Bit;
                    isFontFound = true;
                    break;
                  }
                  fontSb.Append((char)b);
                }
                if (!isFontFound) {
                  System.Diagnostics.Debugger.Break();
                }
                break;
              }
            }
            if (!isFontFound) {
              System.Diagnostics.Debugger.Break();
            }

          } else if (b3=='E' && bytes[bytesIndex+1]=='T') { 
            startBTIndex = int.MinValue;
            fontEncoding = null;
            contentState = contentStateEnum.parse;
            if (areEOLsNeeded) {
              aqppendDelayedEOLCount = 3;
            }
          }

        } else {
          //search for BI, DI, EI and BT
          if (Tokeniser.IsDelimiterByte(b0) && Tokeniser.IsDelimiterByte(b3)) {
            switch (contentState) {
            case contentStateEnum.parse:
              if (b1=='B' && b2=='I') {
                if (areEOLsNeeded) {
                  sb.Length -= 2;
                  sb.AppendLine();
                  sb.AppendLine();
                  sb.Append("BI");
                }
                contentState = contentStateEnum.BI;

              } else if (b1=='B' && b2=='T') {
                if (areEOLsNeeded) {
                  sb.Length -= 2;
                  sb.AppendLine();
                  sb.AppendLine();
                  sb.Append("BT");
                }
                contentState = contentStateEnum.BT;
                startBTIndex = bytesIndex;
              }
              break;

            case contentStateEnum.BI:
              if (b1=='I' && b2=='D') {
                if (areEOLsNeeded) {
                  sb.Length -= 2;
                  sb.AppendLine();
                  sb.AppendLine();
                  sb.Append("ID");
                }
                contentState = contentStateEnum.ID;
              }
              break;

            case contentStateEnum.ID:
              if (b1=='E' && b2=='I') {
                if (areEOLsNeeded) {
                  aqppendDelayedEOLCount = 3;
                }
                contentState = contentStateEnum.parse;
              }
              break;
            default:
              throw new NotSupportedException();
            }
          }
        }

        append(sb, b3);
        if (aqppendDelayedEOLCount>0) {
          aqppendDelayedEOLCount--;
          if (aqppendDelayedEOLCount==0) {
            sb.AppendLine();
          }
        }
      }

      StreamTextBox.Text = sb.ToString();
      sb.Clear();
    }


    private void append(StringBuilder sb, byte b) {
      var c = PdfEncodings.PdfEncoding[b];
      if (c==0xFFFF) {
        sb.Append('\'' + b.ToString("x") + '\'');
      } else {
        sb.Append(c);
      }
    }
    #endregion


    #region Object Stream
    //      -------------

    private void displayOjectStream(DictionaryToken dictionaryToken) {
      var bytes = bytesMemory.Span;
      sb.Clear();
      var objectCount = ((NumberToken)dictionaryToken["N"]).Integer!.Value;
      var first = ((NumberToken)dictionaryToken["First"]).Integer!.Value;
      var objectId = tokeniser.GetStreamInt();
      var offset = tokeniser.GetStreamInt() + first;
      for (int objectIndex = 0; objectIndex < objectCount; objectIndex++) {
        int objectIdNext;
        int offsetNext;
        if (objectIndex<objectCount-1) {
          objectIdNext = tokeniser.GetStreamInt();
          offsetNext = tokeniser.GetStreamInt() + first;
        } else {
          objectIdNext = -1;
          offsetNext = bytes.Length;
        }
        if (sb.Length>0) {
          sb.AppendLine();
        }
        sb.Append(objectId.ToString());
        sb.Append(' ');
        sb.AppendLine(offset.ToString());
        for (int byteIndex = offset; byteIndex < offsetNext; byteIndex++) {
          append(sb, bytes[byteIndex]);
        }
        sb.AppendLine();

        objectId = objectIdNext;
        offset = offsetNext;
      }
      StreamTextBox.Text = sb.ToString();
      sb.Clear();
    }
    #endregion


    private void findButton_Click(object sender, RoutedEventArgs e) {
      openFindWindow();
    }


    FindWindow? findWindow;


    protected override void OnKeyUp(KeyEventArgs e) {
      var key = e.Key == Key.System ? e.SystemKey : e.Key;

      if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        && key == Key.F) 
      {
        e.Handled = true;
        if (findWindow is null) {
          openFindWindow();
        } else {
          findWindow.Focus();
        }
      }

      if (key==Key.Enter) {
        if (findWindow!=null) {
          findWindow.FindNext();
          e.Handled = true;
        }
      }
      base.OnKeyUp(e);
    }


    private void openFindWindow() {
      if (findWindow is null) {
        findWindow = new FindWindow(this, null, StreamTextBox, removeFindWindow);
        findWindow.Show();
      } else {
        findWindow.Focus();
      }
    }


    private void removeFindWindow() {
      findWindow = null;
    }


    private void pdfStreamWindow_Closed(object? sender, EventArgs e) {
      textViewer.ResetStreamWindow();
      Owner.Activate();
    }
  }
}
