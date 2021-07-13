using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.RightsManagement;
using System.Text;

namespace XRefUpdater {


  public class SampleToPdf {
    public readonly string SampleString;


    public SampleToPdf(string sampleString) {
      SampleString = sampleString;
    }


    List<PdfObject> pdfObjects = new List<PdfObject>();
    Dictionary<string, PdfObject> pdfObjectsByName = new Dictionary<string, PdfObject>();


    public string Translate() {
      var objectSources = SampleString.Split("$object ",StringSplitOptions.RemoveEmptyEntries);
      var objectIndex = 1;
      foreach (var objectSource in objectSources) {
        var pdfObject = new PdfObject(objectIndex++, objectSource);
        pdfObjects.Add(pdfObject);
        pdfObjectsByName.Add(pdfObject.Name, pdfObject);
      }

      StringBuilder sbPdf = new StringBuilder();
      sbPdf.AppendLine("%PDF-1.7");
      sbPdf.AppendLine("%õäöü");
      sbPdf.AppendLine();
      foreach (var pdfObject in pdfObjects) {
        pdfObject.Append(sbPdf, pdfObjectsByName);
        sbPdf.AppendLine();
        sbPdf.AppendLine();
      }

      var xrefPosition = sbPdf.Length;
      sbPdf.AppendLine("xref");
      sbPdf.AppendLine($"0 {(pdfObjects.Count + 1)}");
      sbPdf.AppendLine("0000000000 65535 f");
      foreach (var pdfObject in pdfObjects) {
        sbPdf.AppendLine($"{pdfObject.Address:0000000000} 00000 n");
      }
      sbPdf.AppendLine();
      sbPdf.AppendLine("trailer");
      sbPdf.AppendLine($"  << /Size {(pdfObjects.Count + 1)}");
      sbPdf.AppendLine("    /Root 1 0 R");
      sbPdf.AppendLine("  >>");
      sbPdf.AppendLine("startxref");
      sbPdf.AppendLine($"{xrefPosition}");
      sbPdf.AppendLine("%%EOF");

      return sbPdf.ToString();
    }
  }


  public class PdfObject {
    public readonly int ID;
    public int Address;
    public readonly string Name;
    public readonly string ObjectString;
    public readonly string ObjectSource;


    public PdfObject(int id, string objectSource) {
      this.ID = id;
      ObjectSource = objectSource;
      //$object Page1Content1
      //  << /Length ## >>
      //stream
      //  BT
      //    /F1 24 Tf
      //    200 600 Td
      //    ( Hello World ) Tj
      //  ET
      //endstream


      //'$object ' is already removed
      var objectSourceIndex = 0;
      while (objectSource[objectSourceIndex++]!='\r') { }

      Name = objectSource[..(objectSourceIndex-1)];
      if (ObjectSource[objectSourceIndex++]!='\n') throw new Exception();

      var objectSourceSpan = ((ReadOnlySpan<char>)objectSource)[objectSourceIndex..];
      var s0 = objectSourceSpan.ToString();
      var streamIndex = objectSourceSpan.IndexOf("stream");
      if (streamIndex<0) {
        var objectSourceSpanIndex = objectSourceSpan.Length-1;
        for (; objectSourceSpanIndex>=0; objectSourceSpanIndex--) {
          var c = objectSourceSpan[objectSourceSpanIndex];
          if (c!='\r' && c!='\n') break;
        }
        ObjectString = objectSourceSpan[..(objectSourceSpanIndex+1)].ToString();

      } else {
        var objectSpan = objectSourceSpan[..streamIndex];
        var s1 = objectSpan.ToString();
        var streamSpan = objectSourceSpan[streamIndex..];
        var s2 = streamSpan.ToString();
        var lengthIndex = objectSpan.IndexOf('#');
        ObjectString = objectSpan[..lengthIndex].ToString();
        var endStreamIndex = streamSpan.IndexOf("endstream");
        ObjectString += (endStreamIndex - "stream  ".Length-20).ToString();
        ObjectString += objectSpan[(lengthIndex+1)..].ToString();
        var streamEndIndex = streamSpan.Length-1;
        for (; streamEndIndex>=0; streamEndIndex--) {
          var c = streamSpan[streamEndIndex];
          if (c!='\r' && c!='\n') break;
        }
        ObjectString += streamSpan[..(streamEndIndex+1)].ToString();
      }
    }


    private void skipCrLf(ref int objectSourceIndex) {
      if (ObjectSource[objectSourceIndex++]!='\r') throw new Exception();
      if (ObjectSource[objectSourceIndex++]!='\n') throw new Exception();
    }


    internal void Append(StringBuilder sbPdf, Dictionary<string, PdfObject> pdfObjectsByName) {
      Address = sbPdf.Length;
      sbPdf.AppendLine($"{ID} 0 obj"); 
      var objectStringParts = ObjectString.Split('§');
      var isOdd = true;
      foreach (var objectStringPart in objectStringParts) {
        if (isOdd) {
          isOdd = false;
          sbPdf.Append(objectStringPart);
        } else {
          isOdd = true;
          var referencedPdfObject = pdfObjectsByName[objectStringPart];
          sbPdf.Append($"{referencedPdfObject.ID} 0 R");
        }
      }
      sbPdf.Append(Environment.NewLine + "endobj");
    }


    public override string ToString() {
      return $"ID: {ID}; Name: {Name}; ObjectString: {ObjectString}; ";
    }
  }
}
