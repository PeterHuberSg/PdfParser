using System;
using System.Collections.Generic;
using System.Text;

namespace PdfParserLib {


  public class PdfPage {


    public IReadOnlyDictionary<string, PdfFont> Fonts => fonts;
    readonly Dictionary<string, PdfFont> fonts = new Dictionary<string, PdfFont>();


    public IReadOnlyList<PdfContent> Contents => contents;
    readonly List<PdfContent> contents = new List<PdfContent>();


    public readonly string? Exception;


    public PdfPage(Tokeniser tokeniser, DictionaryToken pageToken) {
      pageToken.PdfObject = this;
      try {
        if (pageToken.TryGetDictionary("Resources", out var resourcesDictionaryToken)) {
          if (resourcesDictionaryToken.TryGetDictionary("Font", out var fontsDictionaryToken)) {
            foreach (var fontName_Token in fontsDictionaryToken) {
              if (fontName_Token.Value.PdfObject!=null) {
                var pdfFont = (PdfFont) fontName_Token.Value.PdfObject;
                if (pdfFont.Name!=fontName_Token.Key) {
                  throw new Exception($"PDF Font {fontName_Token.Key} exists already, but has name {pdfFont.Name}.");
                }
                fonts.Add(fontName_Token.Key, pdfFont);
              } else {
                fonts.Add(fontName_Token.Key, new PdfFont(fontName_Token.Key, fontName_Token.Value));
              }
            }
          }
        }

        if (pageToken.TryGetValue("Contents", out var contentsToken)) {
          if (contentsToken is ArrayToken contentsArrayToken) {
            foreach (var contentToken in contentsArrayToken) {
              contents.Add(new PdfContent((DictionaryToken)contentToken, Fonts));
            }
          } else if (contentsToken is DictionaryToken contentsDictionaryToken) {
            contents.Add(new PdfContent(contentsDictionaryToken, Fonts));
          } else {
            throw new NotSupportedException();
          }
        }

      } catch (Exception ex) {
        if (Exception is null) {
          Exception = "";
        } else {
          Exception += Environment.NewLine + Environment.NewLine;
        }
        if (ex is PdfStreamException || ex is PdfException) {
          Exception = ex.ToDetailString();
        } else {
          Exception = ex.ToDetailString() + Environment.NewLine + tokeniser.ShowStreamContentAtIndex();
        }
      }
    }
  }
}
