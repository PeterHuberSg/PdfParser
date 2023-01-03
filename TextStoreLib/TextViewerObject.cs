using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PdfParserLib {


  #region Class TextViewerObject
  //      ======================

  /// <summary>
  /// can be a link or a stream or not used.
  /// </summary>
  public class TextViewerObject {
    public bool IsLink => Anchor is not null;
    public bool IsStream => ObjectId.ObjectNumber>=0;
    public bool IsEmpty => !(IsLink && IsStream);

    public ObjectId ObjectId; //only for streams
    public TextViewerAnchor? Anchor; //only for links
    public int Line;
    public double StartX;
    public double EndX;


    //public TextViewerObject(TextViewerAnchor anchor, int line, double startX, double endX) {
    //  Anchor = anchor;
    //  Line = line;
    //  StartX = startX;
    //  EndX = endX;
    //}


    public TextViewerObject() {
      ObjectId = new ObjectId(-1, 0);
    }


    public TextViewerObject UpdateLink(TextViewerAnchor anchor, int line, double startX, double endX) {
      Anchor = anchor;
      Line = line;
      StartX = startX;
      EndX = endX;
      return this;
    }


    public TextViewerObject UpdateStream(ObjectId objectId, int line, double startX, double endX) {
      ObjectId = objectId;
      Line = line;
      StartX = startX;
      EndX = endX;
      return this;
    }


    public override string ToString() {
      if (IsLink) {
        return $"Link Name: {Anchor!.ObjectIdString}; Line: {Line}; StartX: {StartX}; EndX: {EndX};";
      } else if (IsStream) {
        return $"Stream ObjectId: {ObjectId}; Line: {Line}; StartX: {StartX}; EndX: {EndX};";
      }

      return "empty";
    }
  }
  #endregion


  #region Class TextViewerObjects
  //      =======================

  /// <summary>
  /// TextViewerObjects contains only the links and stream presently displayed on the screen.
  /// </summary>
  public class TextViewerObjects: IEnumerable<TextViewerObject> {

    #region Properties
    //      ----------

    public int ObjectsCount { get; private set; }
    
    
    public int DisplayLinesCount { get; private set; }


    public int DisplayAbsoluteLineOffset { get; private set; }
    

    public List<TextViewerObject> this[int lineIndex] {
      get {
        lineIndex -= DisplayAbsoluteLineOffset;
        if (lineIndex>DisplayLinesCount) throw new ArgumentException();

        return objectsLines[lineIndex]; 
      }
    }
    #endregion


    #region Constructor
    //      -----------

    readonly List<List<TextViewerObject>> objectsLines;
    readonly TextViewerObjectRecycler textViewerObjectRecycler;


    public TextViewerObjects() {
      objectsLines = new List<List<TextViewerObject>>(100);
      for (var lineIndex = 0; lineIndex < objectsLines.Capacity; lineIndex++) {
        objectsLines.Add(new());
      }
      textViewerObjectRecycler = new TextViewerObjectRecycler();
    }


    public void Reset(int absultelineOffset) {
      foreach (var objectsLine in objectsLines) {
        if (objectsLine.Count==0) continue;

        textViewerObjectRecycler.ResetObjects(objectsLine);
        objectsLine.Clear();
      }
      ObjectsCount = 0;
      DisplayLinesCount = 0;
      DisplayAbsoluteLineOffset = absultelineOffset;
    }
    #endregion


    #region Methods
    //      -------

    /// <summary>
    /// Add link based on line number as used in TextStore, not just display line number
    /// </summary>
    public TextViewerObject AddLink(TextViewerAnchor anchor, int absoluteline, double startX, double endX) {
      var line = absoluteline - DisplayAbsoluteLineOffset;
      if (line>=objectsLines.Count) {
        for (int lineIndex = objectsLines.Count; lineIndex<=line; lineIndex++) {
          objectsLines.Add(new List<TextViewerObject>());
        }
      }
      DisplayLinesCount = Math.Max(DisplayLinesCount, line+1);
      var objectsLine = objectsLines[line];
      foreach (var textViewerObject in objectsLine) {
        if (textViewerObject.Anchor==anchor) {
          //a link object for that anchor exists already
          System.Diagnostics.Debugger.Break();
          if (textViewerObject.Line!=line || textViewerObject.StartX!=startX || textViewerObject.EndX!=endX) {
            System.Diagnostics.Debugger.Break();
            textViewerObject.Line = line;
            textViewerObject.StartX = startX;
            textViewerObject.EndX = endX;
          }
          return textViewerObject;
        } 
      }
      //is new
      ObjectsCount++;
      var returnlink = textViewerObjectRecycler.GetObject().UpdateLink(anchor, line, startX, endX);
      objectsLine.Add(returnlink);
      return returnlink;
    }


    /// <summary>
    /// Add stream based on line number as used in TextStore, not just display line number
    /// </summary>
    public TextViewerObject AddStream(ObjectId objectId, int absoluteLine, double startX, double endX) {
      var line = absoluteLine - DisplayAbsoluteLineOffset;
      if (line>=objectsLines.Count) {
        for (int lineIndex = objectsLines.Count; lineIndex<=line; lineIndex++) {
          objectsLines.Add(new List<TextViewerObject>());
        }
      }
      DisplayLinesCount = Math.Max(DisplayLinesCount, line+1);
      var objectsLine = objectsLines[line];
      foreach (var textViewerObject in objectsLine) {
        if (textViewerObject.ObjectId==objectId) {
          //an stream object for that objectId exists already
          System.Diagnostics.Debugger.Break();
          if (textViewerObject.Line!=line || textViewerObject.StartX!=startX || textViewerObject.EndX!=endX) {
            System.Diagnostics.Debugger.Break();
            textViewerObject.Line = line;
            textViewerObject.StartX = startX;
            textViewerObject.EndX = endX;
          }
          return textViewerObject;
        }
      }
      //is new
      ObjectsCount++;
      var returnlink = textViewerObjectRecycler.GetObject().UpdateStream(objectId, line, startX, endX);
      objectsLine.Add(returnlink);
      return returnlink;
    }


    /// <summary>
    /// Gets the object based one the view line number. First line displayed is 0, although in the TextStore it might
    /// have a much higher line number
    /// </summary>
    public TextViewerObject? GetObjectForViewLine(int displayLine, double x) {
      if (displayLine>=DisplayLinesCount) return null;

      foreach (var textViewerObject in objectsLines[displayLine]) {
        if (textViewerObject.StartX<=x && textViewerObject.EndX>=x) {
          return textViewerObject;
        }
      }
      return null;
    }


    public IEnumerator<TextViewerObject> GetEnumerator() {
      foreach (var objectsLine in objectsLines) {
        if (objectsLine.Count>0) {
          foreach (var textViewerObject in objectsLine) {
            yield return textViewerObject;
          }
        }
      }
    }


    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }
    #endregion
  }
  #endregion


  #region Class TextViewerObjectRecycler
  //      ==============================

  public class TextViewerObjectRecycler {

    readonly Stack<TextViewerObject> textViewerObjects;


    public TextViewerObjectRecycler(int initialObjectsCount = 100) {
      textViewerObjects = new Stack<TextViewerObject>(initialObjectsCount);
      for (int objectIndex = 0; objectIndex < initialObjectsCount; objectIndex++) {
        textViewerObjects.Push(new TextViewerObject());
      }
    }


    public TextViewerObject GetObject() {
      if (textViewerObjects.TryPop(out var textViewerObject)) {
        return textViewerObject;
      } else {
        return new TextViewerObject();
      }
    }


    //public void ReturnObject(TextViewerObject textViewerObject) {
    //  textViewerObject.Anchor = null;
    //  textViewerObject.ObjectId = new ObjectId(-1, 0);
    //  textViewerObjects.Push(textViewerObject);
    //}


    public void ResetObjects(List<TextViewerObject> lineObjects) {
      foreach (var textViewerObject in lineObjects) {
        textViewerObject.Anchor = null;
        textViewerObject.ObjectId = new ObjectId(-1, 0);
        textViewerObjects.Push(textViewerObject);
      }
    }
  }
  #endregion
}
