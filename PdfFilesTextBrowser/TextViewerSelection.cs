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


    /// <summary>
    /// vertical pixel offset from start of font to start of displayed selection rectangle 
    /// </summary>
    public double GlyphYOffset;


    public readonly TextViewer TextViewer;
    public readonly TextStore TextStore;
    private readonly IReadOnlyList<double> displayedGlyphWidths;

    #endregion


    #region Constructor
    //      -----------

    public TextViewerSelection(TextViewer textViewer) {
      TextViewer = textViewer;
      TextStore = TextViewer.TextStore;
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
//TextViewer.LogLine($"Selection.SetDisplayRegion displayLines: '{displayLines}'; xDisplayOffset: {xDisplayOffset:F0}");
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
//TextViewer.LogLine($"Selection.SetSelection {Selection}");
        if (isImmediateRenderingNeeded) {
          InvalidateVisual();
        }
      }
    }


    /// <summary>
    /// Returns character index of first character mouse is over. absoluteLine gets correctged if mouse is outside the 
    /// displayed text.
    /// </summary>
    public int GetCharPosLeft(ref int absoluteLine, double x) {
      var displayedGlyphWidthsStartDisplayLineOffset = TextStore.LineStarts[displayLines!.StartAbsoluteLine];
      if (absoluteLine>=TextStore.LinesCount) {
        //position is outside of TextStore, i.e. mouse id over empty lines after all text is displayed
        System.Diagnostics.Debugger.Break(); //this check is already performed by the callers, execution should never come here
        absoluteLine = TextStore.LinesCount - 1;
        return TextStore.LineStarts[TextStore.LinesCount]-TextStore.LineStarts[absoluteLine]-1;
      }

      if (absoluteLine>displayLines.EndAbsoluteLine) {
        //position is outside of displayed lines
        absoluteLine = displayLines.EndAbsoluteLine;
      }
      var displayedGlyphWidthsIndex = TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextStore.LineStarts[absoluteLine+1] - displayedGlyphWidthsStartDisplayLineOffset;
      var width = 0.0;
      var charPos = -1;
      do {
        double charWidth;
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        charPos++;
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          //end of line reached
          return charPos;
        }
        width += charWidth;
      } while (width<x);
      return charPos;
    }
    #endregion


      #region Measure, Arrange and Render
      //      ---------------------------

    protected override Size MeasureContentOverride(Size constraint) {
      GlyphYOffset = FontSize / 5;//set it already here in case TextViewerGlyph needs it
      return constraint;
    }


    protected override Size ArrangeContentOverride(Rect arrangeRect) {
      return arrangeRect.Size;
    }


    static readonly Pen selectionPen = new Pen(Brushes.LightBlue, 1);



    protected override void OnRenderContent(DrawingContext drawingContext, Size renderContentSize) {
      if (displayLines is null || Selection is null) return;

      if (Selection.StartLine<displayLines.EndAbsoluteLine && Selection.EndLine>=displayLines.StartAbsoluteLine) {
        //selection is within displayed lines
        //TextViewer.LogLine1($"Selection.OnRenderContent displayLines: {displayLines}; xDisplayOffset: {xDisplayOffset:F0}; " +
          //$"Selection: {Selection}");
        ///////////////////////
        ////var sb = new StringBuilder();
        ////for (int charIndex = 0; charIndex<TextStore.CharsCount; charIndex++) {
        ////for (int charIndex = 0; charIndex<displayedGlyphWidths.Count; charIndex++) {
        //var textStoreCharsIndex = TextStore.LineStarts[absoluteLine];
        //for (int charIndex = 0; charIndex<20; charIndex++) {
        //  TextViewer.LogLine($"charIndex: {charIndex}, {TextStore.Chars[charIndex]}, {textStoreCharsIndex}, {displayedGlyphWidths[textStoreCharsIndex++]}");
        //  //sb.AppendLine($"charIndex: {charIndex}, {TextStore.Chars[charIndex]}, {displayedGlyphWidths[charIndex]}");
        //}
        ////var s = sb.ToString();
        ///////////////////////

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
      var y = (absoluteLine - displayLines!.StartAbsoluteLine) * FontSize + GlyphYOffset;
      var x = TextViewerGlyph.BorderX - xDisplayOffset;
      var width = 0.0;
      var displayedGlyphWidthsStartDisplayLineOffset = TextStore.LineStarts[displayLines.StartAbsoluteLine];
      var displayedGlyphWidthsIndex = TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;

      //find x position of first selected character
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
      var y = (absoluteLine - displayLines!.StartAbsoluteLine) * FontSize + GlyphYOffset;
      var x = 0;
      var width = -xDisplayOffset;
      var displayedGlyphWidthsStartDisplayLineOffset = TextStore.LineStarts[displayLines.StartAbsoluteLine];
      var displayedGlyphWidthsIndex = TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;

      //measure width of all characters on this line
      while (displayedGlyphWidthsIndex<nextLinefirstCharIndex) {
        double charWidth;
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          break;
        }
        width += charWidth;
      }
      if (width>0) {
        drawingContext.DrawRectangle(Brushes.LightBlue, selectionPen, new Rect(x, y, width, FontSize));
      }
    }


    private void drawSelectionLineFrom0ToEndChar(DrawingContext drawingContext, int absoluteLine, int endCharIndex) {
      var y = (absoluteLine - displayLines!.StartAbsoluteLine) * FontSize + GlyphYOffset;
      var x = 0;
      var width = -xDisplayOffset;
      var displayedGlyphWidthsStartDisplayLineOffset = TextStore.LineStarts[displayLines.StartAbsoluteLine];
      var displayedGlyphWidthsIndex = TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;

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
      if (width>0) {
        drawingContext.DrawRectangle(Brushes.LightBlue, selectionPen, new Rect(x, y, width, FontSize));
      }
    }


    private void drawSelectionLineFromStartCharToEndChar(DrawingContext drawingContext, int absoluteLine, int startCharIndex, 
      int endCharIndex) 
    {
      var charsCount = endCharIndex - startCharIndex + 1;
      if (charsCount==1 && TextStore.Chars[TextStore.LineStarts[absoluteLine]]=='\r') {
        //empty line
        return;
      }
      var y = (absoluteLine - displayLines!.StartAbsoluteLine) * FontSize + GlyphYOffset;
      var x = TextViewerGlyph.BorderX - xDisplayOffset;
      var width = 0.0;
      var displayedGlyphWidthsStartDisplayLineOffset = TextStore.LineStarts[displayLines.StartAbsoluteLine];
      var displayedGlyphWidthsIndex = TextStore.LineStarts[absoluteLine++] - displayedGlyphWidthsStartDisplayLineOffset;
      var nextLinefirstCharIndex = TextStore.LineStarts[absoluteLine] - displayedGlyphWidthsStartDisplayLineOffset;

      //find x position of first character
      for (var charIndex = 0; charIndex<startCharIndex; charIndex++) {
        double charWidth;
//TextViewer.LogLine($"charIndex: {charIndex}; char: {TextStore.GetChar(displayedGlyphWidthsIndex+displayedGlyphWidthsStartDisplayLineOffset)}; " +
//$"displayedGlyphWidthsIndex: {displayedGlyphWidthsIndex}; charWidth: {displayedGlyphWidths[displayedGlyphWidthsIndex]:F0}");
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
//TextViewer.LogLine($"charIndex: {charIndex}; char: {TextStore.GetChar(displayedGlyphWidthsIndex+displayedGlyphWidthsStartDisplayLineOffset)}; " +
//$"displayedGlyphWidthsIndex: {displayedGlyphWidthsIndex}; charWidth: {displayedGlyphWidths[displayedGlyphWidthsIndex]:F0}");
        double charWidth;
        charWidth = displayedGlyphWidths[displayedGlyphWidthsIndex++];
        if (displayedGlyphWidthsIndex>=nextLinefirstCharIndex) {
          if (charWidth>0) {
            //character is not carriage return.
            System.Diagnostics.Debugger.Break();
          }
          break;
        }
        width += charWidth;
      }
//TextViewer.LogLine($"x: {x}; y: {y}; width: {width}; FontSize: {FontSize}; ");
      drawingContext.DrawRectangle(Brushes.LightBlue, selectionPen, new Rect(x, y, width, FontSize));

    }
    #endregion
  }
}
