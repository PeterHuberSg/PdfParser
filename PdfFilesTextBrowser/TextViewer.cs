using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CustomControlBaseLib;
using PdfParserLib;


namespace PdfFilesTextBrowser {


  public class TextViewer: CustomControlBase {

    #region Properties
    //      ----------

    public IReadOnlyDictionary<string, TextViewerAnchor> Anchors { get { return anchors; } }
    readonly Dictionary<string, TextViewerAnchor> anchors = new();

    public readonly TextViewerObjects TextViewerObjects = new();


    public readonly Window OwnerWindow;


    readonly ScrollBar scrollBar;
    #endregion


    #region Constructor
    //      -----------

    /// <summary>
    /// Default constructor
    /// </summary>
    public TextViewer(Window ownerWindow) {
      OwnerWindow = ownerWindow;
      scrollBar = new ScrollBar {
        Orientation= Orientation.Vertical,
        VerticalAlignment = VerticalAlignment.Stretch,
        Width = 20,
        SmallChange = 1,
      };
      AddChild(scrollBar);

      Loaded += TextViewer_Loaded;
      SizeChanged += TextViewer_SizeChanged;
      MouseWheel += TextViewer_MouseWheel;
      MouseEnter += TextViewer_MouseEnter;
      MouseMove += TextViewer_MouseMove;
      MouseDown += TextViewer_MouseDown;
      KeyDown += TextViewer_KeyDown;
      
      scrollBar.ValueChanged += ScrollBar_ValueChanged;
    }
    #endregion


    #region Eventhandlers
    //      -------------

    private void TextViewer_Loaded(object sender, RoutedEventArgs e) {
      Focus();
    }


    int linesPerPage;
    bool isFirstTime = true;


    private void TextViewer_SizeChanged(object sender, SizeChangedEventArgs e) {
      linesPerPage = (int)Math.Floor(ActualHeight / FontSize);
      scrollBar.LargeChange = linesPerPage;
      //scrollBar.ViewportSize = Math.Min(140, scrollBar.LargeChange);
      scrollBar.ViewportSize = linesPerPage;
      scrollBar.Maximum = textStore.LinesCount-linesPerPage;
      //System.Diagnostics.Debug.WriteLine($"TextViewer_SizeChanged ActualHeight: {ActualHeight}, FontSize: {FontSize}, scrollBar.Value: {scrollBar.Value}");
      if (isFirstTime) {
        InvalidateVisual();
      }
    }


    private void TextViewer_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) {
      if (e.Delta<0) {
        scrollBar.Value = Math.Min(scrollBar.Maximum, scrollBar.Value+5);
      } else {
        scrollBar.Value = Math.Max(0, scrollBar.Value-5);
      }
      //System.Diagnostics.Debug.WriteLine($"TextViewer_MouseWheel scrollBar.Value: {scrollBar.Value}");
    }


    private void TextViewer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) {
      Focus();
    }


    private void TextViewer_MouseMove(object sender, MouseEventArgs e) {
      updateMouseCursor();
    }


    private void updateMouseCursor() {
      var position = Mouse.GetPosition(this);
      if (position.X<0 || position.X>ActualWidth || position.Y<0 || position.Y>ActualHeight) return;//Mouse not in TextViewer

      var displayLine = (int)(position.Y / FontSize);
      var textViewerObject = TextViewerObjects.GetObjectForDisplayLine(displayLine, position.X);
      if (textViewerObject is null) {
        Cursor = Cursors.Arrow;
      } else {
        Cursor = Cursors.Hand;
      }
    }


    PdfStreamWindow? streamWindow;
    StringBuilder streamWindowStringBuilder = new();


    public void ResetStreamWindow() { streamWindow = null; }


    private void TextViewer_MouseDown(object sender, MouseButtonEventArgs e) {
      var position = Mouse.GetPosition(this);
      var displayLine = (int)(position.Y / FontSize);
      var textViewerObject = TextViewerObjects.GetObjectForDisplayLine(displayLine, position.X);
      if (textViewerObject is not null) {
        if (textViewerObject.IsLink) {
          glyphDrawer!.SetMarker(textViewerObject.Anchor);
          scrollBar.Value = Math.Min(Math.Max(textViewerObject.Anchor.Line-3, 0), scrollBar.Maximum);
          //System.Diagnostics.Debug.WriteLine($"TextViewer_MouseDown scrollBar.Value: {scrollBar.Value}");
          InvalidateVisual(); //needs to force a redraw even if exactly the same page gets displayed, so that the anchor 
                              //marker gets drawn
        } else if (textViewerObject.IsStream) {
          if (streamWindow is null) {
            streamWindow = new PdfStreamWindow(tokeniser, streamWindowStringBuilder, textViewerObject.ObjectId, this);
          } else {
            streamWindow.Update(textViewerObject.ObjectId);
          }

        } else {
          System.Diagnostics.Debugger.Break();
          throw new NotSupportedException();
        }
      }
    }


    private void TextViewer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
      switch (e.Key) {
      case Key.NumPad8:
      case Key.Down: scrollBar.Value = Math.Max(scrollBar.Minimum, scrollBar.Value - 1);break;
      case Key.NumPad2:
      case Key.Up: scrollBar.Value = Math.Min(scrollBar.Maximum, scrollBar.Value + 1); break;
      case Key.NumPad7:
      case Key.Home: scrollBar.Value = scrollBar.Minimum; break;
      case Key.NumPad1:
      case Key.End: scrollBar.Value = scrollBar.Maximum; break;
      case Key.NumPad9:
      case Key.PageUp: scrollBar.Value = Math.Max(scrollBar.Minimum, scrollBar.Value - linesPerPage); break;
      case Key.NumPad3:
      case Key.PageDown: scrollBar.Value = Math.Min(scrollBar.Maximum, scrollBar.Value + linesPerPage); break;
      default:
        break;
      }
      //System.Diagnostics.Debug.WriteLine($"KeyDown Key: {e.Key}; SystemKey: {e.SystemKey}; KeyStates: {e.KeyStates}, scrollBar.Value: {scrollBar.Value}");
    }


    private void ScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
      //System.Diagnostics.Debug.WriteLine($"ScrollBar_ValueChanged: {scrollBar.Value}");
      InvalidateVisual();
    }
    #endregion


    #region TextStore
    //      ---------

    readonly TextStore textStore = new();
    Tokeniser tokeniser;

    public void Load(Tokeniser tokeniser) {
      this.tokeniser = tokeniser;
      textStore.Reset();
      anchors.Clear();
      PdfToTextStore.Convert(tokeniser, textStore, anchors);
      scrollBar.Maximum = textStore.LinesCount-linesPerPage;
      scrollBar.Value = 0;
      InvalidateVisual();
      //System.Diagnostics.Debug.WriteLine($"Load() scrollBar.Value: {scrollBar.Value}");
    }


    private bool getNext(out byte nextByte, ReadOnlySpan<byte> text, ref int charIndex) {
      charIndex++;
      if (charIndex>=text.Length) {
        nextByte = 0;
        return false;
      }

      nextByte = text[charIndex];
      return true;
    }
    #region old

    //public void AddLine(ReadOnlySpan<char> source) {
    //  var isUnFormatted = true;
    //  char nextCharacter;

    //  //extract formatted text like anchors or links
    //  var isAnchor = false;
    //  var isLink = false;
    //  var stringBuilder = new StringBuilder();
    //  for (int charIndex = 0; charIndex<source.Length; charIndex++) {
    //    char character = source[charIndex];
    //    if (isUnFormatted) {

    //      if (character==TextViewerGlyphDrawer.StartFormat) {
    //        if (!getNext(out nextCharacter, source, ref charIndex)) break;

    //        switch (nextCharacter) {
    //        case (char)TextViewerGlyphDrawer.StartFormat:
    //          //second '{' found, just write it
    //          break;

    //        case 'a':
    //          isAnchor = true;
    //          isUnFormatted = false;
    //          stringBuilder.Clear();
    //          break;

    //        case 'b':
    //          isUnFormatted = false;
    //          break;

    //        case 'B':
    //          isUnFormatted = false;
    //          break;

    //        case 'l':
    //          isLink = true;
    //          isUnFormatted = false;
    //          break;

    //        case 's':
    //          isLink = true;
    //          isUnFormatted = false;
    //          break;

    //        default:
    //          //unknown formatting character found
    //          //System.Diagnostics.Debugger.Break();
    //          break;
    //        }

    //      // handling of ']' in unformatted string not needed to detect formatting info
    //      //} else if (character==TextViewerGlyphDrawer.EndFormat) {
    //      //  if (!getNext(out nextCharacter, source, ref charIndex)) break;

    //      //  if (nextCharacter==TextViewerGlyphDrawer.EndFormat) {
    //      //    //double '[' found, nothing to do
    //      //  } else {
    //      //    //single ']' found, but there is no start format, just draw it and the following glyph.
    //      //  }
    //      }

    //    } else {
    //      //glyphs are formatted
    //      //--------------------
    //      if (character==TextViewerGlyphDrawer.StartFormat) {
    //        // A '{' was found withing formatting. This is ok if there is another one following or an error for everything
    //        //else. To behave as TextViewerGlyphDrawer, read next character and ignore it.
    //        if (!getNext(out nextCharacter, source, ref charIndex)) break;

    //        //if (nextCharacter==TextViewerGlyphDrawer.StartFormat) {
    //        //  //second '[' found, nothing to do

    //        //} else {
    //        //  //single '[', which is an error, bit nothing to do anyway
    //        //}

    //      } else if (character==TextViewerGlyphDrawer.EndFormat) {
    //        var isNextCharFound = getNext(out var nextChar, source, ref charIndex);

    //        if (isNextCharFound && nextChar==TextViewerGlyphDrawer.EndFormat) {
    //          //double '}' found, nothing to do
    //        } else {
    //          //end of formatting found
    //          isUnFormatted = true;
    //          if (isAnchor) {
    //            isAnchor = false;
    //            var name = stringBuilder.ToString();
    //            //if there are 2 anchors with the same name, only the first one gets added
    //            anchors.TryAdd(name, new TextViewerAnchor(name, textStore.LinesCount));
    //          }
    //          if (!isNextCharFound) break;

    //          if (nextChar==TextViewerGlyphDrawer.StartFormat) {
    //            //already start of next format. Process '{' once more
    //            character = (char)0;
    //            charIndex--;

    //          } else {
    //            //unformatted text found
    //          }
    //        }
    //      } else {
    //        if (isAnchor) {
    //          stringBuilder.Append(character);
    //        }
    //      }
    //    }
    //  }

    //  textStore.AddLine(source);
    //  scrollBar.Maximum = textStore.LinesCount-linesPerPage;
    //  InvalidateVisual();
    //  //System.Diagnostics.Debug.WriteLine("AddLine");
    //}


    //private bool getNext(out char nextChar, ReadOnlySpan<char> text, ref int charIndex) {
    //  charIndex++;
    //  if (charIndex>=text.Length) {
    //    nextChar = (char)0;
    //    return false;
    //  }

    //  nextChar = text[charIndex];
    //  return true;
    //}
    #endregion
    #endregion


    #region Search
    //      ------

    int selectedStartLine;
    int selectedEndLine;
    int selectedStartChar;
    int selectedEndChar;


    internal void Search(string searchString, bool isForward, bool isIgnoreCase) {
      var newLocation = textStore.FindLocation(searchString, isForward, isIgnoreCase);
      if (newLocation is null) return;

      (selectedStartLine, selectedEndLine, selectedStartChar, selectedEndChar) = newLocation.Value;
      InvalidateVisual();
    }
    #endregion


    #region Measure, Arrange and Render
    //      ---------------------------

    protected override Size MeasureContentOverride(Size constraint) {
      scrollBar.Measure(constraint);
      return constraint;
    }


    protected override Size ArrangeContentOverride(Rect arrangeRect) {
      scrollBar.ArrangeBorderPadding(arrangeRect, arrangeRect.Width - scrollBar.DesiredSize.Width, 0,
        scrollBar.DesiredSize.Width, arrangeRect.Height);
      return arrangeRect.Size;
    }


    static TextViewerGlyphDrawer? glyphDrawer;


    protected override void OnRenderContent(System.Windows.Media.DrawingContext drawingContext, Size renderContentSize) {
      //System.Diagnostics.Debug.WriteLine("OnRenderContent");
      if (glyphDrawer is null) {
        glyphDrawer = new TextViewerGlyphDrawer(this, FontFamily, FontStyle, FontStretch, VisualTreeHelper.GetDpi(this).PixelsPerDip);
      }
      var startLine = (int)scrollBar.Value;
      TextViewerObjects.Reset(startLine);
      var lineOffset = FontSize;
      for (int lineIndex = startLine; lineIndex < startLine+linesPerPage; lineIndex++) {
        glyphDrawer.Write(drawingContext, new Point(3, lineOffset), textStore[lineIndex], FontSize, lineIndex);
        lineOffset += FontSize;
      }
      glyphDrawer!.SetMarker(null);
      updateMouseCursor();
    }
    #endregion
  }
}
