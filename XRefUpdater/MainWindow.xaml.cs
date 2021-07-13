using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PdfParserLib;


namespace XRefUpdater {


  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow: Window {

    DirectoryInfo xRefUpdaterDirectory;


    #pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    public MainWindow() {
    #pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
      InitializeComponent();

      UpdateButton.Click += updateButton_Click;

      xRefUpdaterDirectory = new DirectoryInfo(Environment.CurrentDirectory).Parent.Parent.Parent;
      var fileSource = xRefUpdaterDirectory.FullName + @"\PdfTestSample.txt";
      SourceTextBox.Text = File.ReadAllText(fileSource);
      var sampleToPdf = new SampleToPdf(SourceTextBox.Text);
      UpdatedTextBox.Text = sampleToPdf.Translate();

      var bytes = new byte[UpdatedTextBox.Text.Length];
      for (int bytesIndex = 0; bytesIndex < bytes.Length; bytesIndex++) {
        bytes[bytesIndex] = (byte)UpdatedTextBox.Text[bytesIndex];
      }
      var fileDestination = xRefUpdaterDirectory.FullName + @"\PdfTestSample.pdf";
      File.WriteAllBytes(fileDestination, bytes);

      //var bytes = File.ReadAllBytes(fileDestination);
      //var chars = UpdatedTextBox.Text;
      //var length = Math.Min(bytes.Length, chars.Length);
      //var sb = new StringBuilder();
      //for (int i = 0; i < length; i++) {
      //  sb.AppendLine($"{i:000} {(int)chars[i]} {chars[i]} {bytes[i]} {(char)bytes[i]}");
      //}
      //var s = sb.ToString();
      //update();
    }


    private void updateButton_Click(object sender, RoutedEventArgs e) {
      update();
    }


    enum parseStateEnum {
      parse,
      newLine,
      number1,
      number2,
      stream,
      xref,
      trailer
    }


    string source;
    int sourceIndex;


    private void update() {
      LengthTextBox.Text = "";
      source = SourceTextBox.Text;
      var sb = new StringBuilder();
      var objectAddresses = new SortedList<int, int>();
      var parseState = parseStateEnum.parse;
      var number1 = int.MinValue;
      var number1Index = int.MinValue;
      var number2 = int.MinValue;
      var streamCount = int.MinValue;
      var xrefIndex = int.MinValue;
      for (sourceIndex = 0; sourceIndex < source.Length; sourceIndex++) {
        var c = source[sourceIndex];
        if (parseState<parseStateEnum.xref) {
          sb.Append(c);
        }

        switch (parseState) {
        case parseStateEnum.parse:
          if (c=='\r' && source[sourceIndex+1]=='\n') {
            c = source[++sourceIndex];
            sb.Append(c);
            parseState = parseStateEnum.newLine;
          }
          break;

        case parseStateEnum.newLine:
          if (c>='0' && c<='9') {
            number1 = c - '0';
            number1Index = sourceIndex;
            parseState = parseStateEnum.number1;
          } else if(hasFound("stream", source, ref sourceIndex, sb)) {
            parseState = parseStateEnum.stream;
            streamCount = 0;
          } else {
            var sourceIndexCopy = sourceIndex;
            if (hasFound("xref", source, ref sourceIndex, sb)) {
              xrefIndex = sourceIndexCopy;
              parseState = parseStateEnum.xref;
            }
          }
          break;

        case parseStateEnum.number1:
          if (c>='0' && c<='9') {
            number1 = number1*10 + c-'0';
          } else {
            var c1 =  source[sourceIndex+1];
            if (c==' ' && c1>='0' && c1<='9') {
              number2 = c1 - '0';
              c = source[++sourceIndex];
              sb.Append(c);
              parseState = parseStateEnum.number2;
            } else {
              parseState = parseStateEnum.parse;
            }
          }
          break;

        case parseStateEnum.number2:
          if (c>='0' && c<='9') {
            number2 = number2*10 + c-'0';
          } else if (c==' ' && source[sourceIndex+1]=='o' && source[sourceIndex+2]=='b' && source[sourceIndex+3]=='j') {
            if (number2!=0) throw new Exception($"Generation of object {number1} should be 0, but was {number2}.");

            objectAddresses.Add(number1, number1Index);
            c = source[++sourceIndex];
            sb.Append(c);
            c = source[++sourceIndex];
            sb.Append(c);
            c = source[++sourceIndex];
            sb.Append(c);
            parseState = parseStateEnum.parse;
          } else {
            parseState = parseStateEnum.parse;
          }
          break;

        case parseStateEnum.stream:
          streamCount++;
          if (hasFound("endstream", source, ref sourceIndex, sb)){
            streamCount -= 3;
            LengthTextBox.Text += streamCount.ToString() + "; ";
            parseState = parseStateEnum.parse;
          }
          break;

        case parseStateEnum.xref:
          if (hasFound("trailer", source, sourceIndex)) {
            sb.AppendLine();
            sb.AppendLine($"0 {objectAddresses.Count+1}");
            sb.AppendLine("0000000000 65535 f");
            foreach (var objectId_Address in objectAddresses) {
              sb.AppendLine($"{objectId_Address.Value:0000000000} 00000 n");
            }
            sb.AppendLine();
            sb.Append("t");
            parseState = parseStateEnum.trailer;
          }
          break;

        case parseStateEnum.trailer:
          sb.Append(c);
          //if (hasFound("/Size ", source, sourceIndex) {
          //  //   << /Size 8
          //  c = source[sourceIndex];
          //  while (c>='0' && c<='9') {
          //    c = source[++sourceIndex];
          //  }
          //  sb.AppendLine((objectAddresses.Count+1).ToString());

          //}
          if (hasFound("startxref", source, sourceIndex)) {
            sb.AppendLine("tartxref");
            sb.AppendLine($"{xrefIndex}");
            sb.Append("%%EOF");
            UpdatedTextBox.Text = sb.ToString();
            var file = xRefUpdaterDirectory.FullName + @"\H3 Simple Text String Example Updated.pdf";
            File.WriteAllText(file, UpdatedTextBox.Text);
            return;
          }
          break;

        default:
          throw new NotSupportedException();
        }
      }
    }


    private bool hasFound(string searchString, string source, ref int sourceIndex, StringBuilder sb) {
      for (int i = 0; i < searchString.Length; i++) {
        if (searchString[i]!=source[sourceIndex + i]) {
          return false;
        }
      }

      for (int i = 0; i < searchString.Length-1; i++) {
        var c = source[++sourceIndex];
        sb.Append(c);
      }
      return true;
    }


    private bool hasFound(string searchString, string source, int sourceIndex) {
      for (int i = 0; i < searchString.Length; i++) {
        if (searchString[i]!=source[sourceIndex + i]) {
          return false;
        }
      }

      return true;
    }


    /// <summary>
    /// Shows the file content at the present reading position
    /// </summary>
    public string ShowSourceAtIndex() {
      var startEarlier = Math.Max(0, sourceIndex-100);
      var endLater = Math.Min(source.Length, sourceIndex+100);
      var sb = new StringBuilder();
      int showIndex = startEarlier;
      for (; showIndex < sourceIndex; showIndex++) {
        append(sb, source[showIndex]);
      }
      //sb.AppendLine();
      sb.Append("==>");
      if (showIndex<source.Length) {
        append(sb, source[showIndex++]);
      }
      sb.Append("<==");
      for (; showIndex < endLater; showIndex++) {
        append(sb, source[showIndex]);
      }
      sb.AppendLine();
      return sb.ToString();
    }


    private void append(StringBuilder sb, int b) {
      if (b=='\r' || b=='\n' || (b>=20 && b<127)) {
        sb.Append((char)b);
      } else {
        var ch = PdfEncodings.PdfEncoding[b];
        if (ch<0xFFFF) {
          sb.Append(ch);
        } else {
          sb.Append('\'' + b.ToString("x") + '\'');
        }
      }
    }
  }
}
