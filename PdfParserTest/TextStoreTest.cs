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
    const bool isBackward = false;
    const bool isIgnoreCase = true;
    const bool isNotIgnoreC = false;


    [TestMethod]
    public void TestTextStoreFindLocation() {
      //var textStore1 = new TextStore();
      //textStore1.Append(new byte[] {(byte)'A', (byte)'B', (byte)'C', (byte)'Ä', (byte)Environment.NewLine[0], (byte)Environment.NewLine[1] });
      //var v = textStore1.FindLocation("ABCÄ\r", isForward, isNotIgnoreC);

      //simple 1 character tests
      TextStore textStore = new TextStore(1);
      Assert.IsNull(textStore.FindLocation(" ", isForward, isIgnoreCase));
      textStore.Append(new byte[] { (byte)'a' });
      assertIsFound("a", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, 0, 0, 0, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));

      textStore.Append(new byte[] { (byte)'ä' });
      assertIsFound("ä", isForward, isIgnoreCase, textStore, 0, 1, 0, 1);
      assertIsFound("ä", isForward, isNotIgnoreC, textStore, 0, 1, 0, 1);
      assertIsFound("Ä", isForward, isIgnoreCase, textStore, 0, 1, 0, 1);
      Assert.IsNull(textStore.FindLocation("Ä", isForward, isNotIgnoreC));
    
      textStore.Append(new byte[] { (byte)'a' });
      assertIsFound("a", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, 0, 2, 0, 2);
      assertIsFound("A", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));
      assertIsFound("ä", isForward, isIgnoreCase, textStore, 0, 1, 0, 1);
      assertIsFound("ä", isForward, isNotIgnoreC, textStore, 0, 1, 0, 1);
      assertIsFound("Ä", isForward, isIgnoreCase, textStore, 0, 1, 0, 1);
      Assert.IsNull(textStore.FindLocation("Ä", isForward, isNotIgnoreC));

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
      textStore.Append(new byte[] { (byte)'\r' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore, 0, 0, 0, 0);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));

      textStore.Append(new byte[] { (byte)'\r' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, 0, 0, 1, 0);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));

      textStore.Append(new byte[] { (byte)'a' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, 2, 0, 2, 0);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));

      textStore.Append(new byte[] { (byte)'\r' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 2, 1, 2, 1);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, 2, 0, 2, 0);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));

      textStore.Append(new byte[] { (byte)'b' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 2, 1, 2, 1);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, 2, 0, 2, 0);
      assertIsFound("b", isForward, isNotIgnoreC, textStore, 3, 0, 3, 0);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));

      textStore.Append(new byte[] { (byte)'c' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 2, 1, 2, 1);//continues from previous search, finds third EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, 2, 0, 2, 0);
      assertIsFound("b", isForward, isNotIgnoreC, textStore, 3, 0, 3, 0);
      assertIsFound("C", isForward, isIgnoreCase, textStore, 3, 1, 3, 1);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));

      textStore.Append(new byte[] { (byte)'\r' });
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 0, 0, 0, 0);
      assertIsFound("\r", isForward, isNotIgnoreC, textStore, 1, 0, 1, 0);//continues from previous search, finds second EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 2, 1, 2, 1);//continues from previous search, finds third EOL
      assertIsFound("\r", isForward, isIgnoreCase, textStore, 3, 2, 3, 2);//continues from previous search, finds fourth EOL
      assertIsFound("\r\r", isForward, isNotIgnoreC, textStore, 0, 0, 1, 0);
      assertIsFound("A", isForward, isIgnoreCase, textStore, 2, 0, 2, 0);
      assertIsFound("a", isForward, isNotIgnoreC, textStore, 2, 0, 2, 0);
      assertIsFound("b", isForward, isNotIgnoreC, textStore, 3, 0, 3, 0);
      assertIsFound("C", isForward, isIgnoreCase, textStore, 3, 1, 3, 1);
      Assert.IsNull(textStore.FindLocation("A", isForward, isNotIgnoreC));
    }


    private void assertTextStore(string searchedText) {
      System.Diagnostics.Debug.WriteLine("");
      System.Diagnostics.Debug.WriteLine(searchedText.Replace("\r", "\\r"));
      var textStore = new TextStore(1);
      var pdfBytes = new byte[searchedText.Length];
      for (var searchedTextIndex = 0; searchedTextIndex < searchedText.Length; searchedTextIndex++) {
        pdfBytes[searchedTextIndex] = (byte)searchedText[searchedTextIndex];
      }
      textStore.Append(pdfBytes);
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
            expectedStartLine, expectedStartChar, expectedEndLine, expectedEndChar);
          if (searchedText=="\r") {
            //ensure that search starts again at beginning and does not continue previous search
            var (startLine, startChar, endLine, endChar) = textStore.FindLocation("z", isForward, isIgnoreCase)!.Value;
          }
          assertIsFound(searchText.ToLowerInvariant(), isForward, isIgnoreCase, textStore,
            expectedStartLine, expectedStartChar, expectedEndLine, expectedEndChar);
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
      int expectedStartLine, int expectedStartChar, int expectedEndLine, int expectedEndChar) 
    {
      System.Diagnostics.Debug.WriteLine($"{searchString.Replace("\r", "\\r")}, {expectedStartLine}, {expectedStartChar}, {expectedEndLine}, {expectedEndChar}, ");
      var (startLine, startChar, endLine, endChar) = textStore.FindLocation(searchString, isForward, isIgnoreCase)!.Value;
      Assert.AreEqual(expectedStartLine, startLine);
      Assert.AreEqual(expectedStartChar, startChar);
      Assert.AreEqual(expectedEndLine, endLine);
      Assert.AreEqual(expectedEndChar, endChar);
    }
    #endregion
  }
}
