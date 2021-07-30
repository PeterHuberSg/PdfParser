using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfParserLib;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfParserTest {


  [TestClass]
  public class TextStoreTest {

    #region Test TextStore
    //      --------------

    [TestMethod]
    public void TestTextStore() {
      List<string> expectedStrings = new List<string>();
      TextStore textStore = new TextStore(10);
      assertText(textStore, expectedStrings);
      add("", textStore, expectedStrings);
      add("a", textStore, expectedStrings);
      add("bc de", textStore, expectedStrings);
      add(new string(' ', 122), textStore, expectedStrings);

      //test all characters defined in PdfEncodings.PdfEncoding
      textStore.Reset();
      var bytes = new byte[PdfEncodings.PdfEncoding.Length];
      for (int bytesIndex = 0; bytesIndex < bytes.Length; bytesIndex++) {
        bytes[bytesIndex] = (byte)bytesIndex;
      }
      textStore.Append(bytes);
      Assert.AreEqual(3, textStore.LinesCount);
      textStore.Append(new byte[] { 0xA });
      Assert.AreEqual(3, textStore.LinesCount);
    }


    private void add(string text, TextStore textStore, List<string> expectedStrings) {
      var pdfBytes = new byte[text.Length+1];
      for (int textIndex = 0; textIndex < text.Length; textIndex++) {
        pdfBytes[textIndex] = (byte)text[textIndex];
      }
      pdfBytes[text.Length] = (byte)'\r';

      ReadOnlySpan<byte> readOnlySpan = pdfBytes;
      textStore.Append(readOnlySpan);
      expectedStrings.Add(text);
      assertText(textStore, expectedStrings);
    }


    private void assertText(TextStore textStore, List<string> expectedStrings) {
      Assert.AreEqual(expectedStrings.Count, textStore.LinesCount);
      for (int lineIndex = 0; lineIndex < textStore.LinesCount; lineIndex++) {
        Assert.AreEqual(expectedStrings[lineIndex], textStore[lineIndex].ToString());
      }
    }
    #endregion


    #region Test FindLocation
    //      -----------------

    const bool isForward = true;
    const bool isBckward = false;
    const bool isIgnoreCase = true;
    const bool isNotIgnoreC = false;


    [TestMethod]
    public void TestTextStoreFindLocation() {
      //var textStore1 = new TextStore();
      //textStore1.Append(new byte[] {(byte)'A', (byte)'B', (byte)'C', (byte)'Ä', (byte)Environment.NewLine[0], (byte)Environment.NewLine[1] });
      //var v = textStore1.FindLocation("ABCÄ\r", isForward, isNotIgnoreC);

      //simple 1 character tests
      TextStore textStore = new TextStore(1);
      Assert.IsNull(textStore.FindString(null, " ", isForward, isIgnoreCase));
      append(textStore, 'a');
      assertIsFound("a", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("a", isBckward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("a", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("A", isBckward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "A", isBckward, isNotIgnoreC));

      append(textStore, 'ä');
      assertIsFound("ä", isForward, isIgnoreCase, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("ä", isForward, isNotIgnoreC, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("Ä", isForward, isIgnoreCase, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("ä", isBckward, isIgnoreCase, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("ä", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("Ä", isBckward, isIgnoreCase, textStore, -1, -1, 0, 1, 0, 1);
      Assert.IsNull(textStore.FindString(null, "Ä", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "Ä", isBckward, isNotIgnoreC));

      append(textStore, 'a');
      assertIsFound("a", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore,  0,  0, 0, 2, 0, 2);
      assertIsFound("A", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("a", isBckward, isIgnoreCase, textStore, -1, -1, 0, 2, 0, 2);
      assertIsFound("a", isBckward, isNotIgnoreC, textStore,  0,  2, 0, 0, 0, 0);
      assertIsFound("A", isBckward, isIgnoreCase, textStore, -1, -1, 0, 2, 0, 2);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "A", isBckward, isNotIgnoreC));
      assertIsFound("ä", isForward, isIgnoreCase, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("ä", isForward, isNotIgnoreC, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("Ä", isForward, isIgnoreCase, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("ä", isBckward, isIgnoreCase, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("ä", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 1, 0, 1);
      assertIsFound("Ä", isBckward, isIgnoreCase, textStore, -1, -1, 0, 1, 0, 1);
      Assert.IsNull(textStore.FindString(null, "Ä", isForward, isNotIgnoreC));

      //tests without repeating characters
      assertTextStore("A");
      assertTextStore("AB");
      assertTextStore("ABC");
      assertTextStore("ABCÄ");
      assertTextStore("ABCÄ\r");
      assertTextStore("ABCÄ\rD");
      assertTextStore("ABCÄ\rDE");

      //lines test
      textStore.Reset();
      append(textStore, new byte[] { (byte)'\r' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 0, 0);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "A", isBckward, isNotIgnoreC));

      append(textStore, new byte[] { (byte)'\r' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore,  0,  0, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, -1, -1, 1, 0, 1, 0);
      assertIsFound("\r", isBckward, isNotIgnoreC, textStore, 1, 0, 0, 0, 0, 0);//continues from previous search, finds first EOL
      assertIsFound("\r\r", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "A", isBckward, isNotIgnoreC));

      append(textStore, new byte[] { (byte)'a' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore,  0,  0, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, -1, -1, 1, 0, 1, 0);
      assertIsFound("\r", isBckward, isNotIgnoreC, textStore, 1, 0, 0, 0, 0, 0);//continues from previous search, finds first EOL
      assertIsFound("\r\r", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isBckward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isBckward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "A", isBckward, isNotIgnoreC));

      append(textStore, new byte[] { (byte)'\r' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore,  0,  0, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore,  1,  0, 2, 1, 2, 1);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, -1, -1, 2, 1, 2, 1);
      assertIsFound("\r", isBckward, isNotIgnoreC, textStore, 2, 1, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, 1, 0, 0, 0, 0, 0);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isBckward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isBckward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));

      append(textStore, new byte[] { (byte)'b' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore,  0,  0, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore,  1, 0, 2, 1, 2, 1);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("b", isForward, isNotIgnoreC, textStore, -1, -1, 3, 0, 3, 0);
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, -1, -1, 2, 1, 2, 1);
      assertIsFound("\r", isBckward, isNotIgnoreC, textStore, 2, 1, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, 1, 0, 0, 0, 0, 0);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isBckward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isBckward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("b", isBckward, isNotIgnoreC, textStore, -1, -1, 3, 0, 3, 0);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "A", isBckward, isNotIgnoreC));

      append(textStore, new byte[] { (byte)'c' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore,  0,  0, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore,  1,  0, 2, 1, 2, 1);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("b", isForward, isNotIgnoreC, textStore, -1, -1, 3, 0, 3, 0);
      assertIsFound("C", isForward, isIgnoreCase, textStore, -1, -1, 3, 1, 3, 1);
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, -1, -1, 2, 1, 2, 1);
      assertIsFound("\r", isBckward, isNotIgnoreC, textStore, 2, 1, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, 1, 0, 0, 0, 0, 0);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isBckward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isBckward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("b", isBckward, isNotIgnoreC, textStore, -1, -1, 3, 0, 3, 0);
      assertIsFound("C", isBckward, isIgnoreCase, textStore, -1, -1, 3, 1, 3, 1);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "A", isBckward, isNotIgnoreC));

      append(textStore, new byte[] { (byte)'\r' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore,  0,  0, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore,  1,  0, 2, 1, 2, 1);//continues from previous search, finds third EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore,  2,  1, 3, 2, 3, 2);//continues from previous search, finds fourth EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("b", isForward, isNotIgnoreC, textStore, -1, -1, 3, 0, 3, 0);
      assertIsFound("C", isForward, isIgnoreCase, textStore, -1, -1, 3, 1, 3, 1);
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, -1, -1, 3, 2, 3, 2);
      assertIsFound("\r", isBckward, isNotIgnoreC, textStore, 3, 2, 2, 1, 2, 1);//continues from previous search, finds second EOL
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, 2, 1, 1, 0, 1, 0);//continues from previous search, finds third EOL
      assertIsFound("\r", isBckward, isIgnoreCase, textStore, 1, 0, 0, 0, 0, 0);//continues from previous search, finds fourth EOL
      assertIsFound("\r\r", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 1, 0);
      assertIsFound("A", isBckward, isIgnoreCase, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("a", isBckward, isNotIgnoreC, textStore, -1, -1, 2, 0, 2, 0);
      assertIsFound("b", isBckward, isNotIgnoreC, textStore, -1, -1, 3, 0, 3, 0);
      assertIsFound("C", isBckward, isIgnoreCase, textStore, -1, -1, 3, 1, 3, 1);
      Assert.IsNull(textStore.FindString(null, "A", isForward, isNotIgnoreC));
      Assert.IsNull(textStore.FindString(null, "A", isBckward, isNotIgnoreC));

      textStore.Reset();
      append(textStore, new byte[] { (byte)'a', (byte)'b', (byte)'A', (byte)'B'});
      assertIsFound("ab", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 1);
      assertIsFound("ab", isForward, isIgnoreCase, textStore,  0,  0, 0, 2, 0, 3);
      assertIsFound("ab", isForward, isIgnoreCase, textStore,  0,  2, 0, 0, 0, 1);
      assertIsFound("ab", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 0, 1);
      assertIsFound("ab", isForward, isNotIgnoreC, textStore,  0,  0, 0, 0, 0, 1);
      assertIsFound("ab", isBckward, isIgnoreCase, textStore, -1, -1, 0, 2, 0, 3);
      assertIsFound("ab", isBckward, isIgnoreCase, textStore,  0,  2, 0, 0, 0, 1);
      assertIsFound("ab", isBckward, isIgnoreCase, textStore,  0,  0, 0, 2, 0, 3);
      assertIsFound("ab", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 0, 1);
      assertIsFound("ab", isBckward, isNotIgnoreC, textStore,  0,  0, 0, 0, 0, 1);


      textStore.Reset();
      append(textStore, new byte[] { (byte)'a', (byte)'b', (byte)'\r', (byte)'A', (byte)'B' });
      assertIsFound("ab", isForward, isIgnoreCase, textStore, -1, -1, 0, 0, 0, 1);
      assertIsFound("ab", isForward, isIgnoreCase, textStore,  0,  0, 1, 0, 1, 1);
      assertIsFound("ab", isForward, isIgnoreCase, textStore,  1,  0, 0, 0, 0, 1);
      assertIsFound("ab", isForward, isNotIgnoreC, textStore, -1, -1, 0, 0, 0, 1);
      assertIsFound("ab", isForward, isNotIgnoreC, textStore,  0,  0, 0, 0, 0, 1);
      assertIsFound("ab", isBckward, isIgnoreCase, textStore, -1, -1, 1, 0, 1, 1);
      assertIsFound("ab", isBckward, isIgnoreCase, textStore,  1,  0, 0, 0, 0, 1);
      assertIsFound("ab", isBckward, isIgnoreCase, textStore,  0,  0, 1, 0, 1, 1);
      assertIsFound("ab", isBckward, isNotIgnoreC, textStore, -1, -1, 0, 0, 0, 1);
      assertIsFound("ab", isBckward, isNotIgnoreC, textStore,  0,  0, 0, 0, 0, 1);
    }


    private void append(TextStore textStore, char c) {
      textStore.Append(new byte[] { (byte)c });
      System.Diagnostics.Debug.WriteLine(Environment.NewLine + $"{textStore}");
    }


    private void append(TextStore textStore, byte[] bytes) {
      textStore.Append(bytes);
      System.Diagnostics.Debug.WriteLine(Environment.NewLine + $"{textStore}");
    }


    private void assertTextStore(string searchedText) {
      System.Diagnostics.Debug.WriteLine("");
      System.Diagnostics.Debug.WriteLine(searchedText.Replace("\r", "\\r"));
      var textStore = new TextStore(1);
      var pdfBytes = new byte[searchedText.Length];
      for (var searchedTextIndex = 0; searchedTextIndex < searchedText.Length; searchedTextIndex++) {
        pdfBytes[searchedTextIndex] = (byte)searchedText[searchedTextIndex];
      }
      append(textStore, pdfBytes);
      var expectedStartLine = 0;
      var expectedStartChar = -1;
      var expectedEndLine = 0;
      var expectedEndChar = -1;//assignment not really needed, will be immediately overwritten
      for (var firstCharIndex = 0; firstCharIndex < searchedText.Length; firstCharIndex++) {
        expectedEndLine = expectedStartLine;
        expectedEndChar = expectedStartChar;//this is 1 less than expectedStartChar in next line
        expectedStartChar++;
        for (var lastCharIndex = firstCharIndex; lastCharIndex < searchedText.Length; lastCharIndex++) {
          expectedEndChar++;
          var searchText = searchedText[firstCharIndex..(lastCharIndex+1)];
          assertIsFound(searchText, isForward, isNotIgnoreC, textStore,
            -1, -1, expectedStartLine, expectedStartChar, expectedEndLine, expectedEndChar);
          assertIsFound(searchText, isBckward, isNotIgnoreC, textStore,
            -1, -1, expectedStartLine, expectedStartChar, expectedEndLine, expectedEndChar);
          var lowerSearchText = searchText.ToLowerInvariant();
          assertIsFound(lowerSearchText, isForward, isIgnoreCase, textStore,
            -1, -1, expectedStartLine, expectedStartChar, expectedEndLine, expectedEndChar);
          assertIsFound(lowerSearchText, isBckward, isIgnoreCase, textStore,
            -1, -1, expectedStartLine, expectedStartChar, expectedEndLine, expectedEndChar);
          if (searchedText[lastCharIndex]=='\r') {
            expectedEndLine++;
            expectedEndChar = -1;
          }
        }
        if (searchedText[firstCharIndex]=='\r') {
          expectedStartLine++;
          expectedStartChar = -1;
        }
      }
    }


    private void assertIsFound(string searchString, bool isForward, bool isIgnoreCase, TextStore textStore, 
      int startSearchLine, int startSearchCharPos, int expectedStartLine, int expectedStartChar, int expectedEndLine, int expectedEndChar) 
    {
      System.Diagnostics.Debug.WriteLine($"{searchString.Replace("\r", "\\r")}, {expectedStartLine}, {expectedStartChar}, {expectedEndLine}, {expectedEndChar}, ");
      TextStoreSelection? previousSelection;
      if (startSearchLine<0) {
        previousSelection = null;
      } else {
        previousSelection = new TextStoreSelection(textStore, startSearchLine, startSearchCharPos, startSearchLine, startSearchCharPos);
      }
      TextStoreSelection newSelection = textStore.FindString(previousSelection, searchString, isForward, isIgnoreCase)!;
      Assert.AreEqual(expectedStartLine, newSelection.StartLine);
      Assert.AreEqual(expectedStartChar, newSelection.StartChar);
      Assert.AreEqual(expectedEndLine, newSelection.EndLine);
      Assert.AreEqual(expectedEndChar, newSelection.EndChar);
    }
    #endregion
  }
}
