/**************************************************************************************

PdfContent
==========

holds the content of a pdf Page

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
using System.Collections.Generic;


namespace PdfParserLib {


  public class PdfContent {

    public readonly IReadOnlyDictionary<string, PdfFont> Fonts;
    public readonly string? PdfFontName;
    public readonly string? Text;
    public readonly List<string> TextFragments;
    public readonly string? Exception;
    public readonly string? Error;


    public PdfContent(DictionaryToken contentsDictionaryToken, IReadOnlyDictionary<string, PdfFont> fonts) {
      contentsDictionaryToken.PdfObject = this;
      Error = contentsDictionaryToken.StreamLengthProblem;
      var tokeniser = contentsDictionaryToken.GetStreamBytes();
      Fonts = fonts;
      TextFragments = new List<string>();
      decimal? lastLineOffset = null;
      string? newText = null;
      try {
        //q 0.12 0 0 0.12 0 0 cm
        ///R7 gs
        //0 0 0 rg
        //q
        //8.33333 0 0 8.33333 0 0 cm

        //322.8 648.48 31.92001 44.64001 re W n 

        //BT
        //  /F1 24 Tf
        //  100 100 Td
        //  ( Hello World ) Tj
        //ET

        (decimal x, decimal y, decimal width, decimal height)? clippingRegion = null;
        while (true) {
          //find BT
          ReadOnlySpan<byte> opCodeSpan;
          do {
            var opCode = tokeniser.GetStreamOpCode();
            if (opCode is null) return;

            opCodeSpan = opCode.Value.Span;
            if (opCodeSpan.Length==2) {
              if (opCodeSpan[0]=='B' && opCodeSpan[1]=='I') {
                tokeniser.ContentStreamSkipInlineImage();
                continue;
              }

              if (opCodeSpan[0]=='r' && opCodeSpan[1]=='e') {
                var cregion = tokeniser.ContentStreamGetClippingRegion();
                if (cregion!=null) {
                  //if (clippingRegion!=null && clippingRegion.Value.x + clippingRegion.Value.x) {

                  //}
                  clippingRegion = cregion;
                  var s = 1.ToString();
                  //////System.Diagnostics.Debug.WriteLine($"{(int)cregion.Value.x,3:D} {(int)cregion.Value.y,3:D} {(int)cregion.Value.width,3:D} {(int)cregion.Value.height,3:D} {(int)(cregion.Value.x + cregion.Value.width),3:D} {(int)(cregion.Value.y + cregion.Value.height),3:D}");
                }
                continue;
              }
            }


          } while (opCodeSpan.Length!=2 || opCodeSpan[0]!='B' || opCodeSpan[1]!='T');

          //processes text operation until ET
          tokeniser.MarkStreamStartOfTextFragment();
          PdfFont? font = null;
          while (true) {
            var opCode = tokeniser.GetStreamOpCode();//cannot return null (end of stream), because opCode ET must follow
            if (opCode is null) {
              Error += "Error Content stream: stream end found but 'ET' still missing." + Environment.NewLine;
              Error += tokeniser.ShowStreamContentAtIndex();
              return;
            }

            opCodeSpan = opCode.Value.Span;
            if (opCodeSpan.Length==1) {
              if (opCodeSpan[0]=='\'') {
                tokeniser.StartStreamArgumentReading();
                newText = tokeniser.GetStreamString(font);
                tokeniser.EndStreamArgumentReading();

              } else if (opCodeSpan[0]=='"') {
                tokeniser.StartStreamArgumentReading();
                tokeniser.SkipStreamArgument();
                tokeniser.SkipStreamArgument();
                newText = tokeniser.GetStreamString(font);
                tokeniser.EndStreamArgumentReading();
              } else {
                continue;
              }

            } else if (opCodeSpan.Length==2) {
              if (opCodeSpan[0]=='T') {
                var opCodeChar1 = opCodeSpan[1];
                if (opCodeChar1=='j') {
                  tokeniser.StartStreamArgumentReading();
                  newText = tokeniser.GetStreamString(font);
                  tokeniser.EndStreamArgumentReading();

                } else if (opCodeChar1=='J') {
                  tokeniser.StartStreamArgumentReading();
                  newText = tokeniser.GetStreamArrayString(font);
                  tokeniser.EndStreamArgumentReading();

                } else if (opCodeChar1=='f') {
                  tokeniser.StartStreamArgumentReading();
                  PdfFontName = tokeniser.GetStreamName();
                  if (!fonts.TryGetValue(PdfFontName, out font)) {
                    Error += $"Could not find font '{PdfFontName}'." + Environment.NewLine;
                  }
                  tokeniser.EndStreamArgumentReading();
                  continue;

                } else if (opCodeChar1=='d' || opCodeChar1=='D' || opCodeChar1=='*') {
                  Text += Environment.NewLine;
                  continue;

                } else if (opCodeChar1=='m') {
                  tokeniser.StartStreamArgumentReading();
                  tokeniser.GetStreamInt();
                  tokeniser.GetStreamInt();
                  tokeniser.GetStreamInt();
                  tokeniser.GetStreamInt();
                  tokeniser.GetStreamNumber();
                  var lineOffset = tokeniser.GetStreamNumber();
                  if (lastLineOffset!=lineOffset) {
                    lastLineOffset = lineOffset;
                    if (Text!=null) {
                      Text += Environment.NewLine;
                    }
                  }
                  tokeniser.EndStreamArgumentReading();
                  continue;

                } else {
                  //skip operants like TL
                  continue;
                }

              } else if (opCodeSpan[0]=='E' && opCodeSpan[1]=='T') {
                TextFragments.Add(tokeniser.GetStreamTextFragment());
                break;
              
              } else {
                //other 2 characters opcodes
                continue;
              }
            } else {
              //opcode is longer than 2 letters
              continue;
            }

            if (newText?.Contains("size")??false) {

            }
            Text += newText + tokeniser.ContentDelimiter;
          } 
        }
        //var s = tokeniser.StreamBytesToString();

      } catch (Exception ex) {
        if (Exception is null) {
          Exception = "";
        } else {
          Exception += Environment.NewLine + Environment.NewLine;
        }
        if (ex is PdfStreamException || ex is PdfException) {
          Exception = ex.ToDetailString();
        } else {
          Exception = ex.ToDetailString() + Environment.NewLine + tokeniser.ShowStreamContentAtIndex();
        }
      }
    }
  }
}
