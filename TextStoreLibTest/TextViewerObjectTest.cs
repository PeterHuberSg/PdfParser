using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfParserLib;
using System;
using System.Collections.Generic;


namespace PdfParserTest {


  [TestClass]
  public class TextViewerObjectTest {

    [TestMethod]
    public void TestTextViewerObject() {
      var textViewerObjects = new TextViewerObjects();
      var expectedObjects = new List<TextViewerObject>();
      Assert.IsNull(textViewerObjects.GetObjectForDisplayLine(0, 0));
      assertTextViewerObjects(expectedObjects, textViewerObjects);

      var anchor1 = new TextViewerAnchor("a1", 99);
      var link1 = textViewerObjects.AddLink(anchor1, 0, 5, 10);
      expectedObjects.Add(link1);
      Assert.IsNull(textViewerObjects.GetObjectForDisplayLine(0, 0));
      Assert.AreEqual(link1, textViewerObjects.GetObjectForDisplayLine(0, 5));
      Assert.AreEqual(link1, textViewerObjects.GetObjectForDisplayLine(0, 7));
      Assert.AreEqual(link1, textViewerObjects.GetObjectForDisplayLine(0, 10));
      Assert.IsNull(textViewerObjects.GetObjectForDisplayLine(0, 11));
      assertTextViewerObjects(expectedObjects, textViewerObjects);

      var anchor2 = new TextViewerAnchor("a2", 99);
      var link2 = textViewerObjects.AddLink(anchor2, 3, 5, 10);
      expectedObjects.Add(link2);
      Assert.IsNull(textViewerObjects.GetObjectForDisplayLine(3, 0));
      Assert.AreEqual(link2, textViewerObjects.GetObjectForDisplayLine(3, 5));
      Assert.AreEqual(link2, textViewerObjects.GetObjectForDisplayLine(3, 7));
      Assert.AreEqual(link2, textViewerObjects.GetObjectForDisplayLine(3, 10));
      Assert.IsNull(textViewerObjects.GetObjectForDisplayLine(3, 11));
      assertTextViewerObjects(expectedObjects, textViewerObjects);

      var anchor1a = new TextViewerAnchor("a1", 99);
      var link1a = textViewerObjects.AddLink(anchor1a, 0, 20, 30);
      expectedObjects.Insert(1, link1a);
      Assert.IsNull(textViewerObjects.GetObjectForDisplayLine(0, 0));
      Assert.AreEqual(link1, textViewerObjects.GetObjectForDisplayLine(0, 5));
      Assert.AreEqual(link1, textViewerObjects.GetObjectForDisplayLine(0, 7));
      Assert.AreEqual(link1, textViewerObjects.GetObjectForDisplayLine(0, 10));
      Assert.IsNull(textViewerObjects.GetObjectForDisplayLine(0, 11));
      Assert.AreEqual(link1a, textViewerObjects.GetObjectForDisplayLine(0, 20));
      Assert.AreEqual(link1a, textViewerObjects.GetObjectForDisplayLine(0, 27));
      Assert.AreEqual(link1a, textViewerObjects.GetObjectForDisplayLine(0, 30));
      Assert.IsNull(textViewerObjects.GetObjectForDisplayLine(0, 31));
      assertTextViewerObjects(expectedObjects, textViewerObjects);

      var stream1 = textViewerObjects.AddStream(new ObjectId(12, 0), 5, 0, 20);
      expectedObjects.Add(stream1);
      assertTextViewerObjects(expectedObjects, textViewerObjects);

      textViewerObjects.Reset(0);
      Assert.AreEqual(0, textViewerObjects.ObjectsCount);
      Assert.AreEqual(0, textViewerObjects.DisplayLinesCount);
      const int iMax = 302;
      for (int i = 0; i < iMax; i++) {
        var stream = textViewerObjects.AddStream(new ObjectId(12, 0), i, 0, 20);
        expectedObjects.Add(stream1);
      }
      Assert.AreEqual(iMax, textViewerObjects.ObjectsCount);
      Assert.AreEqual(iMax, textViewerObjects.DisplayLinesCount);
    }


    private void assertTextViewerObjects(List<TextViewerObject> expectedObjects, TextViewerObjects textViewerObjects) {
      Assert.AreEqual(expectedObjects.Count, textViewerObjects.ObjectsCount);
      if (expectedObjects.Count==0) return;

      foreach (var expectedObject in expectedObjects) {
        Assert.AreEqual(expectedObject, textViewerObjects.GetObjectForDisplayLine(expectedObject.Line, expectedObject.StartX));
      }
    }
  }
}
