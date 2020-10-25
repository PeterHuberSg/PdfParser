using System;
using System.Collections.Generic;
using System.Text;


namespace PdfParserLib {


  public class PdfToUnitCode {

    public IReadOnlyList<char> Unicodes => unicodes;
    readonly char[] unicodes;


    public readonly string Header;


  // /CIDInit /ProcSet findresource begin
  // 11 dict begin
  // begincmap
  // /CIDSystemInfo
  // << /Registry(Adobe)
  // /Ordering(UCS)
  // /Supplement 0
  // >> def
  // /CMapName /Adobe-Identity-UCS def
  // /CMapType 2 def
  // 1 begincodespacerange
  // <0000> <FFFF>
  // endcodespacerange
  // 2 beginbfchar
  // <0003> <0020>
  // <00B1> <2013>
  // endbfchar
  // 1 beginbfrange
  // <00B5> <00B6> <2018>
  // endbfrange
  // endcmap
  // CMapName currentdict /CMap defineresource pop
  // end
  // end

    public PdfToUnitCode(DictionaryToken toUnicodeStream) {
      var tokeniser = toUnicodeStream.GetStreamBytes();
      if (tokeniser.GetStreamOpCode("begincmap") is null) 
        throw tokeniser.StreamException("ToUnicode stream is missing 'begincmap'.");

      tokeniser.SetStreamMark();
      if (tokeniser.GetStreamOpCode("endcodespacerange") is null) 
        throw tokeniser.StreamException("ToUnicode stream is missing 'endcodespacerange'.");

      Header = tokeniser.GetStreamMarkedText();
      var cidUniCodes = new List<(ushort, char)>();
      var cidRangeUniCodes = new List<(ushort, ushort, char)>();
      var minCid =int.MaxValue;
      var maxCid =int.MinValue;
      while (true) {
        var opCode = tokeniser.GetStreamOpCode();
        if (opCode is null) throw tokeniser.StreamException("ToUnicode stream incomplete.");

        var opCodeSpan = opCode.Value.Span;
        if (isEqual("beginbfchar", opCodeSpan)) {
          tokeniser.StartStreamArgumentReading();
          var linesCount = tokeniser.GetStreamInt();
          tokeniser.EndStreamArgumentReading();
          for (int lineIndex = 0; lineIndex < linesCount; lineIndex++) {
            var cid = tokeniser.GetStreamCid();
            minCid = Math.Min(minCid, cid);
            maxCid = Math.Max(maxCid, cid);
            var unicodeChar = tokeniser.GetStreamUnicode();
            cidUniCodes.Add((cid, (char)unicodeChar));
          }
          opCode = tokeniser.GetStreamOpCode();
          opCodeSpan = opCode!.Value.Span;
          if (!isEqual("endbfchar", opCodeSpan)) 
            throw tokeniser.StreamException("ToUnicode stream is missing 'endbfchar' after 'beginbfchar'.");
        
        } else if (isEqual("beginbfrange", opCodeSpan)) {
          tokeniser.StartStreamArgumentReading();
          var linesCount = tokeniser.GetStreamInt();
          tokeniser.EndStreamArgumentReading();
          for (int lineIndex = 0; lineIndex < linesCount; lineIndex++) {
            var cidStart = tokeniser.GetStreamCid();
            minCid = Math.Min(minCid, cidStart);
            var cidEnd = tokeniser.GetStreamCid();
            maxCid = Math.Max(maxCid, cidEnd);
            if (cidEnd<cidStart)
              throw tokeniser.StreamException($"ToUnicode: beginbfrange cid1 '{cidStart} should be smaller than cid2'{cidEnd}'.");

            var unicodeChar = tokeniser.GetStreamUnicode();
            cidRangeUniCodes.Add((cidStart, cidEnd, (char)unicodeChar));
          }
          opCode = tokeniser.GetStreamOpCode();
          opCodeSpan = opCode!.Value.Span;
          if (!isEqual("endbfrange", opCodeSpan)) 
            throw tokeniser.StreamException("ToUnicode stream is missing 'endbfrange' after 'beginbfrange'.");
        
        } else if (isEqual("endcmap", opCodeSpan)) {
          break;
        }

      }

      unicodes = new char[maxCid + 1];
      for (int unicodesIndex = 0; unicodesIndex < unicodes.Length; unicodesIndex++) {
        unicodes[unicodesIndex] = (char)unicodesIndex;
      }
      foreach ((ushort cid, ushort unicodeChar) cidUniCode in cidUniCodes) {
        if (unicodes[cidUniCode.cid]!=cidUniCode.cid) 
          throw tokeniser.StreamException($"ToUnicode defines the same cid '{cidUniCode.cid}' twice.");

        unicodes[cidUniCode.cid] = (char)cidUniCode.unicodeChar;
      }
      foreach ((ushort cidStart, ushort cidEnd, ushort unicodeChar) cidRangeUniCode in cidRangeUniCodes) {
        var unicodeIndex = cidRangeUniCode.unicodeChar;
        for (ushort cidIndex = cidRangeUniCode.cidStart; cidIndex <= cidRangeUniCode.cidEnd; cidIndex++) {
          if (unicodes[cidIndex]!=cidIndex)
            throw tokeniser.StreamException($"ToUnicode defines the same cid '{cidIndex}' twice.");

          unicodes[cidIndex] = (char)unicodeIndex++;
        }
      }

      //var s = tokeniser.StreamBytesToString();
    }


    private bool isEqual(string expectedString, ReadOnlySpan<byte> opCodeSpan) {
      if (expectedString.Length!=opCodeSpan.Length) return false;

      for (int charIndex = 0; charIndex < expectedString.Length; charIndex++) {
        if (expectedString[charIndex]!=opCodeSpan[charIndex]) return false;

      }
      return true;
    }


    public char ToChar(ushort cid) {
      if (cid>=unicodes.Length) return (char)cid;

      return (char)unicodes[cid];
    }
  }
}
