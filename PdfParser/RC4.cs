using System;
using System.Collections.Generic;
using System.Text;

namespace PdfParserLib {
  public class RC4 {
    //copied from GitHubGist hoiogi/RC4.cs: https://gist.github.com/hoiogi/89cf2e9aa99ffc3640a4

    /// <summary>
    /// Process can be used for encryption and decryption, since both work exactly the same way. dataLength bytes starting at 
    /// dataOffset get in place processed, the changed bytes are still in data at the same location.
    /// </summary>
    public static void Encrypt(byte[] pwd, byte[] data, int dataOffset, int dataLength) {
      int a, i, j, k, tmp;
      int[] key, box;

      key = new int[256];
      box = new int[256];

      for (i = 0; i < 256; i++) {
        key[i] = pwd[i % pwd.Length];
        box[i] = i;
      }
      for (j = i = 0; i < 256; i++) {
        j = (j + box[i] + key[i]) % 256;
        tmp = box[i];
        box[i] = box[j];
        box[j] = tmp;
      }
      for (a = j = i = 0; i < dataLength; i++) {
        a++;
        a %= 256;
        j += box[a];
        j %= 256;
        tmp = box[a];
        box[a] = box[j];
        box[j] = tmp;
        k = box[((box[a] + box[j]) % 256)];
        var index = dataOffset + i;
        data[index] = (byte)(data[index] ^ k);
      }
    }


    public static byte[] Encrypt(byte[] pwd, byte[] data) {
      int a, i, j, k, tmp;
      int[] key, box;
      byte[] cipher;

      key = new int[256];
      box = new int[256];
      cipher = new byte[data.Length];

      for (i = 0; i < 256; i++) {
        key[i] = pwd[i % pwd.Length];
        box[i] = i;
      }
      for (j = i = 0; i < 256; i++) {
        j = (j + box[i] + key[i]) % 256;
        tmp = box[i];
        box[i] = box[j];
        box[j] = tmp;
      }
      for (a = j = i = 0; i < data.Length; i++) {
        a++;
        a %= 256;
        j += box[a];
        j %= 256;
        tmp = box[a];
        box[a] = box[j];
        box[j] = tmp;
        k = box[((box[a] + box[j]) % 256)];
        cipher[i] = (byte)(data[i] ^ k);
      }
      return cipher;
    }


    public static byte[] Decrypt(byte[] pwd, byte[] data) {
      return Encrypt(pwd, data);
    }

  }
}