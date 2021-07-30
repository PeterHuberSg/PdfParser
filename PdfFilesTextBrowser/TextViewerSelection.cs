using CustomControlBaseLib;
using PdfParserLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PdfFilesTextBrowser {


  public class TextViewerSelection: CustomControlBase {

    #region Properties
    //      ----------

    public TextStoreSelection? Selection { get; private set; }


    public readonly TextViewer TextViewer;
    private readonly IReadOnlyList<double> displayedGlyphWidths;

    #endregion


    #region Constructor
    //      -----------

    public TextViewerSelection(TextViewer textViewer) {
      TextViewer = textViewer;
      displayedGlyphWidths = textViewer.DisplayedGlyphWidths;
    }
    #endregion


    #region Methods
    //      -------

    DisplayLines? displayLines;
    double xDisplayOffset;


    public void Reset() {
      displayLines = null;
      Selection = null;
    }


    public void SetDisplayRegion(DisplayLines displayLines, double xDisplayOffset) {
      if (this.displayLines is null ||
        this.displayLines.StartAbsoluteLine!=displayLines.StartAbsoluteLine || 
        this.displayLines.LinesCount!=displayLines.LinesCount ||
        this.xDisplayOffset!=xDisplayOffset) 
      {
        this.displayLines = displayLines;
        this.xDisplayOffset = xDisplayOffset;
        TextViewer.LogLine($"Selection.SetDisplayRegion displayLines: '{displayLines}'; xDisplayOffset: {xDisplayOffset:F0}");
        InvalidateVisual();
      }
    }


    public void SetSelection(TextStoreSelection? selection, bool isImmediateRenderingNeeded) 
    {
      if (
        (selection is null && Selection is not null) ||
        (selection is not null && Selection is null) ||
        ((selection is not null && Selection is not null) &&
          (Selection.StartLine!=selection.StartLine ||
          Selection.StartChar!=selection.StartChar ||
          Selection.EndLine!=selection.EndLine ||
          Selection.EndChar!=selection.EndChar)))
      {
        Selection = selection; 
        TextViewer.LogLine($"Selection.SetSelection {Selection}");
        if (isImmediateRenderingNeeded) {
          InvalidateVisual();
        }
      }
    }


    public int GetCharPosLeft(int absoluteLine, double x) {
      var displayedGlyphWidthsStartDisplayLineOffset = TextViewer.TextStore.LineStarts[displayLines!.StartAbsoluteLine];
      if (absoluteLine>displayLines.EndAbsoluteLine) {
        //position is outside of displayed lines
        absoluteLine = displayLines.EndAbsoluteLine;
      }
      var displayedGlyphWidthsIndex = TextViewer.TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextViewer.TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;
      var width = 0.0;
      var charPos = -1;
      do {
        double charWidth;
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          //end of line reached
          return charPos;
        }
        charPos++;
        width += charWidth;
      } while (width<x);
      return charPos;
    }
    #endregion


      #region Measure, Arrange and Render
      //      ---------------------------

    protected override Size MeasureContentOverride(Size constraint) {
      return constraint;
    }


    protected override Size ArrangeContentOverride(Rect arrangeRect) {
      return arrangeRect.Size;
    }


    static readonly Pen selectionPen = new Pen(Brushes.LightBlue, 1);
    double glyphYOffset;
    double glyphXOffset;



    protected override void OnRenderContent(DrawingContext drawingContext, Size renderContentSize) {
      if (displayLines is null || Selection is null) return;

      if (Selection.StartLine<displayLines.EndAbsoluteLine && Selection.EndLine>=displayLines.StartAbsoluteLine) {
        //selection is within displayed lines
        TextViewer.LogLine1($"Selection.OnRenderContent displayLines: {displayLines}; xDisplayOffset: {xDisplayOffset:F0}; " +
          $"Selection: {Selection}");
        ///////////////////////
        ////var sb = new StringBuilder();
        ////for (int charIndex = 0; charIndex<TextStore.CharsCount; charIndex++) {
        ////for (int charIndex = 0; charIndex<displayedGlyphWidths.Count; charIndex++) {
        //var textStoreCharsIndex = TextViewer.TextStore.LineStarts[absoluteLine];
        //for (int charIndex = 0; charIndex<20; charIndex++) {
        //  TextViewer.LogLine($"charIndex: {charIndex}, {TextViewer.TextStore.Chars[charIndex]}, {textStoreCharsIndex}, {displayedGlyphWidths[textStoreCharsIndex++]}");
        //  //sb.AppendLine($"charIndex: {charIndex}, {TextViewer.TextStore.Chars[charIndex]}, {displayedGlyphWidths[charIndex]}");
        //}
        ////var s = sb.ToString();
        ///////////////////////

        glyphYOffset = FontSize / 5;
        glyphXOffset = FontSize / 20;
        //selection is visible in the TextViewer
        if (Selection.StartLine==Selection.EndLine) {
          //display only 1 line
          drawSelectionLineFromStartCharToEndChar(drawingContext, Selection.StartLine, Selection.StartChar, Selection.EndChar);
        } else {
          //draw first startSelectionLine if it is in display area
          var lineIndex = Selection.StartLine;
          if (Selection.StartLine>=displayLines.StartAbsoluteLine) {
            drawSelectionLineFromStartCharToEnd(drawingContext, lineIndex++, Selection.StartChar);
          } else {
            lineIndex = displayLines.StartAbsoluteLine;
          }
          //draw lines where the complete line is selected
          var lastLineIndex = Math.Min(Selection.EndLine, displayLines.EndAbsoluteLine);
          for (; lineIndex < lastLineIndex; lineIndex++) {
            drawSelectionLineFrom0ToEnd(drawingContext, lineIndex);
          }
          if (lineIndex<displayLines.EndAbsoluteLine) {
            //draw final selection line
            drawSelectionLineFrom0ToEndChar(drawingContext, Selection.EndLine, Selection.EndChar);
          }
        }
      }
    }


    private void drawSelectionLineFromStartCharToEnd(DrawingContext drawingContext, int absoluteLine, int startCharIndex) {
      var y = (absoluteLine - displayLines!.StartAbsoluteLine) * FontSize + glyphYOffset;
      var x = glyphXOffset;
      var width = 0.0;
      var displayedGlyphWidthsStartDisplayLineOffset = TextViewer.TextStore.LineStarts[displayLines.StartAbsoluteLine];
      var displayedGlyphWidthsIndex = TextViewer.TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextViewer.TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;

      //find x position of first character
      for (var charIndex = 0; charIndex<startCharIndex; charIndex++) {
        double charWidth;
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          System.Diagnostics.Debugger.Break();
          break;
        }
        x += charWidth;
      }
      //measure width of all remaining characters on this line
      while (displayedGlyphWidthsIndex<nextLinefirstCharIndex) {
        double charWidth;
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          break;
        }
        width += charWidth;
      }
      drawingContext.DrawRectangle(Brushes.LightBlue, selectionPen, new Rect(x, y, width, FontSize));
    }


    private void drawSelectionLineFrom0ToEnd(DrawingContext drawingContext, int absoluteLine) {
      var y = (absoluteLine - displayLines!.StartAbsoluteLine) * FontSize + glyphYOffset;
      var x = glyphXOffset;
      var width = 0.0;
      var displayedGlyphWidthsStartDisplayLineOffset = TextViewer.TextStore.LineStarts[displayLines.StartAbsoluteLine];
      var displayedGlyphWidthsIndex = TextViewer.TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextViewer.TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;

      //measure width of all characters on this line
      while (displayedGlyphWidthsIndex<nextLinefirstCharIndex) {
        double charWidth;
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          break;
        }
        width += charWidth;
      }
      drawingContext.DrawRectangle(Brushes.LightBlue, selectionPen, new Rect(x, y, width, FontSize));
    }


    private void drawSelectionLineFrom0ToEndChar(DrawingContext drawingContext, int absoluteLine, int endCharIndex) {
      var y = (absoluteLine - displayLines!.StartAbsoluteLine) * FontSize + glyphYOffset;
      var x = glyphXOffset;
      var width = 0.0;
      var displayedGlyphWidthsStartDisplayLineOffset = TextViewer.TextStore.LineStarts[displayLines.StartAbsoluteLine];
      var displayedGlyphWidthsIndex = TextViewer.TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextViewer.TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;

      //measure width of all selected characters
      for (var charIndex = 0; charIndex<=endCharIndex; charIndex++) {
        double charWidth;
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          //System.Diagnostics.Debugger.Break();
          break;
        }
        width += charWidth;
      }
      drawingContext.DrawRectangle(Brushes.LightBlue, selectionPen, new Rect(x, y, width, FontSize));
    }


    private void drawSelectionLineFromStartCharToEndChar(DrawingContext drawingContext, int absoluteLine, int startCharIndex, 
      int endCharIndex) 
    {
      var charsCount = endCharIndex - startCharIndex + 1;
      var y = (absoluteLine - displayLines!.StartAbsoluteLine) * FontSize + glyphYOffset;
      var x = -xDisplayOffset + glyphXOffset;
      var width = 0.0;
      var displayedGlyphWidthsStartDisplayLineOffset = TextViewer.TextStore.LineStarts[displayLines.StartAbsoluteLine];
      var displayedGlyphWidthsIndex = TextViewer.TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextViewer.TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;

      //find x position of first character
      for (var charIndex = 0; charIndex<startCharIndex; charIndex++) {
        double charWidth;
        //TextViewer.LogLine($"charIndex: {charIndex}");
        //TextViewer.LogLine($"displayedGlyphWidthsIndex: {displayedGlyphWidthsIndex}; charWidth: {displayedGlyphWidths[displayedGlyphWidthsIndex]:F0}");
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          System.Diagnostics.Debugger.Break();
          break;
        }
        x += charWidth;
      }
//TextViewer.LogLine($"x: {x:F0}");
      //measure width of all selected characters
      for (var charIndex = 0; charIndex < charsCount; charIndex++) {
//TextViewer.LogLine($"charIndex: {charIndex}");
        double charWidth;
        //TextViewer.LogLine($"displayedGlyphWidthsIndex: {displayedGlyphWidthsIndex}; charWidth: {displayedGlyphWidths[displayedGlyphWidthsIndex]}");
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          System.Diagnostics.Debugger.Break();
          break;
        }
        width += charWidth;
      }
      drawingContext.DrawRectangle(Brushes.LightBlue, selectionPen, new Rect(x, y, width, FontSize));
//TextViewer.LogLine($"width: {width}");

    }
    #endregion
  }
}
