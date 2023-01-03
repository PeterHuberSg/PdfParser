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


  /// <summary>
  /// Inidicates which lines of the pdf document should be displayed in the TextViewer
  /// </summary>
  public class ViewLines {
    public readonly int StartDocuLine;
    public readonly int LinesCount;
    public readonly int EndDocuLine;

    public ViewLines(int startDocuLine, int linesCount) {
      if (linesCount<=0) throw new ArgumentException();

      StartDocuLine = startDocuLine;
      LinesCount = linesCount;
      EndDocuLine = startDocuLine + linesCount;
    }

    public override string ToString() {
      return $"StartDocuLine: {StartDocuLine}; EndDocuLine: {EndDocuLine};";
    }
  }



  /// <summary>
  /// Displays AdrLabels and the text of a pdf file in the TextViewer window.
  /// </summary>
  public class TextViewerGlyph: CustomControlBase {

    #region Properties
    //      ----------

    /// <summary>
    /// First and last line to get displayed
    /// </summary>
    public ViewLines? ViewLines { get; private set; }


    /// <summary>
    /// Width of longest line found in document, incl. BorderX and AdrLabelWidth
    /// </summary>
    public double MaxDocuLineWidth { get; private set;}


    /// <summary>
    /// number of x pixels needed to display empty border pixels and 'pdf file byte offset' before the actual text
    /// </summary>
    public double TextStartDocuX { get; private set; }


    /// <summary>
    /// xOffset of first pixel to be displayed based on horizontal scrollbar value
    /// </summary>
    public double ScrollViewX { get; private set;}


    /// <summary>
    /// Contains width for every character on a displayed text line. A character has the width 0 when it is a formatting character. 
    /// </summary>
    public IReadOnlyList<double> DisplayedGlyphWidths => displayedGlyphWidths;


    /// <summary>
    /// Indicates that next byte is a formatting control character
    /// </summary>
    public const byte StartFormat = (byte)'{';


    /// <summary>
    /// Ends formatting instruction
    /// </summary>
    public const byte EndFormat = (byte)'}';


    /// <summary>
    /// Replacement for characters which cannot be displayed in a certain font
    /// </summary>
    public const char ErrorChar = '¿';


    /// <summary>
    /// Contains the measurement information of normal (not bold) font
    /// </summary>
    public GlyphTypeface GlyphTypefaceNormal {
      get { return glyphTypefaceNormal; }
    }
    GlyphTypeface glyphTypefaceNormal;


    /// <summary>
    /// Contains the measurement information of bold font
    /// </summary>
    public GlyphTypeface GlyphTypefaceBold {
      get { return glyphTypefaceBold; }
    }
    GlyphTypeface glyphTypefaceBold;


    /// <summary>
    /// Screen resolution. 
    /// </summary>
    public float PixelsPerDip { get; private set; }


    /// <summary>
    /// offset from left border before first character gets written
    /// </summary>
    public const int ViewBorderX = 3;
    #endregion


    #region Constructor
    //      -----------

    readonly TextViewer textViewer;
    readonly Action maxLineWidthChanged;


    public TextViewerGlyph(TextViewer textViewer, Action maxLineWidthChanged) 
    {
      this.textViewer = textViewer;
      this.maxLineWidthChanged = maxLineWidthChanged;
    }
    #endregion


    #region Methods
    //      -------

    public void Reset() {
      ViewLines = null;
      MaxDocuLineWidth = 0;
      ScrollViewX = 0;
    }


    public void SetViewLines(ViewLines viewLines) {
      if (ViewLines is null ||
        ViewLines.StartDocuLine!=viewLines.StartDocuLine || 
        ViewLines.LinesCount!=viewLines.LinesCount) 
      {
        ViewLines = viewLines;
        //textViewer.LogLine($"Glyph.SetViewLines {viewLines}");
        InvalidateVisual();
      }
    }


    /// <summary>
    /// Sets the x address of leftmost pixel to be displayed
    /// </summary>
    public void SetScrollViewX(double scrollViewX) {
      if (ScrollViewX!=scrollViewX) {
        ScrollViewX = scrollViewX;
        //textViewer.LogLine($"Glyph.SetScrollViewX {scrollViewX:F0}");
        InvalidateVisual();
      }
    }


    TextViewerAnchor? markAnchor;


    public void SetMarker(TextViewerAnchor? anchor) {
      markAnchor = anchor;
    }


    /// <summary>
    /// Returns the pixel offset of the selection relative to the first text character
    /// </summary>
    public double GetTextX(TextStoreSelection selection) {
      var textStore = textViewer.TextStore;
      var chars = textStore.Chars;
      //var charsIndex = textViewer.TextStore.LineStarts[selection.StartLine];
      var glyphTypeface = glyphTypefaceNormal;
      var x = 0.0;
      var isStartFormatFound = false;
      var isBold = false;
      for (int charCount = 0; charCount < selection.StartChar; charCount++) {
        var c = chars[charCount];
        if (isStartFormatFound) {
          isStartFormatFound = false;
          if (c!=StartFormat) {
            if (c=='b') {
              isBold = true;
              glyphTypeface = glyphTypefaceBold;
            }
            continue;//skip character after '{'
          }

        } else { 
          //previous character is not '{'
          if (c==StartFormat) {
            isStartFormatFound = true;
            continue; //skip first '{'
          }
          if (c==EndFormat) {
            if (isBold) {
              isBold = false;
              glyphTypeface = glyphTypefaceNormal;
            }
            continue; //skip first '}'
          }
        }

        if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(c, out var glyphIndex)) {
          glyphIndex = glyphTypeface.CharacterToGlyphMap[ErrorChar];
        };
        x += glyphTypeface.AdvanceWidths[glyphIndex] * FontSize;
      }
      return x;
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


    Typeface typefaceNormal;
    Typeface typefaceBold;


    /*
    Values for horizontal line:
    ---------------------------

                1234567   obj 1 0              //Sample text
    |BorderX | AdrLabel | Text            |
    |<----------MaxDocuLineWidth--------->|
    |<------hSbar.Max----->|<-hSbar.Page->|
    |<-hSBar.Value->|                          //ScrollXOffset

    View pixel address of first text character:
    |<-hSBar.Value->|                          //ScrollXOffset
                    ╔════════════════════╗
                    ║|<-viewX->|         ║     //view
                    ╚════════════════════╝
    */


    protected override void OnRenderContent(DrawingContext drawingContext, Size renderContentSize) {
      if (ViewLines is null) return;

      if (typefaceNormal is null) {
        //first time OnRender is called, initialise fonts
        typefaceNormal = new Typeface(FontFamily, FontStyle, FontWeights.Normal, FontStretch);
        if (!typefaceNormal.TryGetGlyphTypeface(out glyphTypefaceNormal))
          throw new InvalidOperationException($"No plain GlyphTypeface found for {FontFamily.Source}.");

        typefaceBold = new Typeface(FontFamily, FontStyle, FontWeights.Bold, FontStretch);
        if (!typefaceBold.TryGetGlyphTypeface(out glyphTypefaceBold))
          throw new InvalidOperationException("No bold GlyphTypeface found for {FontFamily.Source}.");

        PixelsPerDip = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;
      }

      textViewer.TextViewerObjects.Reset(ViewLines.StartDocuLine);
      displayedGlyphWidths.Clear();
      var viewLineY = FontSize;
      var lastDisplayedDocuLineIndex = Math.Min(ViewLines.EndDocuLine, textViewer.TextStore.LinesCount);
      //textViewer.LogLine1($"Glyph.OnRenderContent DisplayLines: '{DisplayLines}'; " +
      //  $"lastDisplayedDocuLineIndex: {lastDisplayedDocuLineIndex}; XOffset: {XOffset:F0}");
      var hasMaxLineWidthChanged = false;

      //calculate width of address label (displaying byte offset from start of pdf file)
      var adrLabelDigitCount = textViewer.TextStore.CharsCount.ToString().Length;
      glyphTypefaceNormal.CharacterToGlyphMap.TryGetValue('0', out var glyphIndex);
      double digitWidth = glyphTypefaceNormal.AdvanceWidths[glyphIndex] * FontSize;
      var adrLabelEndDocuX = ViewBorderX + adrLabelDigitCount*digitWidth;
      TextStartDocuX = adrLabelEndDocuX + digitWidth;

      for (int displayedDocuLineIndex = ViewLines.StartDocuLine; displayedDocuLineIndex < lastDisplayedDocuLineIndex; displayedDocuLineIndex++) {
        var textWidth = writeLine(drawingContext, viewLineY, FontSize, displayedDocuLineIndex, adrLabelEndDocuX);
        var lineWidth = TextStartDocuX + textWidth;
        if (MaxDocuLineWidth<lineWidth) {
          MaxDocuLineWidth = lineWidth;
          hasMaxLineWidthChanged = true;
        }
        viewLineY += FontSize;
      }
      //SetMarker(null);
      if (hasMaxLineWidthChanged) {
        maxLineWidthChanged();
      }
      textViewer.TextViewerSelection.SetDisplayRegion(ViewLines, ScrollViewX, TextStartDocuX);
      //textViewer.LogLine("Glyph => textViewer.UpdateMouseCursorAndSelection()");
      textViewer.UpdateMouseCursorAndSelection();
    }


    readonly List<ushort> glyphIndexes = new();
    readonly List<double> glyphRunGlyphWidths = new();//width of every glyph (character) in a glyphRun (string with one particular format)
    readonly List<double> displayedGlyphWidths = new();//width of every displayed char from TextStore. Characters which are
                                                       //part of formatting instructions have width 0 (they don't get displayed)

    /// <summary>
    /// Writes the text of 1 line to the DrawingContext, returns the lenght of text in pixels
    /// </summary>
    private double writeLine(
      DrawingContext drawingContext, 
      double viewLineY, 
      double fontSize, 
      int docuLine, 
      double adrLabelEndDocuX) 
    {
      double glyphRunWidth;
      var isLineStartFound = false;
      if (adrLabelEndDocuX>ScrollViewX) {
        //write pdf file byte offset before actual text
        glyphIndexes.Clear();
        glyphRunGlyphWidths.Clear();
        glyphRunWidth = 0;
        var adrLabelString = textViewer.TextStore.LineByteOffsets[docuLine].ToString();
        foreach (var ch in adrLabelString) {
          //addGlyph(ch, glyphTypefaceNormal, fontSize, ref glyphRunWidth, ref lineWidth, ref isLineStartFound, ref xOffset);
          if (!glyphTypefaceNormal.CharacterToGlyphMap.TryGetValue(ch, out var glyphIndex)) {
            glyphIndex = glyphTypefaceNormal.CharacterToGlyphMap[ErrorChar];
          };
          double width = glyphTypefaceNormal.AdvanceWidths[glyphIndex] * fontSize;
          glyphRunWidth += width;

          glyphIndexes.Add(glyphIndex);
          glyphRunGlyphWidths.Add(width);
        }

        //write line address (offset from start of pdf file in bytes) right aligned
        var adrLabelPoint = new Point(adrLabelEndDocuX - glyphRunWidth - ScrollViewX, viewLineY);
        GlyphRun glyphRun = new GlyphRun(glyphTypefaceNormal, 0, false, fontSize, PixelsPerDip, glyphIndexes.ToArray(), adrLabelPoint,
          glyphRunGlyphWidths.ToArray(), null, null, null, null, null, null);
        drawingContext.DrawGlyphRun(Brushes.Gray, glyphRun);
      }

      //write actual text
      ReadOnlySpan<char> text = textViewer.TextStore[docuLine];
      if (text.Length==0) {
        displayedGlyphWidths.Add(0);//for CR for empty line
        return 0;
      }

      var isUnFormatted = true;
      var isAnchor = false;
      var isLink = false;
      var isStream = false;
      var stringBuilder = new StringBuilder();
      var glyphTypeface = glyphTypefaceNormal;
      var brush = Brushes.Black;
      var isUnderline = false;
      glyphIndexes.Clear();
      glyphRunGlyphWidths.Clear();
      glyphRunWidth = 0;
      var viewX = TextStartDocuX - ScrollViewX;
      var textWidth = 0.0;

      for (int charIndex = 0; charIndex<text.Length; charIndex++) {
        //convert 1 or 2 chars into CodePoint
        int codePoint = text[charIndex];
        int nextCodePoint;
        //https://docs.microsoft.com/en-us/dotnet/standard/base-types/character-encoding-introduction#surrogate-pairs
        if (codePoint<0xd800) {
          // codePoint consists of only 1 integer, nothing to do
        } else if (codePoint<0xdc00) {
          //high surrogate code point
          if (charIndex>=text.Length) {
            //low surrogate code point missing
            System.Diagnostics.Debugger.Break();
            codePoint = ErrorChar;
          } else {
            var lowCodPoint = (int)text[++charIndex];
            if (lowCodPoint<0xdc00 || lowCodPoint>=0xe000) {
              //illeagel second surrogate code point
              System.Diagnostics.Debugger.Break();
              codePoint = ErrorChar;
            } else {
              codePoint = 0x10000 + ((codePoint - 0xD800) *0x0400) + (lowCodPoint - 0xDC00);
            }
          }
        } else if (codePoint<0xe000) {
          //illeagel low surrogate code point, high should come first
          System.Diagnostics.Debugger.Break();
          codePoint = ErrorChar;
        } else {
          // codePoint consists of only 1 integer, nothing to do
        }

        //detect formatting and draw text with the same format
        if (isUnFormatted) {

          if (codePoint==StartFormat) {
            displayedGlyphWidths.Add(0);//the first '{' never gets displayed
            if (!getNext(out nextCodePoint, text, ref charIndex)) break;

            switch (nextCodePoint) {
            case StartFormat:
              //second '{' found, just write it
              break;

            case 'a': //anchor, links can point to it
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              if (markAnchor is not null) {
                isAnchor = true;
                stringBuilder.Clear();
              }
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY,
                isLineStartFound, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Black;
              isUnderline = false;
              break;

            case 'b': //blue
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY, 
                isLineStartFound, drawingContext);
              glyphTypeface = glyphTypefaceNormal;
              brush = Brushes.Blue;
              isUnderline = false;
              break;

            case 'B': //bold
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY,
                isLineStartFound, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Black;
              isUnderline = false;
              break;

            case 'e': //error
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY,
                isLineStartFound, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.DarkRed;
              isUnderline = false;
              break;

            case 'l':
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              isLink = true;
              stringBuilder.Clear();
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY,
                isLineStartFound, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Blue;
              isUnderline = true;
              break;

            case 's':
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              isStream = true;
              stringBuilder.Clear();
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY,
                isLineStartFound, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Green;
              isUnderline = true;
              break;

            case 'u':
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY,
                isLineStartFound, drawingContext);
              glyphTypeface = glyphTypefaceNormal;
              brush = Brushes.Black;
              isUnderline = true;
              break;

            default:
              //unknown formatting character found
              //System.Diagnostics.Debugger.Break();
              addGlyph(StartFormat, glyphTypeface, fontSize, ref glyphRunWidth, ref textWidth, ref isLineStartFound, ref viewX);
              addGlyph(nextCodePoint, glyphTypeface, fontSize, ref glyphRunWidth, ref textWidth, ref isLineStartFound, ref viewX);
              codePoint = ErrorChar;
              break;
            }

          } else if (codePoint==EndFormat) {
            if (!getNext(out nextCodePoint, text, ref charIndex)) break;

            if (nextCodePoint==EndFormat) {
              //double '}' found, draw only 1
              displayedGlyphWidths.Add(0);
            } else {
              //single '}' found, but there is no start format, just draw it and the following glyph.
              //if the nextCodePoint is a '{', it will not be used as start format. Easier code
              addGlyph(EndFormat, glyphTypeface, fontSize, ref glyphRunWidth, ref textWidth, ref isLineStartFound, ref viewX);
              codePoint = nextCodePoint;
            }
          }

        } else {
          //glyphs are formatted
          if (codePoint==StartFormat) {
            if (!getNext(out nextCodePoint, text, ref charIndex)) break;

            if (nextCodePoint==StartFormat) {
              //second '{' found, just write it
              displayedGlyphWidths.Add(0);

            } else {
              //single '{', draw it with next glyph
              //System.Diagnostics.Debugger.Break();
              addGlyph(StartFormat, glyphTypeface, fontSize, ref glyphRunWidth, ref textWidth, ref isLineStartFound, ref viewX);
              codePoint = nextCodePoint;
            }

          } else if (codePoint==EndFormat) {
            displayedGlyphWidths.Add(0);
            var isNextCodePoint = getNext(out nextCodePoint, text, ref charIndex);

            if (isNextCodePoint && nextCodePoint==EndFormat) {
              //double '}' found, draw only 1
            } else {
              //end of formatting found

              if (isAnchor) {
                var name = stringBuilder.ToString();
                if (textViewer.Anchors.TryGetValue(name, out var anchor)) {
                  if (anchor==markAnchor) {
                    drawingContext.DrawRectangle(Brushes.LightGreen, null,
                      new Rect(
                        viewX,
                        viewLineY - fontSize + textViewer.SelectionYOffset,
                        glyphRunWidth, fontSize));
                  }
                }

              } else if (isLink) {
                var objectIdString = stringBuilder.ToString();
                if (textViewer.Anchors.TryGetValue(objectIdString, out var anchor)) {
                  var docuX = viewX - TextStartDocuX + ScrollViewX;
                  textViewer.TextViewerObjects.AddLink(anchor, docuLine, docuX, docuX + glyphRunWidth);
                } else {
                  //System.Diagnostics.Debugger.Break(); //anchors are not available when an exception occured
                }

              } else if (isStream) {
                var objectIdString = stringBuilder.ToString();
                var docuX = viewX - TextStartDocuX + ScrollViewX;
                textViewer.TextViewerObjects.AddStream(new ObjectId(objectIdString), docuLine, docuX, docuX + glyphRunWidth);
              }

              if (!isNextCodePoint) {
                codePoint = int.MinValue;
                break;
              }

              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY,
               isLineStartFound, drawingContext);
              isUnFormatted = true;
              if (nextCodePoint==StartFormat) {
                //already start of next format. Process '{' once more
                codePoint = int.MinValue;
                charIndex--;

              } else {
                //unformatted text found
                glyphTypeface = glyphTypefaceNormal;
                brush = Brushes.Black;
                isUnderline = false;
                codePoint = nextCodePoint;
              }
            }
          } else {
            //codepoint within format found
            if (isAnchor || isLink || isStream) {
              //if (isLink) {
              stringBuilder.Append((char)codePoint);
            }
          }
        }

        //concatenate glyphs with the same format
        addGlyph(codePoint, glyphTypeface, fontSize, ref glyphRunWidth, ref textWidth, ref isLineStartFound, ref viewX);
      }
      drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref viewX, viewLineY,
        isLineStartFound, drawingContext);
      displayedGlyphWidths.Add(0);//for CR at the end of line
      return textWidth;
    }


    private bool getNext(out int nextCodePoint, ReadOnlySpan<char> text, ref int charIndex) {
      charIndex++;
      if (charIndex>=text.Length) {
        nextCodePoint = int.MaxValue;
        return false;
      }

      nextCodePoint = text[charIndex];
      return true;
    }


    private void addGlyph(int codePoint, GlyphTypeface glyphTypeface, double size, ref double glyphRunWidth, 
      ref double textWidth, ref bool isLineStartFound, ref double viewX) 
    {
      if (codePoint>=0) {
        if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(codePoint, out var glyphIndex)) {
          glyphIndex = glyphTypeface.CharacterToGlyphMap[ErrorChar];
        };
        double width = glyphTypeface.AdvanceWidths[glyphIndex] * size;
        displayedGlyphWidths.Add(width);
        textWidth += width;
        if (!isLineStartFound) {
          viewX += width;
          if (viewX>0) {
            isLineStartFound = true;
            viewX -= width;
          }
        }
        if (isLineStartFound) {
          glyphRunWidth += width;

          glyphIndexes.Add(glyphIndex);
          glyphRunGlyphWidths.Add(width);
        }
      }
    }


    private void drawGlyphRun(
      GlyphTypeface glyphTypeface,
      Brush brush,
      bool isUnderline,
      double fontSize,
      ref double glyphRunWidth,
      ref double viewX,
      double viewLineY,
      bool isLineStartFound, 
      DrawingContext drawingContext) 
    {
      if (glyphIndexes.Count==0 || !isLineStartFound) return;

      GlyphRun glyphRun = new GlyphRun(glyphTypeface, 0, false, fontSize, PixelsPerDip, glyphIndexes.ToArray(), 
        new Point(viewX, viewLineY), glyphRunGlyphWidths.ToArray(), null, null, null, null, null, null);
      drawingContext.DrawGlyphRun(brush, glyphRun);

      if (isUnderline) {
        var underlineHeight = glyphTypeface.UnderlineThickness * fontSize * 2;//times 2 because it looks too thin
        var underlineY = viewLineY + underlineHeight;
        var guidelines = new GuidelineSet();
        guidelines.GuidelinesY.Add(underlineY);
        guidelines.GuidelinesY.Add(underlineY + underlineHeight);
        drawingContext.PushGuidelineSet(guidelines);
        try {
          drawingContext.DrawRectangle(brush, pen: null, new Rect(viewX, underlineY, glyphRunWidth, underlineHeight));
        } finally {
          drawingContext.Pop();
        }
      }
      glyphIndexes.Clear();
      glyphRunGlyphWidths.Clear();
      viewX += glyphRunWidth;
      glyphRunWidth = 0;
    }
    #endregion
  }
}
