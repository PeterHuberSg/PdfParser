using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;


namespace PdfParserLib {


  public class PdfFont {

    public readonly string Name;
    public readonly PdfFontTypeEnum FontType;
    public readonly string? BaseFont;
    public readonly string? EncodingName;
    public readonly bool IsIdentity;
    public readonly char[]? Encoding8Bit;
    public readonly string? ToUnicodeHeader;
    public readonly Token? FontDescriptor;
    public readonly SortedDictionary<int, char>? CMap;


    public readonly string? Exception;


    bool hasFoundMissingChar;


    public PdfFont(string name, Token token) {
      Name = name;
      token.PdfObject = this;
      try {
        var fontDictionaryToken = (DictionaryToken)token;
        if (fontDictionaryToken.TryGetName("Subtype", out var subtype)) {
          FontType = subtype.ToPdfFontTypeEnum();
          if (FontType==PdfFontTypeEnum.Type1) {
            Encoding8Bit = PdfEncodings.Standard.ToArray();
          }
        }
        fontDictionaryToken.TryGetName("BaseFont", out BaseFont);

        if (fontDictionaryToken.TryGetValue("Encoding", out var encodingToken)) {
          if (encodingToken is DictionaryToken encodingDictionaryToken) {
            if (encodingDictionaryToken.TryGetArray("Differences", out var differencesArrayToken)) {
              var charIndex = int.MinValue;
              foreach (var differenceToken in differencesArrayToken) {
                if (differenceToken is NumberToken differenceNumberToken) {
                  charIndex = differenceNumberToken.Integer!.Value;
                } else if (differenceToken is NameToken differenceNameToken) {
                  var charName = differenceNameToken.Value;
                  if (charName.StartsWith("uni", StringComparison.OrdinalIgnoreCase)) {
                    try {
                      var ch = (char)Convert.ToInt32(charName[3..], 16);
                      Encoding8Bit![charIndex++] = ch;
                    } catch (Exception ex) {
                      System.Diagnostics.Debugger.Break();
                      throw ex;
                    }
                  } else {
                    //Encoding8Bit![charIndex++] = PdfEncodings.Chars[charName];
                    try {
                      Encoding8Bit![charIndex++] = PdfEncodings.Chars[charName];
                    } catch (Exception) {
                      System.Diagnostics.Debug.WriteLine(charName);
                      charIndex++;
                      if (!hasFoundMissingChar) {
                        hasFoundMissingChar = true;
                        System.Diagnostics.Debugger.Break();
                      }
                    }
                  }
                } else {
                  System.Diagnostics.Debugger.Break();
                }
              }
            } else {
              System.Diagnostics.Debugger.Break();
            }

          } else if (encodingToken is NameToken encodingNameToken) {
            var EncodingName = encodingNameToken.Value;
            if (EncodingName.Contains("Identity")) {
              //nothing to do, no encoding change needed
              IsIdentity = true;
            } else {
              Encoding8Bit = PdfEncodings.GetEncoding8Bit(EncodingName);
            }
          } else {
            System.Diagnostics.Debugger.Break();
          }
        }

        fontDictionaryToken.TryGetValue("FontDescriptor", out FontDescriptor);
        if (fontDictionaryToken.TryGetDictionary("ToUnicode", out var toUnicodeStream)) {
          if (Encoding8Bit is null) {
            Encoding8Bit = PdfEncodings.Standard.ToArray();
          }
          PdfEncodings.ApplyToUnitCode(toUnicodeStream, ref Encoding8Bit, out ToUnicodeHeader, out CMap);
        } else if (fontDictionaryToken.TryGetValue("ToUnicode", out var toUnicodeToken)) {
          System.Diagnostics.Debugger.Break();
        }
      } catch (Exception ex) {

        Exception += ex.ToDetailString() + Environment.NewLine;
      }
    }


    public override string ToString() {
      var returnString = $"Font Name: {Name}: FontType: {FontType}; Encoding: {EncodingName}; BaseFont: {BaseFont};";
      if (Exception is null) {
        return returnString;
      }
      return returnString + Environment.NewLine + "Exception: " + Exception;
    }
  }
}
