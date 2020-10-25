using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfParserLib;


namespace PdfParserTest {
  [TestClass]
  public class PdfParserTest {
    [TestMethod]
    public void TestPdfParser() {
      var sb = new StringBuilder();
      var addrs = new List<int>();
      addrs.Add(0);
      sb.AppendLine("%PDF-1.4");
      sb.AppendLine("%‚„œ”");

      var page1ContentId = addStream(sb, addrs, $"Hello World");

      var page1Id = addObject(sb, addrs, $"<</Type/Page /Parent ~1~ 0 R/Contents {page1ContentId} 0 R>>");

      var pagesId = addObject(sb, addrs, $"<</Type/Pages /Kids[{page1Id} 0 R] /Count 1>>");

      var catalogId = addObject(sb, addrs, $"<</Type/Catalog /Pages {pagesId} 0 R>>");

      var xrefAddress = sb.Length;
      sb.AppendLine("xref");
      sb.AppendLine($"0 {addrs.Count}");
      sb.AppendLine("0000000000 65535 f");
      foreach (var address in addrs) {
        if (address==0) continue;

        sb.AppendLine($"{address:0000000000} 00000 n");
      }
      sb.AppendLine($"trailer<</Size {addrs.Count}/Root {catalogId} 0 R>>");
      sb.AppendLine("startxref");
      sb.AppendLine(xrefAddress.ToString());
      sb.AppendLine("%%EOF");
      var byteString = sb.ToString();
      var bytes = new byte[byteString.Length];
      var bytesIndex = 0;
      foreach (var ch in byteString) {
        bytes[bytesIndex++] = (byte)ch;
      }

      var pdfParser = new PdfParser(bytes);

      Assert.AreEqual("1.4", pdfParser.PdfVersion);
      var trailer = pdfParser.Tokeniser.TrailerEntries;
      var root = (DictionaryToken)trailer["Root"];
      Assert.AreEqual("Catalog", ((NameToken)root["Type"]).Value);
      var pages = (DictionaryToken)root["Pages"];
      Assert.AreEqual("Pages", ((NameToken)pages["Type"]).Value);
      var kids = (ArrayToken)pages["Kids"];
      foreach (var kid in kids) {
        var page = (DictionaryToken)kid;
        Assert.AreEqual("Page", ((NameToken)page["Type"]).Value);
        var pageContent = (DictionaryToken)page["Contents"];
      }
    }

    private object addStream(StringBuilder sb, List<int> addrs, string content) {

      return addObject(sb, addrs, $"<</Length {content.Length}>>stream\n{content}\nendstream");
    }

    private object addObject(StringBuilder sb, List<int> addrs, string content) {
      var objectId = addrs.Count;
      addrs.Add(sb.Length);
      sb.AppendLine($"{objectId} 0 obj");
      var contents = content.Split('~');
      for (int contentsIndex = 0; contentsIndex < contents.Length; contentsIndex++) {
        if (contentsIndex%2==0) {
          sb.Append(contents[contentsIndex]);
        } else {
          var offset = int.Parse(contents[contentsIndex]);
          sb.Append(objectId+offset);
        }
      }
      sb.AppendLine();
      sb.AppendLine("endobj");
      return objectId;
    }
  }
}
