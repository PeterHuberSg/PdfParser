using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PdfParserLib {


  public class TextViewerLinkOld {
    public readonly TextViewerAnchor Anchor;
    public int Line;
    public double StartX;
    public double EndX;


    public TextViewerLinkOld(TextViewerAnchor anchor, int line, double startX, double endX) {
      Anchor = anchor;
      Line = line;
      StartX = startX;
      EndX = endX;
    }


    public override string ToString() {
      return $"Name: {Anchor.ObjectIdString}; Line: {Line}; StartX: {StartX}; EndX: {EndX};";
    }
  }



  public class TextViewerLinksOld: IEnumerable<TextViewerLinkOld> {

    #region Properties
    //      ----------

    public int Count { get; private set; }


    public List<TextViewerLinkOld> this[int lineIndex] {
      get { return links[lineIndex]; }
    }
    #endregion


    #region Constructor
    //      -----------

    readonly List<TextViewerLinkOld>[] links;


    public TextViewerLinksOld(int lineCount) {
      links = new List<TextViewerLinkOld>[lineCount];
      for (var lineIndex = 0; lineIndex < lineCount; lineIndex++) {
        links[lineIndex] = new();
      }
    }
    #endregion


    #region Methods
    //      -------

    public TextViewerLinkOld Add(TextViewerAnchor anchor, int line, double startX, double endX) {
      var linkLine = links[line];
      foreach (var link in linkLine) {
        if (link.Anchor==anchor) {
          //exists already
          if (link.StartX!=startX || link.EndX!=endX) {
            System.Diagnostics.Debugger.Break();
            link.StartX = startX;
            link.EndX = endX;
          }
          return link;
        } 
      }
      //is new
      Count++;
      var returnlink = new TextViewerLinkOld(anchor, line, startX, endX);
      linkLine.Add(returnlink);
      return returnlink;
    }


    public TextViewerLinkOld? GetLink(int line, double x) {
      foreach (var link in links[line]) {
        if (link.StartX<=x && link.EndX>=x) {
          return link;
        }
      }
      return null;
    }


    public IEnumerator<TextViewerLinkOld> GetEnumerator() {
      foreach (var lineLinks in links) {
        if (lineLinks.Count>0) {
          foreach (var link in lineLinks) {
            yield return link;
          }
        }
      }
    }


    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }
    #endregion
  }
}
