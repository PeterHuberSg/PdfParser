using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PdfParserLib {

  /// <summary>
  /// Defines a string within TextStore. It might include formatting characters
  /// </summary>
  public class TextStoreSelection {
    public readonly TextStore TextStore;
    public readonly int StartLine;
    public readonly int StartChar;
    public readonly int EndLine;
    public readonly int EndChar;


    public TextStoreSelection(TextStore textStore, int startLine, int startChar, int endLine, int endChar) {
      if (startLine<0 || startChar<0 || endLine<0 || endChar<0 ||
        startLine>endLine || (startLine==endLine && StartChar>endChar) ||
        endLine>=textStore.LinesCount || textStore.LineStarts[endLine] + endChar>=textStore.CharsCount)
      {
        throw new ArgumentException();
      }

      TextStore = textStore;
      StartLine = startLine;
      StartChar = startChar;
      EndLine = endLine;
      EndChar = endChar;
    }


    public ReadOnlyMemory<char> GetAllCharacters() {
      return TextStore.Chars.AsMemory()[(TextStore.LineStarts[StartLine] + StartChar)..(TextStore.LineStarts[EndLine] + EndChar + 1)];
    }


    public string GetContent() {
      ReadOnlySpan<char> charsSpan = TextStore.Chars.AsSpan()[(TextStore.LineStarts[StartLine] + StartChar)..(TextStore.LineStarts[EndLine] + EndChar + 1)];
      var sb = new StringBuilder();
      var copyFromIndex = 0;
      var isOpenBracketFound0 = false;
      var isOpenBracketFound1 = false;
      var isCloseBracketFound = false;
      var charIndex = 0;
      for (; charIndex<charsSpan.Length; charIndex++) {
        if (isOpenBracketFound0) {
          //skip one character after opening bracket
          isOpenBracketFound0 = false;
          isOpenBracketFound1 = true;
          continue;
        }

        if (isOpenBracketFound1) {
          isOpenBracketFound1 = false;
          copyFromIndex = charIndex;
        } else if (isCloseBracketFound) {
          isCloseBracketFound = false;
          copyFromIndex = charIndex;
        } else {
          var c = charsSpan[charIndex];
          if (c=='{') {
            isOpenBracketFound0 = true;
            sb.Append(charsSpan[copyFromIndex..charIndex]); //copy charactes up to but without bracket
          } else if (c=='}') {
            isCloseBracketFound = true;
            sb.Append(charsSpan[copyFromIndex..charIndex]); //copy charactes up to but without bracket
          }
        }
      }
      sb.Append(charsSpan[copyFromIndex..charIndex]); //copy remaining charactes
      return sb.ToString();
    }


    public override string ToString() {
      var selectionString = GetAllCharacters().ToString();
      if (selectionString.Length>40) {
        selectionString = selectionString[..40];
      }
      return $"StartLine: {StartLine}; StartChar: {StartChar}; EndLine: {EndLine}; EndChar: {EndChar}; '" + selectionString + "'";
    }
  }
}
