using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PdfParserLib {

  /// <summary>
  /// Stores large text read from a pdf file as bytes in a reusable character array chars. LineStarts stores where each line 
  /// starts.<br/>
  /// Acording to the pdf specification, end of line is marked with a single CR, single LF or a CR LF pair. In chars, each EOl
  /// is stored as a CR.
  /// </summary>
  public class TextStore {

    #region Properties
    //      ----------

    public char[] Chars => chars;
    public int CharsCount => charsCount;
    public int LinesCount => linesCount;
    public int[] LineStarts;


    //public int[] LineStarts;


    public ReadOnlySpan<char> this[int index] {
      get {
        if (index<0) throw new ArgumentException($"Index '{index}' must be greater equal 0.");
        if (index>=LinesCount) throw new ArgumentException($"Index '{index}' must be smaller than LinesCount {LinesCount}.");

        //int endCharIndex;
        //var nextLineIndex = index+1;
        //if (nextLineIndex==LinesCount) {
        //  endCharIndex = charsCount;
        //} else {
        //  endCharIndex = LineStarts[nextLineIndex] ;
        //}

        //LineStarts[0] is always 0
        //LineStarts[charsCount] is always 0
        var startIndex = LineStarts[index];
        var endCharIndex = LineStarts[index+1];  
        if (startIndex==endCharIndex) {
          //very last line is empty, has not CR
          return new ReadOnlySpan<char>(chars, startIndex, 0);
        }
        endCharIndex--;//remove CR at end.
        return new ReadOnlySpan<char>(chars, startIndex, endCharIndex-startIndex); 
      }
    }
    #endregion


    #region Constructor
    //      -----------

    char[] chars;
    int charsCount;
    int linesCount;
    bool isNewLine = true;


    public TextStore(int size = 1000) {
      if (size<=0) throw new ArgumentException($"Size {size} must be greater 0'");

      chars = new char[size];
      LineStarts = new int[Math.Max(1, size/40)];
    }


    public void Reset() {
      charsCount = 0;
      linesCount = 0;
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
          if (linesCount>=LineStarts.Length) {
            Array.Resize(ref LineStarts, LineStarts.Length*2);
          }
          LineStarts[linesCount++] = charsCount;
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
      if (linesCount>=LineStarts.Length) {
        Array.Resize(ref LineStarts, LineStarts.Length*2);//only 1 more entry is needed, but since LineStarts get reused,
                                                          //it makes sense to assign more space
      }
      LineStarts[linesCount] = charsCount;//it makes reading chars easier if even chars[linesCount] has a value
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
    //  if (linesCount>=LineStarts.Length) {
    //    Array.Resize(ref LineStarts, LineStarts.Length*2);
    //  }
    //  LineStarts[linesCount++] = charsCount;
    //  charsCount = nextCharsCount;
    //}


    public TextStoreSelection SelectAll() {
      var lastLine = linesCount-1;
      return new TextStoreSelection(this, 0, 0, lastLine, charsCount - LineStarts[lastLine] - 1);
    }


    public TextStoreSelection? FindString(
      TextStoreSelection? previousSelection,
      string searchString,
      bool isForward,
      bool isIgnoreCase) 
    {
      var searchStringLength = searchString.Length;

      if (searchStringLength==0 || searchStringLength>charsCount) return null;

      int foundCharsIndex;
      var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
      ReadOnlySpan<char> charsSpan = chars.AsSpan()[0..charsCount];
      ReadOnlySpan<char> searchSpan = searchString;
      var compareOptions = isIgnoreCase ? CompareOptions.IgnoreCase : CompareOptions.None;

      int startSearchCharIndex;
      if (isForward) {
        if (previousSelection is null) {
          startSearchCharIndex = 0;
        } else {
          startSearchCharIndex = LineStarts[previousSelection.StartLine] + previousSelection.StartChar + 1;//start search on next position
          if (startSearchCharIndex>=charsCount - searchStringLength + 1) {
            startSearchCharIndex = 0;
          }
        }

        if (startSearchCharIndex>0) {
          //start search from startSearchPosition within chars
          foundCharsIndex = compareInfo.IndexOf(charsSpan[startSearchCharIndex..], searchString, compareOptions) + startSearchCharIndex;
          if (foundCharsIndex<startSearchCharIndex) {
            //continue search from beginning
            foundCharsIndex = compareInfo.IndexOf(charsSpan[..(startSearchCharIndex+1)], searchString, compareOptions);
          }
        } else {
          //start search at beginning of chars
          foundCharsIndex = compareInfo.IndexOf(charsSpan, searchString, compareOptions);
        }
        if (foundCharsIndex<0) return null;

      } else {
        //search backwards
        if (previousSelection is null) {
          startSearchCharIndex = charsCount;
        } else {
          //start search on previous position. Note that -1 is not needed, because LastIndexOf needs length (i.e. startSearchCharIndex + 1)
          startSearchCharIndex = LineStarts[previousSelection.StartLine] + previousSelection.StartChar;
          if (startSearchCharIndex<searchStringLength) {
            startSearchCharIndex = charsCount;
          }
        }

        if (startSearchCharIndex<charsCount) {
          //start search backwards from startSearchPosition within chars
          var r = charsSpan[00..startSearchCharIndex].ToString();
          foundCharsIndex = compareInfo.LastIndexOf(charsSpan[00..startSearchCharIndex], searchString, compareOptions);
          if (foundCharsIndex<0) {
            //continue search from end
            foundCharsIndex = compareInfo.LastIndexOf(charsSpan[startSearchCharIndex..], searchString, compareOptions);
            if (foundCharsIndex<0) return null;

            foundCharsIndex += startSearchCharIndex;
          }
        } else {
          //start backwards search at end of chars
          foundCharsIndex = compareInfo.LastIndexOf(charsSpan, searchString, compareOptions);
          if (foundCharsIndex<0) return null;
        }
      }

      //find line and character position in that line where searchString was found
      var foundStartLine = linesCount / 2;
      var minLineIndex = 0;
      var maxLineIndex = linesCount;
      while (true) {
        if (LineStarts[foundStartLine]>foundCharsIndex) {
          maxLineIndex = foundStartLine;
        } else if (LineStarts[foundStartLine+1]<=foundCharsIndex) {
          minLineIndex = foundStartLine;
        } else {
          //line found where search string starts
          break;
        }
        foundStartLine = (maxLineIndex + minLineIndex) / 2;
      }
      int foundStartChar = foundCharsIndex - LineStarts[foundStartLine];

      //find line and character position in the line where searchString ends
      var lastFoundEndCharsIndex = foundCharsIndex + searchStringLength - 1;
      var foundEndLine = foundStartLine + 1;
      while (LineStarts[foundEndLine]<=lastFoundEndCharsIndex) {
        foundEndLine++;
      }
      foundEndLine--;
      var foundEndChar = lastFoundEndCharsIndex - LineStarts[foundEndLine];
      return new TextStoreSelection(this, foundStartLine, foundStartChar, foundEndLine, foundEndChar);
    }


    //public (int startLine, int startChar, int endLine, int endChar)? FindLocation(
    //  int startSearchLine, 
    //  int startSearchCharPos, 
    //  string searchString, 
    //  bool isForward, 
    //  bool isIgnoreCase) 
    //{
    //  var searchStringLength = searchString.Length;

    //  if (searchStringLength<=0 || searchStringLength>charsCount) return null;

    //  int startSearchCharIndex;
    //  if (startSearchLine<0 || startSearchCharPos<0 || startSearchLine>=LinesCount) {
    //    startSearchCharIndex = 0;
    //  } else {
    //    startSearchCharIndex =LineStarts[startSearchLine] + startSearchCharPos + 1;//start search on next position
    //    if (startSearchCharIndex>=charsCount) {
    //      startSearchCharIndex = 0;
    //    }
    //  }

    //  var endOfCheckingCharsIndex = charsCount - searchStringLength + 1;
    //  ////add one more "line" with max value
    //  ////this makes it unnecessary to test if serachLineIndex+1 is smaller linesCount when accessing LineStarts[serachLineIndex+1]
    //  //if (linesCount>=LineStarts.Length) {
    //  //  Array.Resize(ref LineStarts, LineStarts.Length*2);
    //  //}
    //  //LineStarts[linesCount] = charsCount; // points 1 position after last valid character
    //  int foundCharsIndex;
    //  var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
    //  ReadOnlySpan<char> charsSpan = chars.AsSpan()[0..charsCount];
    //  ReadOnlySpan<char> searchSpan = searchString;
    //  var compareOptions = isIgnoreCase ? CompareOptions.IgnoreCase : CompareOptions.None;

    //  if (startSearchCharIndex>0 && startSearchCharIndex<endOfCheckingCharsIndex) {
    //    //continue search from last position found
    //    foundCharsIndex = compareInfo.IndexOf(charsSpan[startSearchCharIndex..], searchString, compareOptions) + startSearchCharIndex;
    //    if (foundCharsIndex<startSearchCharIndex) {
    //      //continue search from beginning
    //      foundCharsIndex = compareInfo.IndexOf(charsSpan[..startSearchCharIndex], searchString, compareOptions);
    //    }
    //  } else {
    //    //new search, start search at beginning of chars
    //    foundCharsIndex = compareInfo.IndexOf(charsSpan, searchString, compareOptions);
    //  }
    //  if (foundCharsIndex<0) return null;

    //  //find line and character position in that line where searchString was found
    //  var foundStartLine = linesCount / 2;
    //  var minLineIndex = 0;
    //  var maxLineIndex = linesCount;
    //  while (true) {
    //    if (LineStarts[foundStartLine]>foundCharsIndex) {
    //      maxLineIndex = foundStartLine;
    //    } else if (LineStarts[foundStartLine+1]<=foundCharsIndex) {
    //      minLineIndex = foundStartLine;
    //    } else {
    //      //line found where search string starts
    //      break;
    //    }
    //    foundStartLine = (maxLineIndex + minLineIndex) / 2;
    //  }
    //  int foundStartChar = foundCharsIndex - LineStarts[foundStartLine];

    //  //find line and character position in the line where searchString ends
    //  var lastFoundEndCharsIndex = foundCharsIndex + searchStringLength - 1;
    //  var foundEndLine = foundStartLine + 1;
    //  while (LineStarts[foundEndLine]<=lastFoundEndCharsIndex) {
    //    foundEndLine++;
    //  }
    //  foundEndLine--;
    //  var foundEndChar = lastFoundEndCharsIndex - LineStarts[foundEndLine];
    //  return (foundStartLine, foundStartChar, foundEndLine, foundEndChar);
    //}


    public string GetChar(int charsIndex) {
      var c = Chars[charsIndex];
      return c=='\r' ? "cr" : c.ToString();
    }


    public string GetString(int startLine, int startChar, int endChar) {
      if (startLine<0 || startLine>=linesCount) return "";

      var charsIndex = LineStarts[startLine] + startChar;
      var endCharsIndex = LineStarts[startLine] + endChar;
      endCharsIndex = Math.Min(endCharsIndex, LineStarts[startLine+1]-1);
      if (charsIndex>=endCharsIndex || endCharsIndex>=charsCount) return "";

      return chars.AsSpan()[charsIndex..endCharsIndex].ToString();
    }


    public string ToString(int startLine, int endLine) {
      if (startLine<0 || startLine>endLine || endLine>linesCount) {
        throw new ArgumentException();

      }
      var stringBuilder = new StringBuilder();
      for (int lineIndex = startLine; lineIndex<endLine; lineIndex++) {
        var startChar = LineStarts[lineIndex];
        var nextLine = lineIndex + 1;
        //var endChar = nextLine<linesCount ? LineStarts[nextLine] : charsCount;
        var endChar = LineStarts[nextLine];
        stringBuilder.Append(chars.AsSpan(startChar, endChar-startChar));
        stringBuilder.AppendLine();
      }
      return stringBuilder.ToString();
    }


    public override string ToString() {
      var sb = new StringBuilder();
      for (int charIndex = 0; charIndex < Math.Min(charsCount, 40); charIndex++) {
        var c = chars[charIndex];
        if (c=='\r') {
          sb.Append("\\r");
        } else {
          sb.Append(c);
        }
      }
      return $"LinesCount: {linesCount}; CharsCount: {CharsCount}; '{sb}'";
    }
    #endregion
  }
}
