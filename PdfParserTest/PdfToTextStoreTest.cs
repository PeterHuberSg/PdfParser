//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using PdfParserLib;
//using System;
//using System.Collections.Generic;


//namespace PdfParserTest {


//  [TestClass]
//  public class PdfToTextStoreTest {
//    [TestMethod]
//    public void TestPdfToTextStore() {
////      var pdfString1 = @"
/////I1 9 0 R /I2 10 0 R >>
////";
////      var bytes1 = toByteArray(pdfString1);
////      var textStore1 = new TextStore(10000);
////      PdfToTextStore.Convert(bytes1, textStore1);

//      var pdfString = @"
//%PDF-1.4
//%õäöü

///test {} {asd}
//8 0 obj
//<<
///Type /Page
///Resources <<
//  /ProcSet [ /PDF /Text /ImageC ]
//  /XObject << /I1 9 0 R /I2 10 0 R >>
//  /Font 4 0 R
//  /Shading 6 0 R
//  /ExtGState 7 0 R
//  >>
///MediaBox [0 0 594.00 792.00]
///Contents 11 0 R
///Parent 12 0 R
//>>
//endobj
//38 0 obj
//<<
///Type/XObject
///Subtype/Form
///FormType 1
///BBox [-1 762.37006 528.24406 764.93708]
///Matrix [1 0 0 1 0 0]
///Resources <<
///ProcSet 2 0 R
//>>
///Filter/FlateDecode
///Length 117
//>>
//stream
//WçRRóm·€ëšLÔÉëÆ½¹RiÆªÉ¼€‹Íñ.9Ü!È.þ@À‚éÎ©/Êµ³1Œ*FU¦'ƒu!ô6*£©Kq§xi`,¶lpy¥,”åøßGëOÚÄ˜@ï F¸âôóúáÎéþ^Æª»QØ³
//endstream
//endobj
//trailer
//<<
///Size 74
///Root 73 0 R
///Info 63 0 R
///Encrypt 1 0 R
///ID [<160cd54009e80816a5768f2c6e2f4358> <160cd54009e80816a5768f2c6e2f4358>]
//>>
//startxref
//67964
//%%EOF";
//      var bytes = toByteArray(pdfString);
//      var textStore = new TextStore(10000);
//      PdfToTextStore.Convert(bytes, textStore);
//      var expectedString = @"
//%PDF-1.4
//%õäöü

///test {{}} {{asd}}
//{a8 0} obj
//<<
///Type /Page
///Resources <<
//  /ProcSet [ /PDF /Text /ImageC ]
//  /XObject << /I1 {l9 0} R /I2 {l10 0} R >>
//  /Font {l4 0} R
//  /Shading {l6 0} R
//  /ExtGState {l7 0} R
//  >>
///MediaBox [0 0 594.00 792.00]
///Contents {l11 0} R
///Parent {l12 0} R
//>>
//endobj
//{a38 0} obj
//<<
///Type/XObject
///Subtype/Form
///FormType 1
///BBox [-1 762.37006 528.24406 764.93708]
///Matrix [1 0 0 1 0 0]
///Resources <<
///ProcSet {l2 0} R
//>>
///Filter/FlateDecode
///Length 117
//>>
//stream{s38 0}endstream
//endobj
//trailer
//<<
///Size 74
///Root {l73 0} R
///Info {l63 0} R
///Encrypt {l1 0} R
///ID [<160cd54009e80816a5768f2c6e2f4358> <160cd54009e80816a5768f2c6e2f4358>]
//>>
//startxref
//67964
//%%EOF
//";
//      var expectedStrings = expectedString.Split(Environment.NewLine);
//      var actualStrings = textStore.ToString(0, textStore.LinesCount).Split(Environment.NewLine);
//      Assert.AreEqual(expectedStrings.Length, actualStrings.Length);
//      for (int lineIndex = 0; lineIndex < expectedStrings.Length; lineIndex++) {
//        Assert.AreEqual(expectedStrings[lineIndex], actualStrings[lineIndex]);
//      }
//    }


//    private byte[]toByteArray(string pdfString) {
//      var bytes = new byte[pdfString.Length];
//      for (int charIndex = 0; charIndex < pdfString.Length; charIndex++) {
//        bytes[charIndex] = (byte)pdfString[charIndex];
//      }
//      return bytes;
//    }
//  }
//}
