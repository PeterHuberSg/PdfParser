/**************************************************************************************

PdfFontTypeEnum
===============

Enumerates the defined odf font types

Written in 2021 by Jürgpeter Huber, Singapore

Contact: https://github.com/PeterHuberSg/PdfParser

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 1.0 Universal license. 

To view a copy of this license, read the file CopyRight.md or visit 
http://creativecommons.org/publicdomain/zero/1.0

This software is distributed without any warranty. 
**************************************************************************************/

namespace PdfParserLib {


  public enum PdfFontTypeEnum {
    none = -1,
    Type0,
    Type1,
    MMType1,
    Type3,
    TrueType,
    CIDFontType0,
    CIDFontType2,
  }


  public static class PdfFontTypeExtensions { 
    public static PdfFontTypeEnum ToPdfFontTypeEnum(this string s) {
      return s switch{
        "Type0" => PdfFontTypeEnum.Type0,
        "Type1" => PdfFontTypeEnum.Type1,
        "MMType1" => PdfFontTypeEnum.MMType1,
        "Type3" => PdfFontTypeEnum.Type3,
        "TrueType" => PdfFontTypeEnum.TrueType,
        "CIDFontType0" => PdfFontTypeEnum.CIDFontType0,
        "CIDFontType2" => PdfFontTypeEnum.CIDFontType2,
        _ => PdfFontTypeEnum.none,
      };
    }
  }

}
