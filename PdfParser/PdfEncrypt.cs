using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PdfParserLib {


  public class PdfEncrypt {
    public readonly string Filter;
    public readonly int Version;
    public readonly int Revision;
    public readonly int Permission;
    public readonly int Length;
    public readonly int LengthBytes;
    public readonly byte[] O;
    public readonly byte[] U;
    public readonly byte[] TrailerId;
    public readonly byte[] PaddedPassword;


    public PdfEncrypt(DictionaryToken encryptionDictionary, Dictionary<string, Token>trailerEntries, string password) {
      if (!encryptionDictionary.TryGetName("Filter", out var filter) ||
        filter!="Standard" ||
        !encryptionDictionary.TryGetNumber("V", out var v) ||
        (v.Integer!=2 && v.Integer!=1) ||
        !encryptionDictionary.TryGetNumber("R", out var r) ||
        (r.Integer!=2 && r.Integer!=3) ||
        !encryptionDictionary.TryGetNumber("P", out var p) ||
        !encryptionDictionary.TryGetHexBytes("O", out O!) ||
        !encryptionDictionary.TryGetHexBytes("U", out U!) ||
        !trailerEntries.TryGetValue("ID", out var idToken)) {
        throw new ArgumentException("PdfParser can only decrypt pdf files using Standard encryption." + Environment.NewLine +
          encryptionDictionary.ToString());
      }
      Filter = filter;
      Version = v.Integer!.Value;
      Revision = r.Integer!.Value;
      Permission = p.Integer!.Value;
      Length = encryptionDictionary.TryGetNumber("Length", out var lengthToken) ? lengthToken.Integer!.Value : 40;
      LengthBytes = Length / 8;
      TrailerId = ((StringToken)((ArrayToken)idToken!)[0]).HexBytes!;
      PaddedPassword = pad(password);
    }


    byte[] paddingBytes =
      {0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
       0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A};


    private byte[] pad(string password) {
      var paddedBytes = new byte[32];
      var byteCount = Math.Min(password.Length, 32);
      int plainStringIndex = 0;
      //take first 32 bytes from plainString
      for (; plainStringIndex < byteCount; plainStringIndex++) {
        var c = (int)password[plainStringIndex];
        if (c<0x2F || c>0x7E) {
          throw new NotSupportedException("Presently, only passwords with ASCII characters are supported, but the password " +
            $"{password} had the character'{(char)c}'.");
        }
        paddedBytes[plainStringIndex] = (byte)c;
      }

      //fill up to 32 bytes
      for (; plainStringIndex < 32; plainStringIndex++) {
        paddedBytes[plainStringIndex] = paddingBytes[plainStringIndex];
      }
      return paddedBytes;
    }


    public override string ToString() {
      return $"Filter: {Filter}; Version: {Version}; Revision: {Revision}; Permission: {Permission}; Length: {Length}; ";
    }
  }
}
