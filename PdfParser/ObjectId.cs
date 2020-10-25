using System;
using System.Collections.Generic;
using System.Text;


namespace PdfParserLib {

  /// <summary>
  /// Some tokens in a pdf file are market as objects by assigning them an ObjectNumber and Generation. There might be
  /// several objects with exactly the same ObjectNumber and Generation, in which case the last of these objects is
  /// the valid one. If an object is no longer used (=deleted), it remains in the file, but it's ObjectNumber and 
  /// Generation combination is marked as freed. Only then can the same ObjectNumber be used for a new, unrelated
  /// object which has its Generation increment by 1 compared to the freed object.
  /// </summary>
  public readonly struct ObjectId {
    public int ObjectNumber { get; }
    public int Generation { get;}


    public ObjectId(int objectNumber, int generation) {
      ObjectNumber = objectNumber;
      Generation = generation;
    }


    public override string ToString() {
      return $"Object: {ObjectNumber}; Gen: {Generation};";
    }


    public override int GetHashCode() {
      return (ObjectNumber, Generation).GetHashCode(); ;
    }


    public override bool Equals(object? obj) {
      if (obj is ObjectId xrefRecord) {
        return ObjectNumber==xrefRecord.ObjectNumber && Generation==xrefRecord.Generation;
      } else {
        return false;
      }
    }
  }
}
