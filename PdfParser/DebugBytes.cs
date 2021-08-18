using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfParserLib {


  public static class DebugBytes {


    public static string Show(byte[] bytes) {
      var sb = new StringBuilder();
      foreach (var b in bytes) {
        sb.Append((char)b);
      }
      return sb.ToString();
    }


    public static string Show(ReadOnlySpan<byte> bytes) {
      var sb = new StringBuilder();
      foreach (var b in bytes) {
        sb.Append((char)b);
      }
      return sb.ToString();
    }
  }
}
