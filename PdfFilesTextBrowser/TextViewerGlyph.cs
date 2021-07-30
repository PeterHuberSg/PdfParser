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


  public class DisplayLines {
    public readonly int StartAbsoluteLine;
    public readonly int LinesCount;
    public readonly int EndAbsoluteLine;

    public DisplayLines(int startAbsoluteLine, int linesCount) {
      if (linesCount<=0) throw new ArgumentException();

      StartAbsoluteLine = startAbsoluteLine;
      LinesCount = linesCount;
      EndAbsoluteLine = startAbsoluteLine + linesCount;
    }

    public override string ToString() {
      return $"StartAbsoluteLine: {StartAbsoluteLine}; EndAbsoluteLine: {EndAbsoluteLine};";
    }
  }



  public class TextViewerGlyph: CustomControlBase {

    #region Properties
    //      ----------

    /// <summary>
    /// First and last line to get displayed
    /// </summary>
    public DisplayLines? DisplayLines { get; private set; }


    /// <summary>
    /// Width of longest line found in document
    /// </summary>
    public double MaxLineWidth { get; private set;}


    /// <summary>
    /// xOffset of first character to be displayed
    /// </summary>
    public double XOffset { get; private set;}


    /// <summary>
    /// Contains width for every character on a displayed line. A character has the width 0 when it is a formatting character. 
    /// </summary>
    public IReadOnlyList<double> DisplayedGlyphWidths => displayedGlyphWidths;


    /// <summary>
    /// Next byte is a formatting control character
    /// </summary>
    public const byte StartFormat = (byte)'{';


    /// <summary>
    /// Ends formatting instruction
    /// </summary>
    public const byte EndFormat = (byte)'}';


    /// <summary>
    /// Next character ends formatting instruction
    /// </summary>
    public const char ErrorChar = '¿';


    /// <summary>
    /// Contains the measurement information of one particular font and the specified font properties
    /// </summary>
    public GlyphTypeface GlyphTypefaceNormal {
      get { return glyphTypefaceNormal; }
    }
    GlyphTypeface glyphTypefaceNormal;


    /// <summary>
    /// Contains the measurement information of one particular font and the specified font properties
    /// </summary>
    public GlyphTypeface GlyphTypefaceBold {
      get { return glyphTypefaceBold; }
    }
    GlyphTypeface glyphTypefaceBold;


    /// <summary>
    /// Screen resolution. 
    /// </summary>
    public float PixelsPerDip { get; private set; }
    #endregion


    #region Constructor
    //      -----------

    readonly TextViewer textViewer;
    readonly Action isRendered;
    readonly Action maxLineWidthChanged;


    public TextViewerGlyph(TextViewer textViewer, Action isRendered, Action maxLineWidthChanged) 
    {
      this.textViewer = textViewer;
      this.isRendered = isRendered;
      this.maxLineWidthChanged = maxLineWidthChanged;
    }
    #endregion


    #region Methods
    //      -------

    public void Reset() {
      DisplayLines = null;
      MaxLineWidth = 0;
    }


    public void SetDisplayLines(DisplayLines displayLines) {
      if (DisplayLines is null ||
        DisplayLines.StartAbsoluteLine!=displayLines.StartAbsoluteLine || 
        DisplayLines.LinesCount!=displayLines.LinesCount) 
      {
        DisplayLines = displayLines;
        textViewer.LogLine($"Glyph.SetDisplayLines {DisplayLines}");
        InvalidateVisual();
      }
    }


    public void SetXOffset(double xOffset) {
      if (XOffset!=xOffset) {
        XOffset = xOffset;
        textViewer.LogLine($"Glyph.SetXOffset {xOffset:F0}");
        InvalidateVisual();
      }
    }


    public void ResetMaxLineWidth() {
      MaxLineWidth = 0;
    }


    TextViewerAnchor? markAnchor;


    public void SetMarker(TextViewerAnchor? anchor) {
      markAnchor = anchor;
    }


    public double GetStartX(TextStoreSelection selection) {
      var textStore = textViewer.TextStore;
      var chars = textStore.Chars;
      var charsIndex = textViewer.TextStore.LineStarts[selection.StartLine];
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


    protected override void OnRenderContent(DrawingContext drawingContext, Size renderContentSize) {
      if (DisplayLines is null) return;

      if (typefaceNormal is null) {
        typefaceNormal = new Typeface(FontFamily, FontStyle, FontWeights.Normal, FontStretch);
        if (!typefaceNormal.TryGetGlyphTypeface(out glyphTypefaceNormal))
          throw new InvalidOperationException("No plain GlyphTypeface found");

        typefaceBold = new Typeface(FontFamily, FontStyle, FontWeights.Bold, FontStretch);
        if (!typefaceBold.TryGetGlyphTypeface(out glyphTypefaceBold))
          throw new InvalidOperationException("No plain GlyphTypeface found");

        PixelsPerDip = (float)VisualTreeHelper.GetDpi(this).PixelsPerDip;
      }

      textViewer.TextViewerObjects.Reset(DisplayLines.StartAbsoluteLine);
      displayedGlyphWidths.Clear();
      var lineOffset = FontSize;
      var lastDisplayedAbsoluteLineIndex = Math.Min(DisplayLines.EndAbsoluteLine, textViewer.TextStore.LinesCount);
      textViewer.LogLine1($"Glyph.OnRenderContent DisplayLines: '{DisplayLines}'; " +
        $"lastDisplayedAbsoluteLineIndex: {lastDisplayedAbsoluteLineIndex}; XOffset: {XOffset:F0}");
      var hasMaxLineWidthChanged = false;
      for (int displayedAbsoluteLineIndex = DisplayLines.StartAbsoluteLine; displayedAbsoluteLineIndex < lastDisplayedAbsoluteLineIndex; displayedAbsoluteLineIndex++) {
        var lineWidth = writeLine(drawingContext, new Point(3, lineOffset), FontSize, displayedAbsoluteLineIndex);
        lineOffset += FontSize;
        if (MaxLineWidth<lineWidth) {
          MaxLineWidth = lineWidth;
          hasMaxLineWidthChanged = true;
        }
      }
      //SetMarker(null);
      if (hasMaxLineWidthChanged) {
        maxLineWidthChanged();
      }
      textViewer.TextViewerSelection.SetDisplayRegion(DisplayLines, XOffset);
      textViewer.LogLine("Glyph => textViewer.UpdateMouseCursorAndSelection()");
      textViewer.UpdateMouseCursorAndSelection();
    }


    readonly List<ushort> glyphIndexes = new();
    readonly List<double> glyphRunGlyphWidths = new();//width of every glyph (character) in a glyphRun (string with one particular format)
    readonly List<double> displayedGlyphWidths = new();//width of every displayed char from TextStore. Characters which are
                                                       //part of formatting instructions have width 0 (they don't get displayed)


    /// <summary>
    /// Writes the string of 1 line to a DrawingContext
    /// </summary>
    private double writeLine(DrawingContext drawingContext, Point origin, double fontSize, int displayedAbsoluteLineIndex) {
      ReadOnlySpan<char> text = textViewer.TextStore[displayedAbsoluteLineIndex];
      if (text.Length==0) return 0;

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
      double glyphRunWidth = 0;
      bool isLineStartFound = false;
      double xOffset = -XOffset;
      double lineWidth = 0;

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

            case 'a':
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              if (markAnchor is not null) {
                isAnchor = true;
                stringBuilder.Clear();
              }
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref origin, 
                isLineStartFound, xOffset, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Black;
              isUnderline = false;
              break;

            case 'b':
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref origin, 
                isLineStartFound, xOffset, drawingContext);
              glyphTypeface = glyphTypefaceNormal;
              brush = Brushes.Blue;
              isUnderline = false;
              break;

            case 'B':
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref origin, 
                isLineStartFound, xOffset, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Black;
              isUnderline = false;
              break;

            case 'l':
              isUnFormatted = false;
              displayedGlyphWidths.Add(0);
              isLink = true;
              stringBuilder.Clear();
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref origin, 
                isLineStartFound, xOffset, drawingContext);
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
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref origin, 
                isLineStartFound, xOffset, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Green;
              isUnderline = true;
              break;

            default:
              //unknown formatting character found
              //System.Diagnostics.Debugger.Break();
              addGlyph(StartFormat, glyphTypeface, fontSize, ref glyphRunWidth, ref lineWidth, ref isLineStartFound, ref xOffset);
              addGlyph(nextCodePoint, glyphTypeface, fontSize, ref glyphRunWidth, ref lineWidth, ref isLineStartFound, ref xOffset);
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
              addGlyph(EndFormat, glyphTypeface, fontSize, ref glyphRunWidth, ref lineWidth, ref isLineStartFound, ref xOffset);
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
              addGlyph(StartFormat, glyphTypeface, fontSize, ref glyphRunWidth, ref lineWidth, ref isLineStartFound, ref xOffset);
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
                    drawingContext.DrawRectangle(Brushes.LightBlue, null,
                      new Rect(origin.X, origin.Y - fontSize + 3, glyphRunWidth, fontSize));
                  }
                }

              } else if (isLink) {
                var objectIdString = stringBuilder.ToString();
                if (textViewer.Anchors.TryGetValue(objectIdString, out var anchor)) {
                  textViewer.TextViewerObjects.AddLink(anchor, displayedAbsoluteLineIndex, origin.X, origin.X + glyphRunWidth);
                } else {
                  System.Diagnostics.Debugger.Break();
                }

              } else if (isStream) {
                var objectIdString = stringBuilder.ToString();
                textViewer.TextViewerObjects.AddStream(new ObjectId(objectIdString), displayedAbsoluteLineIndex, origin.X, origin.X + glyphRunWidth);
              }

              if (!isNextCodePoint) {
                codePoint = int.MinValue;
                break;
              }

              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref origin, 
                isLineStartFound, xOffset, drawingContext);
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
        addGlyph(codePoint, glyphTypeface, fontSize, ref glyphRunWidth, ref lineWidth, ref isLineStartFound, ref xOffset);
      }
      drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref glyphRunWidth, ref origin, 
        isLineStartFound, xOffset, drawingContext);
      displayedGlyphWidths.Add(0);//for CR at the end of line
      return lineWidth;
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
      ref double lineWidth, ref bool isLineStartFound, ref double xOffset) 
    {
      //if (codePoint>=0) {
      //  if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(codePoint, out var glyphIndex)) {
      //    glyphIndex = glyphTypeface.CharacterToGlyphMap[ErrorChar];
      //  };
      //  glyphIndexes.Add(glyphIndex);
      //  double width = glyphTypeface.AdvanceWidths[glyphIndex] * size;
      //  glyphRunGlyphWidths.Add(width);
      //  displayedGlyphWidths.Add(width);
      //  glyphRunWidth += width;
      //}
      if (codePoint>=0) {
        if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(codePoint, out var glyphIndex)) {
          glyphIndex = glyphTypeface.CharacterToGlyphMap[ErrorChar];
        };
        double width = glyphTypeface.AdvanceWidths[glyphIndex] * size;
        displayedGlyphWidths.Add(width);
        lineWidth += width;
        if (!isLineStartFound) {
          xOffset += width;
          if (xOffset>0) {
            isLineStartFound = true;
            xOffset -= width;
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
      double size,
      ref double glyphRunWidth,
      ref Point origin,
      bool isLineStartFound, 
      double xOffset,
      DrawingContext drawingContext) 
    {
      if (glyphIndexes.Count==0 || !isLineStartFound) return;

      var adjustedOrigin = new Point(origin.X + xOffset, origin.Y);
      GlyphRun glyphRun = new GlyphRun(glyphTypeface, 0, false, size, PixelsPerDip, glyphIndexes.ToArray(), adjustedOrigin,
        glyphRunGlyphWidths.ToArray(), null, null, null, null, null, null);
      drawingContext.DrawGlyphRun(brush, glyphRun);

      if (isUnderline) {
        double underlineHeight = glyphTypeface.UnderlineThickness * size * 2;//times 2 because it looks too thin
        var underlineY = adjustedOrigin.Y + underlineHeight;
        var guidelines = new GuidelineSet();
        guidelines.GuidelinesY.Add(underlineY);
        guidelines.GuidelinesY.Add(underlineY + underlineHeight);
        drawingContext.PushGuidelineSet(guidelines);
        try {
          drawingContext.DrawRectangle(brush, pen: null, new Rect(adjustedOrigin.X, underlineY, glyphRunWidth, underlineHeight));
        } finally {
          drawingContext.Pop();
        }
      }
      glyphIndexes.Clear();
      glyphRunGlyphWidths.Clear();
      origin = new Point(origin.X + glyphRunWidth, origin.Y);
      glyphRunWidth = 0;
      isRendered();
    }
    #endregion
  }
}
