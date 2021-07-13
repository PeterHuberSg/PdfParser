using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PdfParserLib {


  public class TextViewerAnchor {
    public readonly string ObjectIdString;
    public readonly int Line;
    //public double XStart;
    //public double XEnd;


    public TextViewerAnchor(string objectIdString, int line) {
      ObjectIdString = objectIdString;
      Line = line;
      //XStart = 0;
      //XEnd = 0;
    }


    public override string ToString() {
      return $"ObjectIdString: {ObjectIdString}; Line: {Line}";
    }
  }
}