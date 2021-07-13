﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PdfParserLib {


  public static class PdfToTextStore {


    enum stateEnum {
      parse,
      space,
      digits1,
      digits1Space,
      digits2,
      digits2Space,
      obj_o,
      obj_b,
      stream_s,
      stream_t,
      stream_r,
      stream_e,
      stream_a,
      end_e,
      end_n,
      end_d,
      endobj_o,
      endobj_b,
      endstream_s,
      endstream_t,
      endstream_r,
      endstream_e,
      endstream_a,
    }


    static readonly byte[] formatAnchor = {(byte)'{', (byte)'a'};
    static readonly byte[] formatLink = {(byte)'{', (byte)'l'};
    static readonly byte[] formatEnd = {(byte)'}'};
    static readonly byte[] formatStart = {(byte)'{'};

    static readonly byte[] formatStream = {(byte)' ', (byte)'{', (byte)'s'};
    static readonly byte[] formatStreamEnd = { (byte)'}', (byte)' '};


    public static void Convert(Tokeniser tokeniser, TextStore textStore, Dictionary<string, TextViewerAnchor> anchors){
      var state = stateEnum.parse;
      var number1 = int.MinValue;
      var number2 = int.MinValue;
      var number1Pos = 0;
      var number2Pos = 0; //beginning of second number
      var lastNumber2Pos = 0; //end of second number
      StringBuilder anchorObjectIdStringBuilder = new();
      int streamObjectIdStart = 0;
      int streamObjectIdEnd = 0;
      var startIndex = 0;
      var bytesIndex = 0;
      byte[] pdfBytesArray = (byte[])tokeniser.PdfBytes; //a byte[] is needed to create ReadOnlySpan<byte>;
      while (bytesIndex<pdfBytesArray.Length) {
        /*
        13 0 obj
        <</Metadata 10 0 R/Pages 9 0 R/Type/Catalog>>
        endobj

        21 0 obj
        <</Filter/FlateDecode/Length 427>>stream
        endstream
        endobj

        */
        var b = pdfBytesArray[bytesIndex++];
        //double brackets if they are not part of a format instruction
        if (b=='{') {
          textStore.Append(pdfBytesArray.AsSpan(startIndex, bytesIndex - startIndex));
          startIndex = bytesIndex;
          textStore.Append(formatStart);
        } else if (b=='}') {
          textStore.Append(pdfBytesArray.AsSpan(startIndex, bytesIndex - startIndex));
          startIndex = bytesIndex;
          textStore.Append(formatEnd);
        }

        switch (state) {
        case stateEnum.parse:
          if (b==' ') {
            state = stateEnum.space;
          } else if (b>='0' && b<='9') {
            number1 = b - '0';
            number1Pos = bytesIndex-1;
            state = stateEnum.digits1;
          } else if (b=='e') {
            state = stateEnum.end_e;
          } else if (b=='s') {
            state = stateEnum.stream_s;
          }
          break;

        case stateEnum.space:
          if (b>='0' && b<='9') {
            state = stateEnum.digits1;
            //isleadingSpaceFound = true;
            number1 = b - '0';
            number1Pos = bytesIndex-1;
          } else if (b=='e') {
            state = stateEnum.end_e;
          } else if (b=='s') {
            state = stateEnum.stream_s;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.digits1:
          if (b>='0' && b<='9') {
            number1 = number1*10 + b - '0';
          } else if (b==' ') {
            state = stateEnum.digits1Space;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.digits1Space:
          if (b>='0' && b<='9') {
            state = stateEnum.digits2;
            number2 = b - '0';
            number2Pos = bytesIndex-1;
          } else if (b=='e') {
            state = stateEnum.end_e;
          } else if (b=='s') {
            state = stateEnum.stream_s;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.digits2:
          if (b>='0' && b<='9') {
            number2 = number2*10 + b - '0';
          } else if (b==' ') {
            state = stateEnum.digits2Space;
            lastNumber2Pos = bytesIndex-1;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.digits2Space:
          if (b=='R') {
            textStore.Append(pdfBytesArray.AsSpan(startIndex, number1Pos - startIndex));
            textStore.Append(formatLink);
            textStore.Append(pdfBytesArray.AsSpan(number1Pos, lastNumber2Pos-number1Pos));
            textStore.Append(formatEnd);
            startIndex = lastNumber2Pos;
            state = stateEnum.parse;
          } else if (b=='o') {
            state = stateEnum.obj_o;
          } else if (b>='0' && b<='9') {
            //sequence of 3 numbers found. Discard first number
            state = stateEnum.digits2;
            //isleadingSpaceFound = true;
            number1 = number2;
            number2 = b - '0';
            number1Pos = number2Pos;
            number2Pos = bytesIndex-1;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.obj_o:
          if (b=='b') {
            state = stateEnum.obj_b;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.obj_b:
          if (b=='j') {
            textStore.Append(pdfBytesArray.AsSpan(startIndex, number1Pos - startIndex));
            textStore.Append(formatAnchor);
            var objectIdBytesSpan = pdfBytesArray.AsSpan(number1Pos, lastNumber2Pos-number1Pos);
            textStore.Append(objectIdBytesSpan);
            textStore.Append(formatEnd);
            anchorObjectIdStringBuilder.Clear();
            foreach (var anchorObjectIdByte in objectIdBytesSpan) {
              anchorObjectIdStringBuilder.Append((char)anchorObjectIdByte);
            }
            var objectIdString = anchorObjectIdStringBuilder.ToString();
            anchors.TryAdd(objectIdString, new TextViewerAnchor(objectIdString, textStore.LinesCount));
            startIndex = lastNumber2Pos;
            streamObjectIdStart = number1Pos;
            streamObjectIdEnd = lastNumber2Pos;
          }
          state = stateEnum.parse;
          break;

        case stateEnum.stream_s:
          if (b=='t') {
            state = stateEnum.stream_t;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.stream_t:
          if (b=='r') {
            state = stateEnum.stream_r;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.stream_r:
          if (b=='e') {
            state = stateEnum.stream_e;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.stream_e:
          if (b=='a') {
            state = stateEnum.stream_a;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.stream_a:
          if (b=='m') {
            textStore.Append(pdfBytesArray.AsSpan(startIndex, bytesIndex - startIndex));
            textStore.Append(formatStream);
            var streamObjectIdSpan = pdfBytesArray.AsSpan(streamObjectIdStart, streamObjectIdEnd-streamObjectIdStart);
            textStore.Append(streamObjectIdSpan);
            textStore.Append(formatStreamEnd);
            var streamDictionaryToken = (DictionaryToken)tokeniser.GetToken(new ObjectId(streamObjectIdSpan));
            //skip stream bytes
            bytesIndex = startIndex = streamDictionaryToken.StreamStartIndex + streamDictionaryToken.Length;

            //var obj = objectObjectId!.Value;
            //sb.Append($"[s{obj.ObjectNumber}; Gen: {obj.Generation}]");
            //isSkipStreamChars = true;
            //var skipLenght = Math.Min(5, sb.Length);
            //sb.Length -= skipLenght;
            //paragraph.Inlines.Add(new Run(sb.ToString()));
            //sb.Clear();
          }
          state = stateEnum.parse;
          break;

        case stateEnum.end_e:
          if (b=='n') {
            state = stateEnum.end_n;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.end_n:
          if (b=='d') {
            state = stateEnum.end_d;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.end_d:
          if (b=='o') {
            state = stateEnum.endobj_o;
          } else if (b=='s') {
            state = stateEnum.endstream_s;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.endobj_o:
          if (b=='b') {
            state = stateEnum.endobj_b;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.endobj_b:
          //if (b=='j') {
          //  objectObjectId = null;
          //}
          state = stateEnum.parse;
          break;

        case stateEnum.endstream_s:
          if (b=='t') {
            state = stateEnum.endstream_t;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.endstream_t:
          if (b=='r') {
            state = stateEnum.endstream_r;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.endstream_r:
          if (b=='e') {
            state = stateEnum.endstream_e;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.endstream_e:
          if (b=='a') {
            state = stateEnum.endstream_a;
          } else {
            state = stateEnum.parse;
          }
          break;

        case stateEnum.endstream_a:
          if (b=='m') {
            startIndex = bytesIndex - 9;
            //paragraph.Inlines.Add(new PdfStreamRun(objectObjectId!.Value, this));
            //var streamToken = (DictionaryToken)tokeniser.GetToken(objectObjectId!.Value)!;
            //if (streamToken is DictionaryToken streamDictionaryToken && streamDictionaryToken.Type=="XRef") {
            //  if (streamDictionaryToken.PdfObject is string xRefString) {
            //    paragraph.Inlines.Add(new Run(Environment.NewLine + "XRef stream content:" + Environment.NewLine + xRefString));
            //  }
            //}
            //var streamToken = (DictionaryToken)tokeniser.GetToken(objectObjectId!.Value)!;
            //if (streamToken is DictionaryToken streamDictionaryToken && streamDictionaryToken.Type=="XRef") {
            //  if (streamDictionaryToken.PdfObject is string xRefString) {
            //    System.Diagnostics.Debugger.Break();
            //  }
            //}

          }
          state = stateEnum.parse;
          break;

        default:
          throw new NotSupportedException();
        }

        //if (!isSkipStreamChars) {
        //  var c = PdfEncodings.PdfEncoding[b];
        //  if (c==0xFFFF) {
        //    sb.Append('\'' + b.ToString("x") + '\'');
        //  } else {
        //    sb.Append(c);
        //  }
        //}

        //if (isSkipOneMore) {
        //  isSkipOneMore = false;
        //  isSkipStreamChars = false;
        //}
      }
      textStore.Append(pdfBytesArray.AsSpan(startIndex, bytesIndex - startIndex));
    }
  }
}
