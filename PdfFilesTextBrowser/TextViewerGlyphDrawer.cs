/**************************************************************************************

PdfFilesTextBrowser.TextViewerGlyphDrawer
=========================================

Special GlyphDrawer for TextViewer, writing formatted text to a DrawingContext. Can also be used to calculate the length of text.

Written 2021 by Jürgpeter Huber. Singapore
Contact: PeterCode at Peterbox dot com

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 license (details see COPYING.txt file, see also
<http://creativecommons.org/publicdomain/zero/1.0/>). 

This software is distributed without any warranty. 
**************************************************************************************/
#pragma warning disable IDE0052 // Remove unread private members
using PdfParserLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;


namespace PdfFilesTextBrowser {

  /// <summary>
  /// Draws glyphs to a DrawingContext. From the font information in the constructor, GlyphDrawer creates and stores 
  /// the GlyphTypeface, which is used every time for the drawing of the string. Can also be used to calculate the
  /// length of text.
  /// </summary>
  public class TextViewerGlyphDrawer {

    #region Properties
    //      ----------

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
    readonly GlyphTypeface glyphTypefaceNormal;


    /// <summary>
    /// Contains the measurement information of one particular font and the specified font properties
    /// </summary>
    public GlyphTypeface GlyphTypefaceBold {
      get { return glyphTypefaceBold; }
    }
    readonly GlyphTypeface glyphTypefaceBold;


    /// <summary>
    /// Screen resolution. 
    /// </summary>
    public float PixelsPerDip { get; }
    #endregion


    #region Consructor
    //      ----------

    readonly TextViewer textViewer;
    readonly Typeface typefaceNormal;
    readonly Typeface typefaceBold;


    /// <summary>
    /// Construct a GlyphTypeface with the specified font properties
    /// </summary>
    public TextViewerGlyphDrawer(TextViewer textViewer, FontFamily fontFamily, FontStyle fontStyle,
      FontStretch fontStretch, double pixelsPerDip) 
    {
      this.textViewer = textViewer;
      typefaceNormal = new Typeface(fontFamily, fontStyle, FontWeights.Normal, fontStretch);
      if (!typefaceNormal.TryGetGlyphTypeface(out glyphTypefaceNormal))
        throw new InvalidOperationException("No plain GlyphTypeface found");

      typefaceBold = new Typeface(fontFamily, fontStyle, FontWeights.Bold, fontStretch);
      if (!typefaceBold.TryGetGlyphTypeface(out glyphTypefaceBold))
        throw new InvalidOperationException("No plain GlyphTypeface found");

      PixelsPerDip = (float)pixelsPerDip;
    }
    #endregion


    #region Methods
    //      -------

    TextViewerAnchor? markAnchor;


    public void SetMarker(TextViewerAnchor? anchor) {
      markAnchor = anchor;
    }


    readonly List<ushort> glyphIndexes = new();
    readonly List<double> advanceWidths = new();


    /// <summary>
    /// Writes a string to a DrawingContext, using the GlyphTypeface stored in the GlyphDrawer.
    /// </summary>
    public void Write(DrawingContext drawingContext, Point origin, ReadOnlySpan<char> text, double fontSize, int line) {
      if (text.Length==0) return;

      var isUnFormatted = true;
      var isAnchor = false;
      var isLink = false;
      var isStream = false;
      var stringBuilder = new StringBuilder();
      var glyphTypeface = glyphTypefaceNormal;
      var brush = Brushes.Black;
      var isUnderline = false;
      glyphIndexes.Clear();
      advanceWidths.Clear();
      double totalWidth = 0;

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
            if (!getNext(out nextCodePoint, text, ref charIndex)) break;

            switch (nextCodePoint) {
            case StartFormat:
              //second '{' found, just write it
              break;

            case 'a':
              isUnFormatted = false;
              if (markAnchor is not null) {
                isAnchor = true;
                stringBuilder.Clear();
              }
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref totalWidth, ref origin, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Black;
              isUnderline = false;
              break;

            case 'b':
              isUnFormatted = false;
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref totalWidth, ref origin, drawingContext);
              glyphTypeface = glyphTypefaceNormal;
              brush = Brushes.Blue;
              isUnderline = false;
              break;

            case 'B':
              isUnFormatted = false;
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref totalWidth, ref origin, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Black;
              isUnderline = false;
              break;

            case 'l':
              isUnFormatted = false;
              isLink = true;
              stringBuilder.Clear();
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref totalWidth, ref origin, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Blue;
              isUnderline = true;
              break;

            case 's':
              isUnFormatted = false;
              isStream = true;
              stringBuilder.Clear();
              codePoint = int.MinValue;
              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref totalWidth, ref origin, drawingContext);
              glyphTypeface = glyphTypefaceBold;
              brush = Brushes.Green;
              isUnderline = true;
              break;

            default:
              //unknown formatting character found
              //System.Diagnostics.Debugger.Break();
              addGlyph(StartFormat, glyphTypeface, fontSize, ref totalWidth);
              addGlyph(nextCodePoint, glyphTypeface, fontSize, ref totalWidth);
              codePoint = ErrorChar;
              break;
            }

          } else if (codePoint==EndFormat) {
            if (!getNext(out nextCodePoint, text, ref charIndex)) break;

            if (nextCodePoint==EndFormat) {
              //double '}' found, draw only 1
            } else {
              //single '}' found, but there is no start format, just draw it and the following glyph.
              //if the nextCodePoint is a '{', it will not be used as start format. Easier code
              addGlyph(EndFormat, glyphTypeface, fontSize, ref totalWidth);
              codePoint = nextCodePoint;
            }
          }

        } else {
          //glyphs are formatted
          if (codePoint==StartFormat) {
            if (!getNext(out nextCodePoint, text, ref charIndex)) break;

            if (nextCodePoint==StartFormat) {
              //second '{' found, just write it

            } else {
              //single '{', draw it with next glyph
              //System.Diagnostics.Debugger.Break();
              addGlyph(StartFormat, glyphTypeface, fontSize, ref totalWidth);
              codePoint = nextCodePoint;
            }

          } else if (codePoint==EndFormat) {
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
                      new Rect(origin.X, origin.Y - fontSize + 3, totalWidth, fontSize));
                  }
                }

              } else if (isLink) {
                var objectIdString = stringBuilder.ToString();
                if (textViewer.Anchors.TryGetValue(objectIdString, out var anchor)) {
                  textViewer.TextViewerObjects.AddLink(anchor, line, origin.X, origin.X + totalWidth);
                } else {
                  System.Diagnostics.Debugger.Break();
                }

              } else if (isStream) {
                var objectIdString = stringBuilder.ToString();
                textViewer.TextViewerObjects.AddStream(new ObjectId(objectIdString), line, origin.X, origin.X + totalWidth);
              }

              if (!isNextCodePoint) {
                codePoint = int.MinValue;
                break;
              }

              drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref totalWidth, ref origin, drawingContext);
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
        addGlyph(codePoint, glyphTypeface, fontSize, ref totalWidth);
      }
      drawGlyphRun(glyphTypeface, brush, isUnderline, fontSize, ref totalWidth, ref origin, drawingContext);
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


    private void addGlyph(int codePoint, GlyphTypeface glyphTypeface, double size, ref double totalWidth) {
      if (codePoint>=0) {
        if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(codePoint, out var glyphIndex)) {
          glyphIndex = glyphTypeface.CharacterToGlyphMap[ErrorChar];
        };
        glyphIndexes.Add(glyphIndex);
        double width = glyphTypeface.AdvanceWidths[glyphIndex] * size;
        advanceWidths.Add(width);
        totalWidth += width;
      }
    }


    private void drawGlyphRun(
      GlyphTypeface glyphTypeface, 
      Brush brush, 
      bool isUnderline,
      double size, 
      ref double totalWidth, 
      ref Point origin, 
      DrawingContext drawingContext) 
    {
      if (glyphIndexes.Count==0) return;

      GlyphRun glyphRun = new GlyphRun(glyphTypeface, 0, false, size, PixelsPerDip, glyphIndexes.ToArray(), origin, 
        advanceWidths.ToArray(), null, null, null, null, null, null);
      drawingContext.DrawGlyphRun(brush, glyphRun);

      if (isUnderline) {
        double underlineHeight = glyphTypeface.UnderlineThickness * size * 2;//times 2 because it looks too thin
        var underlineY = origin.Y + underlineHeight;
        var guidelines = new GuidelineSet();
        guidelines.GuidelinesY.Add(underlineY);
        guidelines.GuidelinesY.Add(underlineY + underlineHeight);
        drawingContext.PushGuidelineSet(guidelines);
        try {
          drawingContext.DrawRectangle(brush, pen: null, new Rect(origin.X, underlineY, totalWidth, underlineHeight));
        } finally {
          drawingContext.Pop();
        }
      }
      glyphIndexes.Clear();
      advanceWidths.Clear();
      origin = new Point(origin.X + totalWidth, origin.Y);
      totalWidth = 0;
    }


    /// <summary>
    /// Returns the length of the text using the GlyphTypeface stored in the GlyphDrawer. 
    /// </summary>
    /// <param name="text"></param>
    /// <param name="size">same unit like FontSize: (em)</param>
    /// <returns></returns>
    public double GetLength(string text, double size, GlyphTypeface glyphTypeface) {
      double length = 0;

      for (int charIndex = 0; charIndex<text.Length; charIndex++) {
        ushort glyphIndex = glyphTypeface.CharacterToGlyphMap[text[charIndex]];
        double width = glyphTypeface.AdvanceWidths[glyphIndex] * size;
        length += width;
      }
      return length;
    }


    /// <summary>
    /// calculates the width of each string in strings and returns the longest length.
    /// </summary>
    public double GetMaxLength(IEnumerable<string> strings, double size, GlyphTypeface glyphTypeface) {
      var maxLength = 0.0;
      foreach (var text in strings) {
        double length = 0;
        for (int charIndex = 0; charIndex<text.Length; charIndex++) {
          ushort glyphIndex = glyphTypeface.CharacterToGlyphMap[text[charIndex]];
          double width = glyphTypeface.AdvanceWidths[glyphIndex] * size;
          length += width;
        }
        maxLength = Math.Max(maxLength, length);
      }
      return maxLength;
    }


    ///// <summary>
    ///// Returns width and height of text
    ///// </summary>
    //public Size MeasureString(string text, double size) {
    //  var formattedText = new FormattedText(text, CultureInfo.CurrentUICulture,
    //                                          FlowDirection.LeftToRight,
    //                                          typeface,
    //                                          size,
    //                                          Brushes.Black,
    //                                          PixelsPerDip);

    //  return new Size(formattedText.Width, formattedText.Height);
    //}
    #endregion
  }
}