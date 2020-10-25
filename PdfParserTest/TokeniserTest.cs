using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfParserLib;


namespace PdfParserTest {

  [TestClass]
  public class TokeniserTest {

    [TestMethod]
    public void TestTokeniser() {
      var expectedStrings = new List<string>();
      var testString = "false" + Environment.NewLine;
      expectedStrings.Add("False");

      testString += "true false" + Environment.NewLine;
      expectedStrings.Add("True");
      expectedStrings.Add("False");

      testString += "1 -2 00 +987654321" + Environment.NewLine;
      expectedStrings.Add("1");
      expectedStrings.Add("-2");
      expectedStrings.Add("0");
      expectedStrings.Add("987654321");

      testString += "+123.4 34.5 34. +.2 .1 0.0 -.002 -3.62" + Environment.NewLine;
      expectedStrings.Add("123.4");
      expectedStrings.Add("34.5");
      expectedStrings.Add("34");
      expectedStrings.Add("0.2");
      expectedStrings.Add("0.1");
      expectedStrings.Add("0");
      expectedStrings.Add("-0.002");
      expectedStrings.Add("-3.62");

      testString += "/name /n " + Environment.NewLine;
      expectedStrings.Add("/name");
      expectedStrings.Add("/n");

      testString += "(string)<112233445566778899AABCCDDEEFF>()<>" + Environment.NewLine;
      expectedStrings.Add("\"string\"");
      expectedStrings.Add("\"<112233445566778899AABCCDDEEFF>\"");
      expectedStrings.Add("\"\"");
      expectedStrings.Add("\"<>\"");

      testString += " (string) <112233445566778899AABCCDDEEFF> ( ) < > " + Environment.NewLine;
      expectedStrings.Add("\"string\"");
      expectedStrings.Add("\"<112233445566778899AABCCDDEEFF>\"");
      expectedStrings.Add("\" \"");
      expectedStrings.Add("\"< >\"");

      testString += "(a string can be\r\n on 2 lines or more) (a string can contain ()matched brackets)" + Environment.NewLine;
      expectedStrings.Add("\"a string can be\r\n on 2 lines or more\"");
      expectedStrings.Add("\"a string can contain ()matched brackets\"");

      testString += "(a string with one open \\( bracket) (a string with one closing \\) bracket)" + Environment.NewLine;
      expectedStrings.Add("\"a string with one open \\( bracket\"");
      expectedStrings.Add("\"a string with one closing \\) bracket\"");

      testString += "1%comment\n2" + Environment.NewLine;
      expectedStrings.Add("1");
      expectedStrings.Add("2");

      testString += "1 %comment \n%comment\n % comment \n 2" + Environment.NewLine;
      expectedStrings.Add("1");
      expectedStrings.Add("2");

      testString += " [ /someName false -0 (string) ] [ [ (array in array) ] true ] " + Environment.NewLine;
      expectedStrings.Add("[/someName False 0 \"string\"]\r\n");
      expectedStrings.Add("[\r\n[\"array in array\"]\r\n True]\r\n");

      testString += "[/someName false -0(string)][[(array in array)]true]" + Environment.NewLine;
      expectedStrings.Add("[/someName False 0 \"string\"]\r\n");
      expectedStrings.Add("[\r\n[\"array in array\"]\r\n True]\r\n");

      testString += "[/someName%\n]" + Environment.NewLine;
      expectedStrings.Add("[/someName]\r\n");

      testString += "[ /someName % comment \n /anotherName]" + Environment.NewLine;
      expectedStrings.Add("[/someName /anotherName]\r\n");

      testString += " << /Name1 123 >> " + Environment.NewLine;
      expectedStrings.Add(">>\r\n /Name1 123\r\n<<\r\n");

      testString += "<</Name1 124>>" + Environment.NewLine;
      expectedStrings.Add(">>\r\n /Name1 124\r\n<<\r\n");

      testString += "<< /Name1 125 /Name2 [ (string) (array) 126 ] /Name3 << /subName1 127 /subName2 true >> /Name4 (another string) /Name5 <112233EE> >>" + Environment.NewLine;
      expectedStrings.Add(">>\r\n /Name1 125\r\n /Name2 [\"string\" \"array\" 126]\r\n\r\n /Name3 >>\r\n /subName1 127\r\n /subName2 True\r\n<<\r\n\r\n /Name4 \"another string\"\r\n /Name5 \"<112233EE>\"\r\n<<\r\n");

      testString += "<</Name1 223/Name2[(string)(array)224]/Name3<</subName1 225/subName2 true>>/Name4(another string)/Name5<222233EE>>>" + Environment.NewLine;
      expectedStrings.Add(">>\r\n /Name1 223\r\n /Name2 [\"string\" \"array\" 224]\r\n\r\n /Name3 >>\r\n /subName1 225\r\n /subName2 True\r\n<<\r\n\r\n /Name4 \"another string\"\r\n /Name5 \"<222233EE>\"\r\n<<\r\n");

      testString += "<</Length 17>>\r\nstream\r\n01234567890123456\r\nendstream\r\n" + Environment.NewLine;
      expectedStrings.Add(">>\r\n /Length 17\r\n<<\r\nstream 879, 17 endstream\r\n");

      testString += "<< /Length 17 /Filter [ /FlateDecode ] >>\r\nstream\r\nx\x009Ck`\x0000\x0002\x0009\x00DE\x0003\x000C\x00B8\x0000\x0000( \x0001f\r\nendstream\r\n" + Environment.NewLine;
      expectedStrings.Add(">>\r\n /Length 17\r\n /Filter [/FlateDecode]\r\n\r\n<<\r\nstream 962, 17 endstream\r\n");

      testString += "null" + Environment.NewLine;
      expectedStrings.Add("null");

      testString += " 1  0  obj \r\n(a string) \r\nendobj" + Environment.NewLine;
      expectedStrings.Add("\"a string\" obj 1 0");

      testString += "2  1 obj(a string2)endobj" + Environment.NewLine;
      expectedStrings.Add("\"a string2\" obj 2 1");

      testString += "3  2 R" + Environment.NewLine;
      expectedStrings.Add("Ref  obj 3 2");

      testString += "7 0 obj <</Length 8 0 R>>stream\n12345678\nendstream endobj 8 0 obj 9 endobj" + Environment.NewLine;
      expectedStrings.Add(">>\r\n /Length ref 8 0\r\n<<\r\nstream 1101, 8 endstream obj 7 0\r\n");
      expectedStrings.Add("9 obj 8 0");

      var testStringuffer = new byte[testString.Length];
      for (int i = 0; i < testString.Length; i++) {
        testStringuffer[i] = (byte)testString[i];
      }
      var tokeniser=new Tokeniser(testStringuffer);
      foreach (var expectedString in expectedStrings) {
        Assert.AreEqual(expectedString, tokeniser.GetNextToken().ToString());
      }
    }
  }
}
