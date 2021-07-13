using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PdfParserLib {

  /// <summary>
  /// Stores large text read from a pdf file as bytes in a reusable character array chars. lineStarts stores where each line 
  /// starts.<br/>
  /// Acording to the pdf specification, end of line is marked with a single CR, single LF or a CR LF pair. In chars, each EOl
  /// is stored as a CR.
  /// </summary>
  public class TextStore {

    #region Properties
    //      ----------

    public int LinesCount {
      get { return linesCount; }
    }


    public ReadOnlySpan<char> this[int index] {
      get {
        if (index<0) throw new ArgumentException($"Index '{index}' must be greater equal 0.");
        if (index>=LinesCount) throw new ArgumentException($"Index '{index}' must be smaller than LinesCount {LinesCount}.");

        //int endCharIndex;
        //var nextLineIndex = index+1;
        //if (nextLineIndex==LinesCount) {
        //  endCharIndex = charsCount;
        //} else {
        //  endCharIndex = lineStarts[nextLineIndex] ;
        //}

        //lineStarts[0] is always 0
        //lineStarts[charsCount] is always 0
        var startIndex = lineStarts[index];
        var endCharIndex = lineStarts[index+1] - 1;//remove CR at end.  
        return new ReadOnlySpan<char>(chars, startIndex, endCharIndex-startIndex); 
      }
    }
    #endregion


    #region Constructor
    //      -----------

    char[] chars;
    int charsCount;
    int[] lineStarts;
    int linesCount;
    string? foundString;
    int lastFoundStartCharsIndex;
    int lastFoundEndCharsIndex;
    bool isNewLine = true;


    public TextStore(int size = 1000) {
      if (size<=0) throw new ArgumentException($"Size {size} must be greater 0'");

      chars = new char[size];
      lineStarts = new int[Math.Max(1, size/40)];
    }


    public void Reset() {
      charsCount = 0;
      linesCount = 0;
      lastFoundStartCharsIndex = 0;
      lastFoundEndCharsIndex = 0;
      isNewLine = true;
    }
    #endregion


    #region Methods
    //      -------

    public void Append(ReadOnlySpan<byte> pdfBytes) {
      var isCarriageReturn = false;
      foreach (var pdfByte in pdfBytes) {
        if (isNewLine) {
          isNewLine = false;
          if (linesCount>=lineStarts.Length) {
            Array.Resize(ref lineStarts, lineStarts.Length*2);
          }
          lineStarts[linesCount++] = charsCount;
        }

        if (charsCount+10>chars.Length) {
          //ensure there is plenty of space for 1 more character, which might need several chars
          Array.Resize(ref chars, chars.Length*2);
        }

        //handle end of line
        if (isCarriageReturn && pdfByte==0xa) {
          //skip linefeed after carriage return
          isCarriageReturn = false;
          continue;
        }

        isCarriageReturn = pdfByte==0xd;
        if (isCarriageReturn || pdfByte==0xa) {
          //end of line found
          chars[charsCount++] = '\r';//add carriage return to mark end of line, which helps when searching for multiple lines
          isNewLine = true;
          continue;
        }

        //handle other characters
        var c = PdfEncodings.PdfEncoding[pdfByte];
        if (c==0xFFFF) {
          chars[charsCount++] = '\'';
          foreach (var c1 in pdfByte.ToString("x")) {
            chars[charsCount++] = c1;
          }
          chars[charsCount++] = '\'';
        } else {
          chars[charsCount++] = c;
        }
      }
      if (linesCount>=lineStarts.Length) {
        Array.Resize(ref lineStarts, lineStarts.Length*2);//only 1 more entry is needed, but since lineStarts get reused,
                                                          //it makes sense to assign more space
      }
      lineStarts[linesCount] = charsCount;//it makes reading chars easier if even chars[linesCount] has a value
    }


    //public void Add(ReadOnlySpan<char> source) {
    //  var nextCharsCount = charsCount + source.Length;
    //  while (nextCharsCount>chars.Length) {
    //    Array.Resize(ref chars, chars.Length*2);
    //  }
    //  var destination = new Span<char>(chars, charsCount, source.Length);
    //  source.CopyTo(destination);
    //  charsCount = nextCharsCount;
    //}


    //public void AddLine(ReadOnlySpan<char> source) {
    //  var nextCharsCount = charsCount + source.Length;
    //  while (nextCharsCount>chars.Length) {
    //    Array.Resize(ref chars, chars.Length*2);
    //  }
    //  var destination = new Span<char>(chars, charsCount, source.Length);
    //  source.CopyTo(destination);
    //  if (linesCount>=lineStarts.Length) {
    //    Array.Resize(ref lineStarts, lineStarts.Length*2);
    //  }
    //  lineStarts[linesCount++] = charsCount;
    //  charsCount = nextCharsCount;
    //}


    public (int startLine, int startChar, int endLine, int endChar)? FindLocation(
      string searchString, bool isForward, bool isIgnoreCase) 
    {
      //no longer needed, since chars now stores EOL as CR
      ////remove carriage returns and line feeds from searchString
      ////var stringBuilder = new StringBuilder();
      ////foreach (var searchCharacter in searchString) {
      ////  if (searchCharacter!='\r' && searchCharacter!='\n') {
      ////    stringBuilder.Append(searchCharacter);
      ////  }
      ////}
      ////searchString = stringBuilder.ToString();
      ////searchString = searchString.Replace("\r", "");
      ////searchString = searchString.Replace("\n", "");

      var searchStringLength = searchString.Length;
      if (searchStringLength<=0) throw new ArgumentException();

      if (searchStringLength>charsCount) return null;

      if (foundString!=searchString) {
        lastFoundEndCharsIndex = -1;
        //resetting lastFoundStartCharsIndex is not needed
        foundString = null;
      }
      lastFoundEndCharsIndex++; //search needs to start 1 character after last search character found
      var endOfCheckingCharsIndex = charsCount - searchStringLength + 1;
      //add one more "line" with max value
      //this makes it unnecessary to test if serachLineIndex+1 is smaller linesCount when accessing lineStarts[serachLineIndex+1]
      if (linesCount>=lineStarts.Length) {
        Array.Resize(ref lineStarts, lineStarts.Length*2);
      }
      lineStarts[linesCount] = int.MaxValue;
      int foundCharsIndex;
      var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
      ReadOnlySpan<char> charsSpan = chars.AsSpan()[0..charsCount];
      ReadOnlySpan<char> searchSpan = searchString;
      var compareOptions = isIgnoreCase ? CompareOptions.IgnoreCase : CompareOptions.None;

      //find location of searchString in chars
      //if (isIgnoreCase) {
      //  //unfortunately, there is not Span.IndexOf() for ignore case, so it has to be programmed here :-(
      //  //is slightly different from toLowerAsciiInvariant(). ToLowerInvariant is maybe faster because of unsafe code, maybe slower because
      //  //it tries to cover Unicode properly ?
      //  searchString = searchString.ToLowerInvariant();
      //  if (lastFoundEndCharsIndex>0 && lastFoundEndCharsIndex<lastCharsIndexToCheck) {
      //    //continue search from last position found
      //    foundCharsIndex = indexOfIgnoreCase(chars.AsSpan()[lastFoundEndCharsIndex..charsCount], searchString); 
      //    if (foundCharsIndex<0) {
      //      //continue search from beginning
      //      foundCharsIndex = indexOfIgnoreCase(chars.AsSpan()[0..lastFoundStartCharsIndex], searchString);
      //    }
      //  } else {
      //    //new search, start search at beginning of chars and search over all chars in one go
      //    foundCharsIndex = indexOfIgnoreCase(chars.AsSpan()[0..charsCount], searchString);
      //  }

      //} else {
      //  //search for exact casing match
      //  if (lastFoundEndCharsIndex>1 && lastFoundEndCharsIndex<lastCharsIndexToCheck) {
      //    //continue search from last position found
      //    foundCharsIndex = chars.AsSpan()[lastFoundEndCharsIndex..charsCount].IndexOf(searchString);
      //    if (foundCharsIndex<0) {
      //      //continue search from beginning
      //      foundCharsIndex = chars.AsSpan()[0..lastFoundStartCharsIndex].IndexOf(searchString);
      //    }
      //  } else {
      //    //new search, start search at beginning of chars
      //    foundCharsIndex = chars.AsSpan()[0..charsCount].IndexOf(searchString);
      //  }
      //}

      if (lastFoundEndCharsIndex>0 && lastFoundEndCharsIndex<endOfCheckingCharsIndex) {
        //continue search from last position found
        foundCharsIndex = compareInfo.IndexOf(charsSpan[lastFoundEndCharsIndex..], searchString, compareOptions) + lastFoundEndCharsIndex;
        //foundCharsIndex = chars.AsSpan()[lastFoundEndCharsIndex..charsCount].IndexOf(searchString);
        if (foundCharsIndex<lastFoundEndCharsIndex) {
          //continue search from beginning
          foundCharsIndex = compareInfo.IndexOf(charsSpan[..lastFoundEndCharsIndex], searchString, compareOptions);
          //foundCharsIndex = chars.AsSpan()[0..lastFoundStartCharsIndex].IndexOf(searchString);
        }
      } else {
        //new search, start search at beginning of chars
        foundCharsIndex = compareInfo.IndexOf(charsSpan, searchString, compareOptions);
        //foundCharsIndex = chars.AsSpan()[0..charsCount].IndexOf(searchString);
      }
      if (foundCharsIndex<0) return null;

      //find line and character position in that line where searchString was found
      lastFoundStartCharsIndex = foundCharsIndex;
      var foundStartLine = linesCount / 2;
      var minLineIndex = 0;
      var maxLineIndex = linesCount;
      while (true) {
        if (lineStarts[foundStartLine]>foundCharsIndex) {
          maxLineIndex = foundStartLine;
        } else if (lineStarts[foundStartLine+1]<=foundCharsIndex) {
          minLineIndex = foundStartLine;
        } else {
          //line found where search string starts
          break;
        }
        foundStartLine = (maxLineIndex + minLineIndex) / 2;
      }
      int foundStartChar = foundCharsIndex - lineStarts[foundStartLine];

      //find line and character position in the line where searchString ends
      lastFoundEndCharsIndex = foundCharsIndex + searchStringLength - 1;
      var foundEndLine = foundStartLine + 1;
      while (lineStarts[foundEndLine]<=lastFoundEndCharsIndex) {
        foundEndLine++;
      }
      foundEndLine--;
      var foundEndChar = lastFoundEndCharsIndex - lineStarts[foundEndLine];
      foundString = searchString;
      return (foundStartLine, foundStartChar, foundEndLine, foundEndChar);
    }


    ///// <summary>
    ///// Search first occurance of searchString in charsSpan, comparing both as lower case characters. searchString must be
    ///// already in lower cases.
    ///// </summary>
    //private int indexOfIgnoreCase(ReadOnlySpan<char> charsSpan, string searchString) {
    //  int searchStringIndex;
    //  char firstSearchChar = searchString[0];
    //  var searchStringLength = searchString.Length;
    //  var lastCharsIndexToCheck = charsSpan.Length - searchStringLength;
    //  for (var charsIndex = 0; charsIndex <= lastCharsIndexToCheck; charsIndex++) {
    //    var charsChar = toLowerAsciiInvariant(chars[charsIndex]);
    //    if (charsChar != firstSearchChar) {
    //      continue;
    //    }

    //    for (searchStringIndex = 1; searchStringIndex < searchStringLength; searchStringIndex++) {
    //      charsChar = toLowerAsciiInvariant(chars[charsIndex + searchStringIndex]);
    //      var searchChar = searchString[searchStringIndex];
    //      if (charsChar!=searchChar) break;

    //    }
    //    if (searchStringIndex==searchStringLength) {
    //      return charsIndex;
    //    }
    //  }
    //  return -1;
    //}


    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //private static char toLowerAsciiInvariant(char c) {
    //  //based on https://source.dot.net/#System.Private.CoreLib/TextInfo.cs,145c4c32b3b4bace
    //  if (c>='A' && c<='Z') {
    //    // on x86, extending BYTE -> DWORD is more efficient than WORD -> DWORD
    //    c = (char)(byte)(c | 0x20);
    //  }
    //  return c;
    //}



    public string ToString(int startLine, int endLine) {
      if (startLine<0 || startLine>endLine || endLine>linesCount) {
        throw new ArgumentException();

      }
      var stringBuilder = new StringBuilder();
      for (int lineIndex = startLine; lineIndex<endLine; lineIndex++) {
        var startChar = lineStarts[lineIndex];
        var nextLine = lineIndex + 1;
        //var endChar = nextLine<linesCount ? lineStarts[nextLine] : charsCount;
        var endChar = lineStarts[nextLine];
        stringBuilder.Append(chars.AsSpan(startChar, endChar-startChar));
        stringBuilder.AppendLine();
      }
      return stringBuilder.ToString();
    }
    #endregion
  }
}
