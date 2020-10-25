using System;
using System.Collections.Generic;
using System.Text;


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
