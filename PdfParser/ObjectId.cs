/**************************************************************************************

ObjectId
========

structure holding ObjectNumber and Generation for a pdf objects

Written in 2021 by Jürgpeter Huber, Singapore

Contact: https://github.com/PeterHuberSg/PdfParser

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 1.0 Universal license. 

To view a copy of this license, read the file CopyRight.md or visit 
http://creativecommons.org/publicdomain/zero/1.0

This software is distributed without any warranty. 
**************************************************************************************/

using System;

namespace PdfParserLib {

  /// <summary>
  /// Some tokens in a pdf file are market as objects by assigning them an ObjectNumber and Generation. There might be
  /// several objects with exactly the same ObjectNumber and Generation, in which case the last of these objects is
  /// the valid one. If an object is no longer used (=deleted), it remains in the file, but it's ObjectNumber and 
  /// Generation combination is marked as freed. Only then can the same ObjectNumber be used for a new, unrelated
  /// object which has its Generation increment by 1 compared to the freed object.
  /// </summary>
  public readonly struct ObjectId: IEquatable<ObjectId>{
    public int ObjectNumber { get; }
    public int Generation { get;}


    public ObjectId(int objectNumber, int generation) {
      ObjectNumber = objectNumber;
      Generation = generation;
    }


    public ObjectId(string objectIdString) {
      var blankPosition = objectIdString.IndexOf(' ');
      ReadOnlySpan<char> objectIdStringSpan = objectIdString;
      ObjectNumber = int.Parse(objectIdStringSpan[..blankPosition]);
      Generation = int.Parse(objectIdStringSpan[(blankPosition+1)..]);
    }


    public ObjectId(ReadOnlySpan<byte> objectIdSpan) {
      ObjectNumber = 0;
      Generation = 0;
      var isFirstNumber = true;
      foreach (var b in objectIdSpan) {
        if (b==' ') {
          if (!isFirstNumber) throw new FormatException();

          isFirstNumber = false;
        } else {
          var c = (char)b;
          if (c<'0' || c>'9') throw new FormatException();

          var i = c - '0';
          if (isFirstNumber) {
            ObjectNumber = 10*ObjectNumber + i;
          } else {
            Generation = 10*Generation + i;
          }
        }
      }
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


    public bool Equals(ObjectId other) {
      if (other is ObjectId xrefRecord) {
        return ObjectNumber==xrefRecord.ObjectNumber && Generation==xrefRecord.Generation;
      } else {
        return false;
      }
    }


    public static bool operator ==(ObjectId left, ObjectId right) {
      return left.Equals(right);
    }


    public static bool operator !=(ObjectId left, ObjectId right) {
      return !(left==right);
    }


    public string ToShortString() {
      return $"O#:{ObjectNumber} G:{Generation}";
    }


    public override string ToString() {
      return $"Object: {ObjectNumber}; Gen: {Generation};";
    }
  }
}
