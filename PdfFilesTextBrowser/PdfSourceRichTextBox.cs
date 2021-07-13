//using System;
//using System.Collections.Generic;
//using System.Security.Cryptography;
//using System.Text;
//using System.Windows;
//using System.Windows.Automation;
//using System.Windows.Controls;
//using System.Windows.Controls.Primitives;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using PdfParserLib;

//namespace PdfFilesTextBrowser {


//  public class PdfSourceRichTextBox: RichTextBox {

//    readonly Tokeniser tokeniser;
//    readonly StringBuilder sb;

//    public MainWindow MainWindow => mainWindow;
//    readonly MainWindow mainWindow;
//    readonly Dictionary<ObjectId, PdfObjectRun> pdfObjects;
//    readonly Dictionary<ObjectId, List<PdfRefRun>> pdfRefs;


//    enum stateEnum {
//      parse,
//      space,
//      digits1,
//      digits1Space,
//      digits2,
//      digits2Space,
//      obj_o,
//      obj_b,
//      stream_s,
//      stream_t,
//      stream_r,
//      stream_e,
//      stream_a,
//      end_e,
//      end_n,
//      end_d,
//      endobj_o,
//      endobj_b,
//      endstream_s,
//      endstream_t,
//      endstream_r,
//      endstream_e,
//      endstream_a,
//    }


//    public PdfSourceRichTextBox(Tokeniser tokeniser, StringBuilder sb, MainWindow mainWindow) {
//      this.tokeniser = tokeniser;
//      this.sb = sb;
//      this.mainWindow = mainWindow;
//      pdfObjects = new Dictionary<ObjectId, PdfObjectRun>();
//      pdfRefs = new Dictionary<ObjectId, List<PdfRefRun>>();

//      Document = new FlowDocument();
//      VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
//      HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
//      //  ContextMenu = bytesContextMenu,
//      IsReadOnly = true;

//      var bytesIndex = 0;
//      sb.Clear();
//      var paragraph = new Paragraph();
//      Document.Blocks.Add(paragraph);
//      var state = stateEnum.parse;
//      //var isleadingSpaceFound = false;
//      var number1 = int.MinValue;
//      var number2 = int.MinValue;
//      var number1PosInSb = 0;
//      var isSkipStreamChars = false;
//      var isSkipOneMore = false;
//      ObjectId? objectObjectId = null;
//      while (bytesIndex<tokeniser.PdfBytes.Count) {
//        /*
//        13 0 obj
//        <</Metadata 10 0 R/Pages 9 0 R/Type/Catalog>>
//        endobj

//        21 0 obj
//        <</Filter/FlateDecode/Length 427>>stream
//        endstream
//        endobj

//        */
//        var b = tokeniser.PdfBytes[bytesIndex++];
//        switch (state) {
//        case stateEnum.parse:
//          if (b==' ') {
//            state = stateEnum.space;
//          } else if (b>='0' && b<='9') {
//            number1 = b - '0';
//            number1PosInSb = sb.Length;
//            state = stateEnum.digits1;
//          } else if (b=='e') {
//            state = stateEnum.end_e;
//          } else if (b=='s') {
//            state = stateEnum.stream_s;
//          }
//          break;

//        case stateEnum.space:
//          if (b>='0' && b<='9') {
//            state = stateEnum.digits1;
//            //isleadingSpaceFound = true;
//            number1 = b - '0';
//            number1PosInSb = sb.Length;
//          } else if (b=='e') {
//            state = stateEnum.end_e;
//          } else if (b=='s') {
//            state = stateEnum.stream_s;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.digits1:
//          if (b>='0' && b<='9') {
//            number1 = number1*10 + b - '0';
//          } else if (b==' ') {
//            state = stateEnum.digits1Space;
//          } else {
//            state = stateEnum.parse;
//            //isleadingSpaceFound = false;
//          }
//          break;

//        case stateEnum.digits1Space:
//          if (b>='0' && b<='9') {
//            state = stateEnum.digits2;
//            number2 = b - '0';
//          } else if (b=='e') {
//            state = stateEnum.end_e;
//          } else if (b=='s') {
//            state = stateEnum.stream_s;
//          } else {
//            state = stateEnum.parse;
//            //isleadingSpaceFound = false;
//          }
//          break;

//        case stateEnum.digits2:
//          if (b>='0' && b<='9') {
//            number2 = number2*10 + b - '0';
//          } else if (b==' ') {
//            state = stateEnum.digits2Space;
//          } else {
//            state = stateEnum.parse;
//            //isleadingSpaceFound = false;
//          }
//          break;

//        case stateEnum.digits2Space:
//          if (b=='R') {
//            var objString = sb.ToString();
//            sb.Clear();
//            paragraph.Inlines.Add(new Run(objString[..(number1PosInSb)]));
//            paragraph.Inlines.Add(new PdfRefRun(new ObjectId(number1, number2), this));
//            isSkipStreamChars = true;
//            isSkipOneMore = true;
//            //isleadingSpaceFound = false;
//            state = stateEnum.parse;
//          } else if (b=='o') {
//            state = stateEnum.obj_o;
//            //isleadingSpaceFound = false;
//          } else if (b>='0' && b<='9') {
//            //sequence of 3 numbers found. Discard first number
//            state = stateEnum.digits2;
//            //isleadingSpaceFound = true;
//            number1 = number2;
//            number2 = b - '0';
//          } else {
//            state = stateEnum.parse;
//            //isleadingSpaceFound = false;
//          }
//          break;

//        case stateEnum.obj_o:
//          if (b=='b') {
//            state = stateEnum.obj_b;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.obj_b:
//          if (b=='j') {
//            var objString = sb.ToString();
//            sb.Clear();
//            paragraph.Inlines.Add(new Run(objString[..number1PosInSb]));
//            objectObjectId = new ObjectId(number1, number2);
//            paragraph.Inlines.Add(new PdfObjectRun(objectObjectId.Value, this));
//            isSkipStreamChars = true;
//            isSkipOneMore = true;
//          }
//          state = stateEnum.parse;
//          break;

//        case stateEnum.stream_s:
//          if (b=='t') {
//            state = stateEnum.stream_t;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.stream_t:
//          if (b=='r') {
//            state = stateEnum.stream_r;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.stream_r:
//          if (b=='e') {
//            state = stateEnum.stream_e;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.stream_e:
//          if (b=='a') {
//            state = stateEnum.stream_a;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.stream_a:
//          if (b=='m') {
//            isSkipStreamChars = true;
//            var skipLenght = Math.Min(5, sb.Length);
//            sb.Length -= skipLenght;
//            paragraph.Inlines.Add(new Run(sb.ToString()));
//            sb.Clear();
//          }
//          state = stateEnum.parse;
//          break;

//        case stateEnum.end_e:
//          if (b=='n') {
//            state = stateEnum.end_n;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.end_n:
//          if (b=='d') {
//            state = stateEnum.end_d;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.end_d:
//          if (b=='o') {
//            state = stateEnum.endobj_o;
//          } else if (b=='s') {
//            state = stateEnum.endstream_s;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.endobj_o:
//          if (b=='b') {
//            state = stateEnum.endobj_b;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.endobj_b:
//          if (b=='j') {
//            objectObjectId = null;
//          }
//          state = stateEnum.parse;
//          break;

//        case stateEnum.endstream_s:
//          if (b=='t') {
//            state = stateEnum.endstream_t;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.endstream_t:
//          if (b=='r') {
//            state = stateEnum.endstream_r;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.endstream_r:
//          if (b=='e') {
//            state = stateEnum.endstream_e;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.endstream_e:
//          if (b=='a') {
//            state = stateEnum.endstream_a;
//          } else {
//            state = stateEnum.parse;
//          }
//          break;

//        case stateEnum.endstream_a:
//          if (b=='m') {
//            isSkipOneMore = true;
//            paragraph.Inlines.Add(new PdfStreamRun(objectObjectId!.Value, this));
//            var streamToken = (DictionaryToken)tokeniser.GetToken(objectObjectId!.Value)!;
//            if (streamToken is DictionaryToken streamDictionaryToken && streamDictionaryToken.Type=="XRef") {
//              if (streamDictionaryToken.PdfObject is string xRefString) {
//                paragraph.Inlines.Add(new Run(Environment.NewLine + "XRef stream content:" + Environment.NewLine + xRefString));
//              }
//            }
//          }
//          state = stateEnum.parse;
//          break;

//        default:
//          throw new NotSupportedException();
//        }

//        if (!isSkipStreamChars) {
//          var c = PdfEncodings.PdfEncoding[b];
//          if (c==0xFFFF) {
//            sb.Append('\'' + b.ToString("x") + '\'');
//          } else {
//            sb.Append(c);
//          }
//        }

//        if (isSkipOneMore) {
//          isSkipOneMore = false;
//          isSkipStreamChars = false;
//        }
//      }
//      paragraph.Inlines.Add(new Run(sb.ToString()));
//      sb.Clear();
//    }


//    public class PdfObjectRun: Run {
//      public readonly ObjectId ObjectId;
//      readonly PdfSourceRichTextBox pdfSourceRichTextBox;


//      public PdfObjectRun(ObjectId objectId, PdfSourceRichTextBox pdfSourceRichTextBox) {
//        ObjectId = objectId;
//        this.pdfSourceRichTextBox = pdfSourceRichTextBox;
//        pdfSourceRichTextBox.pdfObjects[objectId] = this; //overwrites older with later version

//        FontWeight = FontWeights.Bold;
//        Text = $"{objectId.ObjectNumber} {objectId.Generation} obj";
//      }


//      protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
//        if (e.ClickCount==2) {
//          e.Handled = true;
//          pdfSourceRichTextBox.Selection.Select(ContentStart, ContentEnd);
//          //BringIntoView();
//        }
//        base.OnPreviewMouseLeftButtonDown(e);
//      }
//    }
  
  
//    public class PdfRefRun: Run {


//      public readonly ObjectId ObjectId;


//      readonly PdfSourceRichTextBox pdfSourceRichTextBox;


//      public PdfRefRun(ObjectId objectId, PdfSourceRichTextBox pdfSourceRichTextBox) {
//        ObjectId = objectId;
//        this.pdfSourceRichTextBox = pdfSourceRichTextBox;
//        if (!pdfSourceRichTextBox.pdfRefs.TryGetValue(objectId, out var pdfRefList)) {
//          pdfRefList = new List<PdfRefRun>();
//          pdfSourceRichTextBox.pdfRefs.Add(objectId, pdfRefList);
//        }
//        pdfRefList.Add(this);

//        TextDecorations = System.Windows.TextDecorations.Underline;
//        Foreground = Brushes.DarkBlue;
//        Text = $"{objectId.ObjectNumber} {objectId.Generation} R";
        
//      }


//      protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
//        if (e.ClickCount==2) {
//          e.Handled = true;
//          if (!pdfSourceRichTextBox.pdfObjects.TryGetValue(ObjectId, out var targetPdfObject)) {
//            MessageBox.Show($"Could not find object {ObjectId}");
//            return;
//          }
//          pdfSourceRichTextBox.Selection.Select(targetPdfObject.ContentStart, targetPdfObject.ContentEnd);
//          //targetPdfObject.BringIntoView();
//          pdfSourceRichTextBox.mainWindow.AddToTrace(this);
//        }
//        base.OnPreviewMouseLeftButtonDown(e);
//      }

//      internal void SetFocus() {
//        pdfSourceRichTextBox.Focus();
//        pdfSourceRichTextBox.Selection.Select(ContentStart, ContentEnd);
//      }
//    }


//    PdfStreamWindow? streamWindow;


//    public void ResetStreamWindow() { streamWindow = null; }


//    public class PdfStreamRun: Run {


//      public readonly ObjectId ObjectId;


//      readonly PdfSourceRichTextBox pdfSourceRichTextBox;


//      public PdfStreamRun(ObjectId objectId, PdfSourceRichTextBox pdfSourceRichTextBox) {
//        ObjectId = objectId;
//        this.pdfSourceRichTextBox = pdfSourceRichTextBox;
//        FontWeight = FontWeights.Bold;
//        Text = "stream...endstream";
//      }


//      protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
//        if (e.ClickCount==2) {
//          e.Handled = true;
//          pdfSourceRichTextBox.Selection.Select(ContentStart, ContentEnd);

//          if (pdfSourceRichTextBox.streamWindow is null) {
//            pdfSourceRichTextBox.streamWindow = 
//              new PdfStreamWindow(pdfSourceRichTextBox.tokeniser, pdfSourceRichTextBox.sb, ObjectId, pdfSourceRichTextBox);
//          } else {
//            pdfSourceRichTextBox.streamWindow.Update(ObjectId);
//          }
//        }
//        base.OnPreviewMouseLeftButtonDown(e);
//      }
//    }
//  }
//}
