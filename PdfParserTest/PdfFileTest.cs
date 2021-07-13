using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfParserLib;


namespace PdfParserTest {


  [TestClass]
  public class PdfFileTest {

    [TestMethod]
    public void TestPdfFile() {
      var pdfParserTestDirectory = new DirectoryInfo(Environment.CurrentDirectory).Parent.Parent.Parent;
      //var file = @"C:\Users\peter\Source\Repos\PdfParser\PdfParserTest\file-sample_150kB.pdf";

      //var file = pdfParserTestDirectory.FullName + @"\H3 Simple Text String Example.pdf";
      //var pdfParser = new PdfParser(file);


      var dir = new DirectoryInfo(@"C:\Users\peter\OneDrive\OneDriveData\");
      processDir(dir);

    }


    int fileCount;


    private void processDir(DirectoryInfo dir) {
      var streamBuffer = new byte[10000_000];
      var stringBuilder = new StringBuilder();
      foreach (var file in dir.GetFiles("*.pdf")) {
        if (fileCount>10) {
          var pdfParser = new PdfParser(file.FullName, "|", "", streamBuffer, stringBuilder);
          System.Diagnostics.Debug.WriteLine(pdfParser.Tokeniser.Pages.Count + " " + file.FullName);
        }
        fileCount++;
      }

      foreach (var subDir in dir.GetDirectories()) {
        processDir(subDir);
      }
    }
  }
}
