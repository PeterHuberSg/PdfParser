/**************************************************************************************

Tokeniser
=========

Breaks a pdf file into its pdf tokens.

Written in 2021 by Jürgpeter Huber, Singapore

Contact: https://github.com/PeterHuberSg/PdfParser

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 1.0 Universal license. 

To view a copy of this license, read the file CopyRight.md or visit 
http://creativecommons.org/publicdomain/zero/1.0

This software is distributed without any warranty. 
**************************************************************************************/


//https://resources.infosecinstitute.com/pdf-file-format-basic-structure/
//https://brendanzagaeski.appspot.com/0005.html
//https://www.oreilly.com/library/view/pdf-explained/9781449321581/ch04.html

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace PdfParserLib {


  public class Tokeniser {

    #region Properties
    //      ----------

    /// <summary>
    /// Version from pdf file header
    /// </summary>
    public string PdfVersion { get { return "1." + pdfVerion; } }


    char pdfVerion;


    /// <summary>
    /// Some information provided by the pdf writer about the file
    /// </summary>
    public string? DocumentInfo { get; private set; }


    /// <summary>
    /// Unique identifier for pdf file. It has 2 parts. The first identifies the file, the second identifies the version.
    /// </summary>
    public string? DocumentID { get; private set; }


    /// <summary>
    /// Metadata about the pdf file, might be XML
    /// </summary>
    public string? Metadata { get; private set; }


    /// <summary>
    /// List of all pages in the pdf file. Iterate through every page to get the text content of the file.
    /// </summary>
    public IReadOnlyList<PdfPage> Pages { get { return pages; } }
    readonly List<PdfPage> pages = new List<PdfPage>();


    /// <summary>
    /// Pdf data structures giving access to the various pdf objects in the file.
    /// </summary>
    public IReadOnlyDictionary<string, Token> TrailerEntries { get { return trailerEntries; } }
    Dictionary<string, Token> trailerEntries = new Dictionary<string, Token>();


    /// <summary>
    /// Shows the pdf file content at the present reading position
    /// </summary>
    public string BufferContentAtIndex { get { return ShowBufferContentAtIndex(); } }


    /// <summary>
    /// Dictionary of all pdf objects in the pdf file
    /// </summary>
    public IReadOnlyDictionary<ObjectId, Token> Tokens => pdfXRefTable.Tokens;


    /// <summary>
    /// Set if pdf file contains encryption info, but the default password or the provided password fail
    /// </summary>
    public bool IsDecryptionError { get; private set; }


    /// <summary>
    /// Set if pdf file is encrypted and string needs decryption. Some strings never need decryption, like pdf document ID 
    /// and O and U in Encrypt dictionary
    /// </summary>
    internal bool IsStringNeedsDecryption { get; private set; }

    /// <summary>
    /// Shared by tokens to convert pdf file bytes into strings
    /// </summary>
    internal StringBuilder StringBuilder { get; private set; } //used by tokens to produce strings


    /// <summary>
    /// Contains all bytes of the pdf file
    /// </summary>
    public IReadOnlyList<byte> PdfBytes => bytes;
    byte[] bytes;

    /// <summary>
    /// Delimiter to separate 2 content section in a Text string
    /// </summary>
    public string ContentDelimiter { get; }
    #endregion


    #region Constructor
    //      -----------

    readonly string password;
    int bytesIndex; //points to the byte in bytes which gets presently parsed
    readonly byte[] workingBuffer;
    readonly List<DictionaryToken> trailerDictionaries;
    readonly Dictionary<int, ((int ObjectId, int Offset)[] Offsets, byte[] Bytes)> objectStreams;


    /// <summary>
    /// User and owner passwords are often empty string.<br/>
    /// If several files need to get parsed, big data structures should be reused. Create workingBuffer and 
    /// stringBuilder once and reuse them for each call of the Tokeniser constructor. If workingBuffer is too
    /// small, an Exception gets raised. If none is provide, a default one gets constructed of 100 kBytes.
    /// </summary>
    public Tokeniser(
      byte[] pdBfytes,
      string password = "",
      string contentDelimiter = "|",
      byte[]? workingBuffer = null,
      StringBuilder? stringBuilder = null) 
    {
      bytes = pdBfytes;
      this.password = password;
      trailerDictionaries = new List<DictionaryToken>();
      objectStreams = new Dictionary<int, ((int ObjectId, int Offset)[] Offsets, byte[] Bytes)>();
      ContentDelimiter = contentDelimiter;
      bytesIndex = 0;
      if (workingBuffer is null) {
        this.workingBuffer = new byte[200_000];
      } else {
        this.workingBuffer = workingBuffer;
      }
      if (stringBuilder is null) {
        StringBuilder = new StringBuilder();
      } else {
        StringBuilder = stringBuilder;
      }
    }
    #endregion


    #region Methods for PdfParser
    //      ---------------------

    /// <summary>
    /// Finds the pdf version in the header of the file. Not really
    /// </summary>
    public void VerifyFileHeader() {
      //%PDF-1.3
      //%âãÏÓ
      if (bytes[0]!='%' || bytes[1]!='P' || bytes[2]!='D' || bytes[3]!='F' || bytes[4]!='-' || bytes[5]!='1' || bytes[6]!='.') {
        //according to the pdf specification, nothing should come before %PDF-1.x But the python library FPDF has few text
        //lines first:
        //q 119.06 0 0 48.93 51.02 41.78 cm /I1 Do Q
        //BT 231.57 45.49 Td (Weitere 8000 Rezepte finden Sie auf www.swissmilk.ch/rezepte) Tj ET
        //BT 490.70 45.49 Td (0 / $) Tj ET
        var bytesIndex = 0;
        while (bytesIndex<2000 && (
          bytes[bytesIndex]!='%' ||
          bytes[bytesIndex + 1]!='P' ||
          bytes[bytesIndex + 2]!='D' ||
          bytes[bytesIndex + 3]!='F' ||
          bytes[bytesIndex + 4]!='-' ||
          bytes[bytesIndex + 5]!='1' ||
          bytes[bytesIndex + 6]!='.')) {
          bytesIndex++;
        }
        if (bytesIndex<2000) {
          pdfVerion = (char)bytes[bytesIndex + 7];
          return;
        }

        var byteString = $"{(char)bytes[0]}{(char)bytes[1]}{(char)bytes[2]}{(char)bytes[3]}{(char)bytes[4]}{(char)bytes[5]}{(char)bytes[6]}";
        throw new PdfException($"PDF File Header Format error: A pdf file should start with the bytes '%PDF-1.', but they are '{byteString}'.", this);
      }
      pdfVerion = (char)bytes[7];
    }


    /// <summary>
    /// A pdf file defines at its end or beginning where the root token is placed. The root token gives then access
    /// to a Pages collection. The property Pages is null until FindPages() is called.
    /// </summary>
    public void FindPages() {
      findXrefTable();
      readXrefTable();
      readTrailers();
    }


    const byte cr = (byte)'\r';
    const byte lf = (byte)'\n';


    int xrefIndex;


    private void findXrefTable() {
      //start reading at end of file, find EOF mark, skip backwards over carriage returns and line feeds:
      //startxref
      //141561
      //%%EOF
      bytesIndex = bytes.Length-1;
      byte b;
      do {
        b = bytes[bytesIndex--];
      } while (b==cr || b==lf || b==' ' || b==0); //One pdf file was filled with trailing zeros at the end :-(

      if ((b=='F') &&
        (bytes[bytesIndex--]=='O') &&
        (bytes[bytesIndex--]=='E') &&
        (bytes[bytesIndex--]=='%') &&
        (bytes[bytesIndex--]=='%')) {
        //pdf file has start information at end of file
        //read byte index of xref, skip carriage returns and line feeds
        do {
          b = bytes[bytesIndex--];
        } while (b==cr || b==lf || b==' ');
        xrefIndex = 0;
        var power = 1;
        while (true) {
          if (b>='0' && b<='9') {
            xrefIndex += power * (b-'0');
            power *= 10;
            b = bytes[bytesIndex--];
          } else if (b==cr || b==lf || b==' ') {
            break;
          } else {
            throw new PdfException("This is not a pdf file.", this);
          }
        }
        bytesIndex = xrefIndex;

      } else {
        //is it a linearized PDF ? Try to find the Linearizion Parameter Dictionary at beginning of file
        //%PDF-1.2
        //%âãÏÓ
        //5 0 obj
        //<<
        ///Linearized 1
        ///O 7
        ///H[676 150]
        ///L 4113
        ///E 3579
        ///N 1
        ///T 3896
        //>>
        //endobj
        //                                                                xref
        //5 12
        //0000000016 00000 n
        //0000000584 00000 n
        //0000000826 00000 n
        //0000001036 00000 n
        //0000001162 00000 n
        //0000001816 00000 n
        //0000001837 00000 n
        //0000002115 00000 n
        //0000003187 00000 n
        //0000003309 00000 n
        //0000000676 00000 n
        //0000000806 00000 n
        //trailer
        //<<
        ///Size 17
        ///Info 1 0 R
        ///Root 6 0 R
        ///Prev 3887
        ///ID[<9afe310ca441eec118659f6f38844dc2><9afe310ca441eec118659f6f38844dc2>]
        //>>
        //startxref
        //0
        //%%EOF

        //skip version and 4 bytes on next line
        bytesIndex = "%PDF-1.2 /%â".Length;//somewhere within the 4 bytes
        b = bytes[bytesIndex++];
        while (b!=cr && b!=lf) {
          b = bytes[bytesIndex++];
        }
        var linearizionParameterToken = GetNextToken();
        if (linearizionParameterToken is DictionaryToken linearizionParameterDictionaryToken) {
          if (linearizionParameterDictionaryToken.ContainsKey("Linearized")) {
            //Linearizion Parameter Dictionary found, search following xref
            do {
              bytesIndex++;
            } while (bytes[bytesIndex]!='x' || bytes[bytesIndex+1]!='r' || bytes[bytesIndex+2]!='e' || bytes[bytesIndex+3]!='f');
            xrefIndex = bytesIndex;
            return;
          }
        }
        throw new PdfException("Pdf file format error: The end of file mark should be '%%EOF'.", this);
      }
    }


    PdfXRefTable pdfXRefTable;


    public void CreateEmptyXrefTableForUnitTest() {
      pdfXRefTable = new PdfXRefTable(this);
    }


    private void readXrefTable() {
      try {
        //read xref table
        bytesIndex = xrefIndex;
        pdfXRefTable = new PdfXRefTable(this);
        do {
          DictionaryToken trailerDictionary;
          if (verify("xref")) {
            //xref
            //0 5
            //0000000000 65535 f 
            //0000138560 00000 n 
            //0000000019 00000 n 
            //0000002806 00000 f 
            //0000138722 00000 n
            //10 2
            //0002138560 00000 n 
            //0002000019 00000 n 
            //trailer <<
            do {
              var startObjectNumber = new NumberToken(this, null).Integer!.Value;
              var xrefsCount = new NumberToken(this, null);
              for (int xrefTableIndex = 0; xrefTableIndex < xrefsCount.Integer!.Value; xrefTableIndex++) {
                var objectNumber = startObjectNumber + xrefTableIndex;
                var address = new NumberToken(this, null).Integer!.Value;
                var generation = new NumberToken(this, null).Integer!.Value;
                var objectId = new ObjectId(objectNumber, generation);
                bytesIndex++;
                var b = bytes[bytesIndex++];
                if (b=='n') {
                  if (!pdfXRefTable.ContainsKey(objectId)) {
                    //add only the newest reference. Since the reading starts at the end of the file, the newest xrefs get read first.
                    pdfXRefTable.Add(objectId, address);
                  }
                } else if (b=='f') {
                  if (objectNumber==0) {
                    if (generation<65535) {//according to spec, this value should be exactly 65535, but was 65536 for Adobe InDesign CS4 \(6.0.1\)
                      throw new PdfException($"Xref table: entry 0000000000 should have the value 65535 but has '{generation}' instead.", this);
                    }
                    //nothing to do.
                  } else {
                    //add or overwrite xref with address -1 to prevent earlier xrefs in the file, which will get read later to making
                    //their own entry.
                    pdfXRefTable.RemoveAddress(objectId);
                  }
                } else {
                  throw new PdfException($"'n' or 'f' missing after ref {address} {generation}.", this);

                }
              }
              SkipWhiteSpace();
            } while (bytes[bytesIndex]!='t');

            //trailer
            //<</Size 1458/Root 1 0 R/Info 127 0 R/ID[<EF04EF4886C5004887D5D04802EFAAA2><690778881CE52944B7BBDBE7F825CB8D>]/Prev 455036>>
            if (bytes[bytesIndex++]!='t' ||
              bytes[bytesIndex++]!='r' ||
              bytes[bytesIndex++]!='a' ||
              bytes[bytesIndex++]!='i' ||
              bytes[bytesIndex++]!='l' ||
              bytes[bytesIndex++]!='e' ||
              bytes[bytesIndex++]!='r') {
              throw new PdfException("Pdf file format error: trailer was missing after xref table.", this);
            } else {
              trailerDictionary = new DictionaryToken(this, null);
              trailerDictionaries.Add(trailerDictionary);
            }
          } else {
            //xref stream

            // 351 0 obj
            // <</DecodeParms<</Columns 5/Predictor 12>>/Filter/FlateDecode/ID[<A4D78FBE3AE8F047BCE6AD83CB3F293E>
            // <1292FD3F97ED434A92E1169982104B52>]/Index[81 1 97 1 103 1 105 1 107 1 109 1 112 2 116 1 123 1 127 1]
            // /Info 188 0 R/Length 122/Prev 116/Root 190 0 R/Size 352/Type/XRef/W[1 3 1]>>stream
            // xxxxxxx
            // endstream
            // endobj
            var xrefStreamToken = GetNextToken();
            if (xrefStreamToken is DictionaryToken xrefStreamDictionaryToken) {
              if (xrefStreamDictionaryToken.Type!="XRef")
                throw new PdfException($"readXrefTable(); dictionary type of xrefStream should be 'XRef' but was '{xrefStreamDictionaryToken.Type}'.", this);

              //read number of xref entries
              if (!xrefStreamDictionaryToken.TryGetNumber("Size", out var sizeNumberToken))
                throw new PdfException($"readXrefTable(); xrefStream is missing the 'W' entry.", this);

              //read start first xref table entry index and number of entries, might contain several first entries/number pairs
              if (!xrefStreamDictionaryToken.TryGetArray("Index", out var indexArrayToken)) {
                indexArrayToken = new ArrayToken(this, token: new NumberToken(this, 0));
                indexArrayToken.Add(sizeNumberToken);
              }

              //read column widths
              if (!xrefStreamDictionaryToken.TryGetArray("W", out var wArrayToken))
                throw new PdfException($"readXrefTable(); xrefStream is missing the 'W' entry.", this);

              if (wArrayToken.Count!=3) {
                throw new PdfException($"readXrefTable(); xrefStream should have 3 integers in 'W' entry, but there were " +
                  $"{wArrayToken.Count}.", this);
              }
              var widths = new int[]{
                ((NumberToken)wArrayToken[0]).Integer!.Value,
                ((NumberToken)wArrayToken[1]).Integer!.Value,
                ((NumberToken)wArrayToken[2]).Integer!.Value
              };

              //read stream
              xrefStreamDictionaryToken.GetStreamBytes();
              var objectCount = 0;
              var objectNumber = 0;
              var indexArrayIndex = 0;
              StringBuilder.Clear();
              while (streamIndex<streamBytes.Length) {
                if (objectCount<=0) {
                  objectNumber = ((NumberToken)indexArrayToken[indexArrayIndex++]).Integer!.Value;
                  objectCount = ((NumberToken)indexArrayToken[indexArrayIndex++]).Integer!.Value;
                } else {
                  objectNumber++;
                }
                objectCount--;
                var typeByteValue = getByteValue(ref streamIndex, widths[0]);
                switch (typeByteValue) {
                case 0: //xref table entry free object, this information is not really needed
                  var nextFreeObjectNumber = getByteValue(ref streamIndex, widths[1]);
                  var generationNumber = getByteValue(ref streamIndex, widths[2]);
                  StringBuilder.AppendLine($"{objectNumber}, 0, {nextFreeObjectNumber}, {generationNumber}");
                  break;

                case 1: //xref table in use entry
                  var byteOffset = getByteValue(ref streamIndex, widths[1]);
                  var generationNumber1 = getByteValue(ref streamIndex, widths[2]);
                  pdfXRefTable.Add(new ObjectId(objectNumber, generationNumber1), byteOffset);
                  StringBuilder.AppendLine($"{objectNumber}, 1, {byteOffset}, {generationNumber1}");
                  break;

                case 2: //xref table entry for compressed objects
                  var streamObjectNumber = getByteValue(ref streamIndex, widths[1]);
                  var streamObjectIndex = getByteValue(ref streamIndex, widths[2]);
                  pdfXRefTable.Add(new ObjectId(objectNumber, 0), streamObjectNumber, streamObjectIndex);
                  StringBuilder.AppendLine($"{objectNumber}, 2, {streamObjectNumber}, {streamObjectIndex}");
                  break;

                default:
                  throw new PdfStreamException($"readXrefTable(); xrefStream first column can be 0..2, but {typeByteValue}" +
                    "was found.", this);
                }
              }
              trailerDictionary = xrefStreamDictionaryToken;
              trailerDictionary.PdfObject = StringBuilder.ToString();
              trailerDictionaries.Add(trailerDictionary);

            } else {
              throw new PdfException("Cannot find cross reference table in pdf file.", this);
            }
          }
          if (trailerDictionary.TryGetValue("Prev", out var previousXrefAddressToken)) {
            bytesIndex = ((NumberToken)previousXrefAddressToken).Integer!.Value;
          } else {
            bytesIndex = -1;
          }
        } while (bytesIndex>=0);
      } catch (PdfException) {
        throw;
      } catch (PdfStreamException) {
        throw;
      } catch (Exception ex) {
        throw new PdfException("Error in PdfParser Read Xref Table: " + ex.Message, ex, this);
      }
    }


    private void readTrailers() {
      try {
        foreach (var trailerDictionary in trailerDictionaries) {
          //direct trailer, written into pdf file after xref
          // <</Size 1458/Root 1 0 R/Info 127 0 R/ID[<EF04EF4886C5004887D5D04802EFAAA2><690778881CE52944B7BBDBE7F825CB8D>]/Prev 455036>>

          //indirect trailer, inside an xref stream DictionaryToken
          // <</DecodeParms<</Columns 5/Predictor 12>>/Filter/FlateDecode/ID[<A...E><1...2>]/Index[81 1 ... 318 34]/Info 188 
          // 0 R/Length 122/Prev 116/Root 190 0 R/Size 352/Type/XRef/W[1 3 1]>>stream ...
          foreach (var tokenKey in trailerDictionary.Keys) {
            if (tokenKey!="Size" &&
              tokenKey!="Prev" &&
              tokenKey!="XRefStm" &&
              tokenKey!="DecodeParms" &&
              tokenKey!="Filter" &&
              tokenKey!="Index" &&
              tokenKey!="Length" &&
              tokenKey!="Type" &&
              tokenKey!="W") {
              var trailerDictionaryChildToken = trailerDictionary[tokenKey];
              if (trailerEntries.TryGetValue(tokenKey, out var token)) {
                if (token.GetType()!=trailerDictionaryChildToken.GetType())
                  throw new PdfException($"Trailer: Token '{trailerDictionaryChildToken}' for key '{tokenKey}' " +
                  $"in previous trailer table should be the same as the token '{token}' in the new table.", this);

                if (tokenKey!="ID" && trailerDictionaryChildToken.ToString()!=token.ToString()) {
                  throw new PdfException($"Trailer: Token '{trailerDictionaryChildToken}' for key '{ tokenKey}' " +
                    $"in previous trailer table should be the same as the token '{token}' in the new table.", this);
                } else {
                  //nothing to do, trailerEntries has already that value
                }
              } else {
                trailerEntries.Add(tokenKey, trailerDictionaryChildToken);
              }
            }
          }
        }

        //DocumentID
        if (trailerEntries.TryGetValue("ID", out var idToken)) {
          var idArray = (ArrayToken)idToken!;
          DocumentID = "";
          foreach (var detailIdToken in idArray) {
            if (detailIdToken is StringToken detailIdStringToken) {
              DocumentID += $"{detailIdStringToken}; ";
            }
          }
        }

        //Encryption
        string? passwordErrorMessage = null;
        if (trailerEntries.TryGetValue("Encrypt", out var encryptionToken)) {
          passwordErrorMessage = setupEncryption((DictionaryToken)encryptionToken);
          if (passwordErrorMessage is null) {
            IsStringNeedsDecryption = true;
            //decrypt strings already read
            foreach (var trailerEntry in trailerEntries) {
              if (trailerEntry.Key is "Root" or "Encrypt" or "ID") continue; //these entries are never encrypted

              if (trailerEntry.Value is DictionaryToken dictionaryToken) {
                foreach (var dictionaryItemKeyValue in dictionaryToken) {
                  if (dictionaryItemKeyValue.Value is StringToken stringToken) {
                    stringToken.DecryptValue(dictionaryToken.ObjectId!.Value, this);
                  }
                }
              }
            }
          }
        }

        //DocumentInfo
        if (trailerEntries.TryGetValue("Info", out var infoToken)) {
          DocumentInfo = "";
          try {
            if (infoToken is ArrayToken infoArrayToken) {
              foreach (var infoArrayTokenChild in infoArrayToken) {
                append(DocumentInfo, infoArrayTokenChild);
              }
            } else {
              append(DocumentInfo, infoToken);
            }
          } catch (Exception ex) {
            DocumentInfo += Environment.NewLine + $"Exception while reading info token {infoToken}:";
            DocumentInfo += Environment.NewLine + ex.ToString() + Environment.NewLine;
          }
        }
        if (passwordErrorMessage is not null) {
          DocumentInfo += Environment.NewLine + $"Exception while reading decryption information:";
          DocumentInfo += Environment.NewLine + passwordErrorMessage + Environment.NewLine;
        }

        //Document Catalog --> Pages
        if (trailerEntries.TryGetValue("Root", out var rootToken)) {
          //if (rootToken is RefToken rootRefToken) {
          //  rootToken = GetReferencedToken(rootRefToken);
          //}
          var rootDictionary = (DictionaryToken)rootToken!;
          if (rootDictionary.TryGetValue("Pages", out var pagesToken)) {
            readPages(pagesToken);
          }
          if (rootDictionary.TryGetValue("Metadata", out var metadataToken)) {
            readMetadata(metadataToken);
          }
        }
      } catch (Exception) {

        throw;
      }

    }


    #region Encryption
    //      ----------

    byte[]? globalEncryptionKey;
    PdfEncrypt? pdfEncrypt;


    private string? setupEncryption(DictionaryToken encryptionDictionary) {
      ///Filter/Standard
      ///V 2: 1 - length of 40 bits; 2 - length can be longer than 40 bits 
      ///R 3: 2 - for V with value 1; 3 - for V with value 2 or 3
      ///P -1852: permission
      ///Length 128
      ///O <3d7d8221e188c1d85e2721cf40ec31aa9c8c38de9e1f77bababcc787ef97a501>
      ///U <f15047b3b0b50546521d3d8c14247e7a00000000000000000000000000000000>
      if (!encryptionDictionary.TryGetName("Filter", out var filter) ||
        filter!="Standard" ||
        !encryptionDictionary.TryGetNumber("V", out var v) ||
        (v.Integer!=2 && v.Integer!=1) ||
        !encryptionDictionary.TryGetNumber("R", out var r) ||
        (r.Integer!=2 && r.Integer!=3) ||
        !encryptionDictionary.TryGetNumber("P", out var permission) ||
        !encryptionDictionary.TryGetHexBytes("O", out var encryptO) ||
        !encryptionDictionary.TryGetHexBytes("U", out var encryptU) ||
        !trailerEntries.TryGetValue("ID", out var idToken)) {
        throw new ArgumentException("PdfParser can only decrypt pdf files using Standard encryption." + Environment.NewLine +
          encryptionDictionary.ToString());
      }

      var paddedPassword = pad(password);
      var encodeLength = encryptionDictionary.TryGetNumber("Length", out var lengthToken) ? lengthToken.Integer!.Value : 40;
      var encodeLengthInBytes = encodeLength / 8;
      var idBytes = ((StringToken)((ArrayToken)idToken!)[0]).HexBytes!;

      var isNew = true;
      if (isNew) {
        pdfEncrypt = new PdfEncrypt(encryptionDictionary, trailerEntries, password);
        var encryptionKey = computeEncryptionKey(pdfEncrypt.PaddedPassword);
        if (authenticateUserPassword(pdfEncrypt.PaddedPassword, encryptionKey)) {
          globalEncryptionKey = encryptionKey;
          return null;
        }
        if (authenticateOwnerPassword()) {
          return null;
        }
        return "Document cannot be decrypted with provided password.";

      } else {
        trace("Setup Encription");
        var ownerKey = computeOwnerKey(encryptO, paddedPassword, encodeLengthInBytes);
        trace("try ownerKey");
        globalEncryptionKey = computeGlobalEncryptionKey(ownerKey, encryptO, permission.Integer!.Value, idBytes, encodeLengthInBytes);
        var userKey = ComputeUserKey(paddedPassword, idBytes, globalEncryptionKey);
        trace("userKey", userKey);
        trace("encryptU", encryptU);
        if (!sameBytes(userKey, encryptU, 16)) {
          trace("userKey and encryptU are different");
          trace("try paddedOwnerPassword");
          globalEncryptionKey = computeGlobalEncryptionKey(paddedPassword, encryptO, permission.Integer!.Value, idBytes, encodeLengthInBytes);
          userKey = ComputeUserKey(paddedPassword, idBytes, globalEncryptionKey);
          trace("userKey", userKey);
          trace("encryptU", encryptU);
          if (!sameBytes(userKey, encryptU, 16)) {
            trace("userKey and encryptU are different");
            IsDecryptionError = true;
            return "Document cannot be decrypted with provided password.";
          }
        }
      }
      return null;
    }


    #region new


    private byte[] computeEncryptionKey(byte[] paddedUserPassword) {
      // Algorithm 2: Computing an encryption key
      // a) Pad or truncate the password string to exactly 32 bytes. 
      // b) Initialize the MD5 hash function and pass the result of step (a) as input to this function.
      // c) Pass the value of the encryption dictionary’s O entry to the MD5 hash function. ("Algorithm 3: Computing the
      // encryption dictionary’s O (owner password) value" shows how the O value is computed.)
      // d) Convert the integer value of the P entry to a 32-bit unsigned binary number and pass these bytes to the MD5 hash
      // function, low-order byte first.
      // e) Pass the first element of the file’s file identifier array (the value of the ID entry in the document’s trailer
      // dictionary; see Table 15) to the MD5 hash function.
      //    NOTE The first element of the ID array generally remains the same for a given document.However, in some situations,
      //    conforming writers may regenerate the ID array if a new generation of a document is created.Security handlers are
      //    encouraged not to rely on the ID in the encryption key computation.
      // f) (Security handlers of revision 4 or greater) If document metadata is not being encrypted, pass 4 bytes with the
      // value 0xFFFFFFFF to the MD5 hash function.
      // g) Finish the hash.
      traceMethod("computeEncryptionKey()");
      var inputBytes = new byte[paddedUserPassword.Length + pdfEncrypt!.O.Length + /*p*/4 + pdfEncrypt.TrailerId.Length];
      trace("paddedUserPassword", paddedUserPassword);
      Buffer.BlockCopy(paddedUserPassword, 0, inputBytes, 0, paddedUserPassword.Length);
      var offset = paddedUserPassword.Length;
      trace("encryptO", pdfEncrypt.O);
      Buffer.BlockCopy(pdfEncrypt.O, 0, inputBytes, offset, pdfEncrypt.O.Length);
      offset += pdfEncrypt.O.Length;
      trace($"permission {pdfEncrypt.Permission:X}");
      var permission = pdfEncrypt.Permission; //need local copy because of the shift right
      for (int byteIndex = 0; byteIndex < 4; byteIndex++) {
        inputBytes[offset++] = (byte)permission;
        permission >>= 8;
      }
      trace("idBytes", pdfEncrypt.TrailerId);
      Buffer.BlockCopy(pdfEncrypt.TrailerId, 0, inputBytes, offset, pdfEncrypt.TrailerId.Length);
      trace("inputBytes", inputBytes);

      var encryptionKey = new byte[pdfEncrypt.LengthBytes];
      Buffer.BlockCopy(MD5.HashData(inputBytes), 0, encryptionKey, 0, encryptionKey.Length);


      // h) Do the following 50 times: Take the output from the previous MD5 hash and pass the first n bytes of the output as input into a new MD5 hash, where n is the number of bytes of the encryption key as defined by the value of the encryption dictionary’s Length entry.
      for (int iteration = 0; iteration<50; ++iteration) {
        Buffer.BlockCopy(MD5.HashData(encryptionKey), 0, encryptionKey, 0, encryptionKey.Length);
      }

      // i) Set the encryption key to the first n bytes of the output from the final MD5 hash, where n shall always be 5 for security handlers of revision 2 but, for security handlers of revision 3 or greater, shall depend on the value of the encryption dictionary’s Length entry.
      trace("EncryptionKey", encryptionKey[0..pdfEncrypt.LengthBytes]);
      traceEndMethod();
      return encryptionKey[0..pdfEncrypt.LengthBytes];
    }


    private bool authenticateUserPassword(byte[] paddedUserPassword, byte[] encryptionKey) {
      // Algorithm 6: Authenticating the user password
      traceMethod("authenticateUserPassword()");
      // a) Perform all but the last step of "Algorithm 5: Computing the encryption dictionary’s U (user password) value
      // (Security handlers of revision 3 or greater)" using the supplied password string.
      trace("paddedUserPassword", paddedUserPassword);
      trace("encryptionKey", encryptionKey);
      var calculatedEncryptU = computeEncryptU(paddedUserPassword, encryptionKey);
      trace("calculatedEncryptU", calculatedEncryptU);
      // b) If the result of step (a) is equal to the value of the encryption dictionary’s U entry (comparing on the first 16
      // bytes in the case of security handlers of revision 3 or greater), the password supplied is the correct user password.
      // The key obtained in step (a) (that is, in the first step of "Algorithm 5: Computing the encryption dictionary’s U
      // (user password) value (Security handlers of revision 3 or greater)") shall be used to decrypt the document.
      trace("pdfEncrypt.U", pdfEncrypt!.U);
      trace("First 16 bytes are the same : " + (sameBytes(calculatedEncryptU, pdfEncrypt!.U, 16) ? "yes" : "No"));
      traceEndMethod();
      return sameBytes(calculatedEncryptU, pdfEncrypt!.U, 16);
    }


    private byte[] computeEncryptU(byte[] paddedUserPassword, byte[] globalEncryptionKey) {
      // Algorithm 5: Computing the encryption dictionary’s U (user password) value (Security handlers of revision 3 or greater)
      // a) Create an encryption key based on the user password string, as described in "Algorithm 2: Computing an encryption
      // key".
      traceMethod("calculateEncryptU()");

      // b) Initialize the MD5 hash function and pass the 32-byte padding string shown in step (a) of "Algorithm 2: Computing an
      // encryption key" as input to this function.
      // c) Pass the first element of the file’s file identifier array (the value of the ID entry in the document’s trailer
      // dictionary; see Table 15) to the hash function and finish the hash.
      var rc4Data = new Byte[paddedUserPassword.Length + pdfEncrypt!.TrailerId.Length];
      trace("paddedUserPassword", paddedUserPassword);
      trace("pdfEncrypt.TrailerId", pdfEncrypt.TrailerId);
      Buffer.BlockCopy(paddedUserPassword, 0, rc4Data, 0, paddedUserPassword.Length);
      Buffer.BlockCopy(pdfEncrypt.TrailerId, 0, rc4Data, paddedUserPassword.Length, pdfEncrypt.TrailerId.Length);
      rc4Data = MD5.HashData(rc4Data);

      // d) Encrypt the 16-byte result of the hash, using an RC4 encryption function with the encryption key from step (a).
      trace("Rc4 Pwd (globalEncryptionKey)", globalEncryptionKey);
      trace("Rc4 data", rc4Data);
      rc4Data = RC4.Encrypt(pwd: globalEncryptionKey, data: rc4Data);
      trace("first newEncryptU32", rc4Data);

      // e) Do the following 19 times: Take the output from the previous invocation of the RC4 function and pass it as input to a new
      // invocation of the function; use an encryption key generated by taking each byte of the original encryption key obtained
      // in step (a) and performing an XOR (exclusive or) operation between that byte and the single-byte value of the
      // iteration counter (from 1 to 19).
      var rC4Key = new byte[globalEncryptionKey.Length];
      for (int iteration = 1; iteration<20; iteration++) {
        for (int byteIndex = 0; byteIndex<globalEncryptionKey.Length; byteIndex++) {
          rC4Key[byteIndex] = (byte)(globalEncryptionKey[byteIndex] ^ iteration);
        }
        RC4.Encrypt(pwd: rC4Key, data: rc4Data, 0, 16);
      }
      trace("final newEncryptU32", rc4Data);

      // f) Append 16 bytes of arbitrary padding to the output from the final invocation of the RC4 function and store the
      // 32-byte result as the value of the U entry in the encryption dictionary.
      traceEndMethod();
      Array.Resize(ref rc4Data, 32);//all 32 bytes get returned, the caller has to mask the last 16 bytes
      return rc4Data;
    }


    private bool authenticateOwnerPassword() {
      //// Algorithm 7: Authenticating the owner password
      //// 7a) Compute an encryption key from the supplied password string
      //traceMethod("authenticateOwnerPassword()");
      //var hashedOwnerPassword = calculatePasswordHash();
      //trace("hashedOwnerPassword", hashedOwnerPassword);

      ////7b) Do the following 20 times: Decrypt the value of the encryption dictionary’s O entry (first iteration) or the output
      ////from the previous iteration (all subsequent iterations), using an RC4 encryption function with a different encryption
      ////key at each iteration. The key shall be generated by taking the original key (obtained in step (a)) and performing an
      ////XOR (exclusive or) operation between each byte of the key and the single-byte value of the iteration counter (from 19
      ////to 0).
      //var rc4Data = pdfEncrypt!.O;
      //trace("rc4Data (pdfEncrypt.O)", rc4Data);
      //var rc4Key = new byte[pdfEncrypt.LengthBytes];
      //for (int iteration = 0; iteration<20; iteration++) {
      //  for (int byteIndex = 0; byteIndex < pdfEncrypt.LengthBytes; byteIndex++) {
      //    rc4Key[byteIndex] = (byte)(hashedOwnerPassword[byteIndex] ^ iteration);
      //  }
      //  RC4.Encrypt(rc4Key, rc4Data, 0, rc4Data.Length);
      //}
      //trace("calculated user password", rc4Data);

      ////7c) The result of step (b) purports to be the user password. Authenticate this user password using "Algorithm 6: 
      ////Authenticating the user password". If it is correct, the password supplied is the correct owner password.
      //var isUserPasswordAuthenticated =  authenticateUserPassword(rc4Data);
      //trace("isUserPasswordAuthenticated: " + isUserPasswordAuthenticated.ToString());
      //traceEndMethod();
      //return isUserPasswordAuthenticated;
      throw new NotImplementedException();
    }


    private byte[] calculatePasswordHash() {
      // Algorithm 3: Computing the encryption dictionary’s O value
      // 3a) Pad or truncate the owner password string
      // 3b) ) Initialize the MD5 hash function and pass the result of step (a) as input to this function.
      traceMethod("calculatePasswordHash()");
      trace("paddedPassword", pdfEncrypt!.PaddedPassword);
      var hashedPassword = MD5.HashData(pdfEncrypt!.PaddedPassword);
      trace("first hashedPassword", hashedPassword);

      // 3c) Do the following 50 times: Take the output from the previous MD5 hash and pass it as input into a new MD5 hash.
      for (int i = 0; i < 50; i++) {
        hashedPassword = MD5.HashData(hashedPassword.AsSpan()[0..pdfEncrypt!.LengthBytes]);
      }

      //3d) Create an RC4 encryption key using the first Encrypt.Length bytes of the output from the final MD5 hash.
      trace("final hashedPassword", hashedPassword);
      //--trace("lenght corrected hashedPassword", hashedPassword[0..pdfEncrypt!.LengthBytes]);
      traceEndMethod();
      //--specification says "Create an RC4 encryption key using the first Encrypt.Length bytes", reference takes all 32 hash bytes
      //--return hashedPassword[0..pdfEncrypt!.LengthBytes];
      return hashedPassword;
    }


    //private bool authenticateUserPassword(byte[] rc4Data) {
    //  // Algorithm 6: Authenticating the user password
    //  // a) Perform all but the last step of "Algorithm 5: Computing the encryption dictionary’s U (user password) value" using
    //  // the supplied password string.
    //  traceMethod("authenticateUserPassword()");
    //  var newEncryptU32 = calculateEncryptU();
    //  trace("calculated EncryptU32", newEncryptU32);
    //  trace("expected EncryptU32", pdfEncrypt!.U);

    //  // b) If the result of step (a) is equal to the value of the encryption dictionary’s U entry (comparing on the first 16
    //  // bytes in the case of security handlers of revision 3 or greater), the password supplied is the correct user password.
    //  // The key obtained in step (a) (that is, in the first step of "Algorithm 5: Computing the encryption dictionary’s U
    //  // (user password) value") shall be used to decrypt the document.
    //  if (sameBytes(newEncryptU32, pdfEncrypt!.U, 16)) {
    //    trace("correct user password");
    //    globalEncryptionKey = newEncryptU32;
    //    traceEndMethod();
    //    return true;
    //  }
    //  trace("invalid user password");
    //  traceEndMethod();
    //  return false;
    //}
    #endregion


    #region old


    private byte[] computeOwnerKey(byte[] encryptO, byte[] paddedPassword, int encodeLengthInBytes) {
      /*Algorithm 3: Computing the encryption dictionary’s O (owner password) value
      a) Pad or truncate the owner password string as described in step (a) of "Algorithm 2: Computing an
      encryption key". If there is no owner password, use the user password instead.
      b) Initialize the MD5 hash function and pass the result of step (a) as input to this function.
      c) (Security handlers of revision 3 or greater) Do the following 50 times: Take the output from the previous
      MD5 hash and pass it as input into a new MD5 hash.
      d) Create an RC4 encryption key using the first n bytes of the output from the final MD5 hash, where n shall
      always be 5 for security handlers of revision 2 but, for security handlers of revision 3 or greater, shall
      depend on the value of the encryption dictionary’s Length entry.
      e) Pad or truncate the user password string as described in step (a) of "Algorithm 2: Computing an encryption
      key".
      f) Encrypt the result of step (e), using an RC4 encryption function with the encryption key obtained in step
      (d).
      g) (Security handlers of revision 3 or greater) Do the following 19 times: Take the output from the previous
      invocation of the RC4 function and pass it as input to a new invocation of the function; use an encryption
      key generated by taking each byte of the encryption key obtained in step (d) and performing an XOR
      (exclusive or) operation between that byte and the single-byte value of the iteration counter (from 1 to 19).
      h) Store the output from the final invocation of the RC4 function as the value of the O entry in the encryption
      dictionary.
      */
      traceMethod("paddedOwnerPassword()");
      //get MD5 of owner password  
      trace("paddedOwnerPassword", paddedPassword);
      var hashedOwnerPassword = MD5.HashData(paddedPassword);//a), b)
      byte[] rc4Key = new byte[encodeLengthInBytes];
      for (int i = 0; i<50; ++i) {
        Buffer.BlockCopy(hashedOwnerPassword, 0, rc4Key, 0, encodeLengthInBytes);
        hashedOwnerPassword = MD5.HashData(rc4Key);//c)
      }
      Buffer.BlockCopy(hashedOwnerPassword, 0, rc4Key, 0, encodeLengthInBytes);//d)
      trace("rc4Key", rc4Key);
      trace("encryptO", encryptO);

      //get RC4 of 
      //pdf spec: Pad or truncate the user password string as described in step (a) of "Algorithm 2: Computing an encryption key".
      //reference apl: uses oValue, but calls it userpad !!!
      byte[] rc4DataBytes = new byte[32];
      Buffer.BlockCopy(encryptO, 0, rc4DataBytes, 0, 32);
      for (int i = 0; i<20; ++i) {
        for (int j = 0; j<encodeLengthInBytes; ++j) {
          rc4Key[j] = (byte)(hashedOwnerPassword[j] ^ i);
        }
        rc4DataBytes = RC4.Encrypt(rc4Key, rc4DataBytes);
      }
      trace("paddedOwnerPassword", rc4DataBytes);
      traceEndMethod();

      return rc4DataBytes;
    }


    private byte[] ComputeUserKey(byte[] paddedUserPassword, byte[] idBytes, byte[] globalEncryptionKey) {
      /* Algorithm 5: Computing the encryption dictionary’s U (user password) value
      
      a) Create an encryption key based on the user password string, as described in "Algorithm 2: Computing an
      encryption key".
      b) Initialize the MD5 hash function and pass the 32-byte padding string shown in step (a) of "Algorithm 2:
      Computing an encryption key" as input to this function.
      c) Pass the first element of the file’s file identifier array (the value of the ID entry in the document’s trailer
      dictionary; see Table 15) to the hash function and finish the hash.
      d) Encrypt the 16-byte result of the hash, using an RC4 encryption function with the encryption key from step
      (a).
      e) Do the following 19 times: Take the output from the previous invocation of the RC4 function and pass it as
      input to a new invocation of the function; use an encryption key generated by taking each byte of the
      original encryption key obtained in step (a) and performing an XOR (exclusive or) operation between that
      byte and the single-byte value of the iteration counter (from 1 to 19).
      f) Append 16 bytes of arbitrary padding to the output from the final invocation of the RC4 function and store
      the 32-byte result as the value of the U entry in the encryption dictionary.
      NOTE The standard security handler uses the algorithms 6 and 7 that follow, to determine whether a supplied
      password string is the correct user or owner password. Note too that algorithm 6 can be used to determine
      whether a document’s user password is the empty string, and therefore whether to suppress prompting for a
      password when the document is opened.
      */
      //byte[] userKey = new byte[32];
      //md5.Update(pad);
      //byte[] digest = md5.Digest(documentId);
      //Array.Copy(digest, 0, userKey, 0, 16);
      //for (int k = 16; k < 32; ++k) {
      //  userKey[k] = 0;
      //}
      //for (int i = 0; i < 20; ++i) {
      //  for (int j = 0; j < mkey.Length; ++j) {
      //    digest[j] = (byte)(mkey[j] ^ i);
      //  }
      //  arcfour.PrepareARCFOURKey(digest, 0, mkey.Length);
      //  arcfour.EncryptARCFOUR(userKey, 0, 16);
      //}
      //return userKey;

      traceMethod("ComputeUserKey()");
      var md5InputBytes = new byte[paddedUserPassword.Length + idBytes.Length];
      trace("paddedUserPassword", paddedUserPassword);
      Buffer.BlockCopy(paddedUserPassword, 0, md5InputBytes, 0, paddedUserPassword.Length);//a), b)
      trace("idBytes", idBytes);
      Buffer.BlockCopy(idBytes, 0, md5InputBytes, paddedUserPassword.Length, idBytes.Length);//c)
      var rc4DataBytes = new byte[16];
      trace("md5InputBytes", md5InputBytes);
      Buffer.BlockCopy(MD5.HashData(md5InputBytes), 0, rc4DataBytes, 0, 16);//d)
      trace("userKeyDataBytes", rc4DataBytes);

      byte[] rc4Key = new byte[globalEncryptionKey.Length];
      for (int i = 0; i<20; ++i) {//d), e)
        for (int j = 0; j < rc4Key.Length; ++j) {
          rc4Key[j] = (byte)(globalEncryptionKey[j] ^ i);
        }
        rc4DataBytes = RC4.Encrypt(rc4Key, rc4DataBytes);//f)
      }
      var userKeyDataBytes = new byte[32];
      Buffer.BlockCopy(rc4DataBytes, 0, userKeyDataBytes, 0, globalEncryptionKey.Length);
      trace("userKey", userKeyDataBytes);
      traceEndMethod();
      return userKeyDataBytes;
    }


    static readonly byte[] metadataPad = new byte[] { 255, 255, 255, 255 };


    private byte[] computeGlobalEncryptionKey(
      byte[] ownerKeyOrPaddedPassword,
      byte[] encryptO,
      int permission,
      byte[] idBytes,
      int encodeLengthInBytes) {
      traceMethod("computeGlobalEncryptionKey()");
      //var inputBytesCount = ownerKey.Length + o.Length + /*p*/4 + idBytes.Length + metadataPad.Length;
      var inputBytes = new byte[ownerKeyOrPaddedPassword.Length + encryptO.Length + /*p*/4 + idBytes.Length];
      trace("ownerKeyOrPaddedPassword", ownerKeyOrPaddedPassword);
      Array.Copy(ownerKeyOrPaddedPassword, inputBytes, ownerKeyOrPaddedPassword.Length);
      var offset = ownerKeyOrPaddedPassword.Length;
      trace("encryptO", encryptO);
      Buffer.BlockCopy(encryptO, 0, inputBytes, offset, encryptO.Length);
      offset += encryptO.Length;
      trace($"permission {permission}");
      for (int byteIndex = 0; byteIndex < 4; byteIndex++) {
        inputBytes[offset++] = (byte)permission;
        permission >>= 8;
      }
      trace("idBytes", idBytes);
      Buffer.BlockCopy(idBytes, 0, inputBytes, offset, idBytes.Length);
      //offset += idBytes.Length;
      //Array.Copy(metadataPad, 0, inputBytes, offset, metadataPad.Length);
      trace("inputBytes", inputBytes);

      var globalEncryptionKeyBytes = new byte[encodeLengthInBytes];
      Buffer.BlockCopy(MD5.HashData(inputBytes), 0, globalEncryptionKeyBytes, 0, globalEncryptionKeyBytes.Length);
      for (int i = 0; i<50; ++i) {
        Buffer.BlockCopy(MD5.HashData(globalEncryptionKeyBytes), 0, globalEncryptionKeyBytes, 0, globalEncryptionKeyBytes.Length);
      }

      trace("GlobalEncryptionKey", globalEncryptionKeyBytes);
      traceEndMethod();
      return globalEncryptionKeyBytes;
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
    #endregion


    #region Old and New

    private static bool sameBytes(byte[] bytes0, byte[] bytes1, int length) {
      if (bytes0.Length!=bytes1.Length || bytes0.Length<length) throw new System.ArgumentException();

      for (int bytesIndex = 0; bytesIndex < length; bytesIndex++) {
        if (bytes0[bytesIndex]!=bytes1[bytesIndex]) return false;
      }
      return true;
    }


    private object toIntString(byte[] inputBytes) {
      var sb = new StringBuilder();
      for (int i = 0; i < inputBytes.Length; i++) {
        var b = inputBytes[i];
        sb.AppendLine($"{i,2} {b,4}  {b:X2}");
        if ((i+1)%10 == 0) {
          sb.AppendLine();
        }
      }
      return sb.ToString();
    }


    string traceIndent = "";


    private void traceMethod(string message) {
      System.Diagnostics.Debug.WriteLine(traceIndent + message);
      traceIndent += "  ";
    }


    private void trace(string message) {
      System.Diagnostics.Debug.WriteLine(traceIndent + message);
    }


    StringBuilder traceStringBuilder = new();


    private void trace(string message, byte[] bytes) {
      traceStringBuilder.Clear();
      traceStringBuilder.Append(traceIndent + message);
      foreach (var b in bytes) {
        traceStringBuilder.Append(" ");
        traceStringBuilder.Append(b.ToString());
      }
      System.Diagnostics.Debug.WriteLine(traceStringBuilder.ToString());
    }


    private void traceEndMethod() {
      traceIndent = traceIndent[..^2];
      System.Diagnostics.Debug.WriteLine("");
    }
    #endregion
    #endregion

    private int getByteValue(ref int streamIndex, int bytesCount) {
      var byteValue = 0;
      var streamBytesSpan = streamBytes.Span;
      for (int bytesIndex = 0; bytesIndex < bytesCount; bytesIndex++) {
        byteValue = byteValue*0x100 + streamBytesSpan[streamIndex++];
      }
      return byteValue;
    }


    private void append(string documentInfo, Token infoToken) {
      var infoDictionary = (DictionaryToken)infoToken;
      foreach (var detailInfoToken in infoDictionary) {
        if (detailInfoToken.Value is StringToken detailInfoStringToken) {
          DocumentInfo += $"{detailInfoToken.Key}: {detailInfoStringToken.Value}; ";
        }
      }
    }


    private void readPages(Token pagesToken) {
      if (IsDecryptionError) return;

      var pagesDictionary = (DictionaryToken)pagesToken!;
      if (pagesDictionary.TryGetValue("Kids", out var kidsToken)) {
        var kidsArray = (ArrayToken)kidsToken!;
        foreach (var pageToken in kidsArray) {
          var pageDictionaryToken = (DictionaryToken)pageToken;
          if (pageDictionaryToken.Type=="Page") {
            pages.Add(new PdfPage(this, pageDictionaryToken));
          } else if (pageDictionaryToken.Type=="Pages") {
            readPages(pageDictionaryToken);
          }
        }
      }
    }


    private void readMetadata(Token metadataToken) {
      var metadataDictionary = (DictionaryToken)metadataToken!;
      if (metadataDictionary.TryGetValue("Subtype", out var subtypeToken)) {
        var subtypeNameToken = (NameToken)subtypeToken;
        if (subtypeNameToken.Value=="XML") {
          metadataDictionary.GetStreamBytes();
          Metadata = ShowStreamContent();
        }
      }
    }


    private bool verify(string verifyString) {
      var tempBytesIndex = bytesIndex;
      foreach (var ch in verifyString) {
        if (bytes[bytesIndex++]!=ch) {
          bytesIndex = tempBytesIndex;
          return false;
        }
      }
      return true;
    }


    internal string PdfExceptionMessage(string message) {
      return message + Environment.NewLine + ShowBufferContentAtIndex();
    }


    /// <summary>
    /// Shows the pdf file content at the present reading position
    /// </summary>
    public string ShowBufferContentAtIndex() {
      return ShowBufferContentAtIndex(bytesIndex);
    }


    /// <summary>
    /// Shows the pdf file content at provided index
    /// </summary>
    public string ShowBufferContentAtIndex(int index) {
      var startEarlier = Math.Max(0, index-100);
      var endLater = Math.Min(bytes.Length, index+100);
      var sb = new StringBuilder();
      int showIndex = startEarlier;
      for (; showIndex < index; showIndex++) {
        append(sb, bytes[showIndex]);
      }
      //sb.AppendLine();
      sb.Append("==>");
      if (showIndex<bytes.Length) {
        append(sb, bytes[showIndex++]);
      }
      sb.Append("<==");
      for (; showIndex < endLater; showIndex++) {
        append(sb, bytes[showIndex]);
      }
      sb.AppendLine();
      return sb.ToString();
    }


    enum objFilterStateEnum {
      start,
      s,
      t,
      r,
      e,
      a,
      m,
      skip,
      skipE,
      skipN,
      skipD,
      skipS,
      skipT,
      skipR,
      skipE1,
      skipA,
      skipM
    }


    /// <summary>
    /// Shows the complete pdf file content, skipping over stream content
    /// </summary>
    public string ShowBufferContent() {
      var sb = new StringBuilder();
      var objFilterState = objFilterStateEnum.start;
      var isEOLFound = false;
      for (var showIndex = 0; showIndex < bytes.Length; showIndex++) {
        var b = bytes[showIndex];
        switch (objFilterState) {
        case objFilterStateEnum.start: if (b=='s') objFilterState++; else objFilterState=0; break;
        case objFilterStateEnum.s: if (b=='t') objFilterState++; else objFilterState = 0; break;
        case objFilterStateEnum.t: if (b=='r') objFilterState++; else objFilterState = 0; break;
        case objFilterStateEnum.r: if (b=='e') objFilterState++; else objFilterState = 0; break;
        case objFilterStateEnum.e: if (b=='a') objFilterState++; else objFilterState = 0; break;
        case objFilterStateEnum.a: if (b=='m') objFilterState++; else objFilterState = 0; break;

        case objFilterStateEnum.m:
          if (b==cr || b==lf) {
            objFilterState++;
            sb.Append("...endstream");
          } else { 
            objFilterState=0;
          } break;

        case objFilterStateEnum.skip:
          if (isEOLFound) {
            if (b=='e') {
              objFilterState++;
            } else {
              isEOLFound = b==cr || b==lf;
            }
          } else {
            isEOLFound = b==cr || b==lf;
          }  
          break;

        case objFilterStateEnum.skipE: if (b=='n') objFilterState++; else objFilterState= objFilterStateEnum.skip ; break;
        case objFilterStateEnum.skipN: if (b=='d') objFilterState++; else objFilterState= objFilterStateEnum.skip ; break;
        case objFilterStateEnum.skipD: if (b=='s') objFilterState++; else objFilterState= objFilterStateEnum.skip ; break;
        case objFilterStateEnum.skipS: if (b=='t') objFilterState++; else objFilterState= objFilterStateEnum.skip ; break;
        case objFilterStateEnum.skipT: if (b=='r') objFilterState++; else objFilterState= objFilterStateEnum.skip ; break;
        case objFilterStateEnum.skipR: if (b=='e') objFilterState++; else objFilterState= objFilterStateEnum.skip ; break;
        case objFilterStateEnum.skipE1: if (b=='a') objFilterState++; else objFilterState= objFilterStateEnum.skip ; break;
        case objFilterStateEnum.skipA: if (b=='m') objFilterState++; else objFilterState= objFilterStateEnum.skip ; break;
        default: throw new NotSupportedException();
        }
        if (objFilterState<objFilterStateEnum.skip) {
          append(sb, b);
        }
        if (objFilterState==objFilterStateEnum.skipM) objFilterState = 0;

      }
      return sb.ToString();
    }


    private void append(StringBuilder sb, byte b) {
      if (b==cr || b==lf || (b>=0x20 && b<0x7F)) {
        sb.Append((char)b);
      } else {
        var ch = PdfEncodings.PdfEncoding[b];
        if (ch<0xFFFF) {
          sb.Append(ch);
        } else {
          sb.Append('\'' + b.ToString("x") + '\'');
        }
      }
    }


    /// <summary>
    /// Used to throw an exception which also shows the file content at the present reading position.
    /// </summary>
    internal PdfException Exception(string message) {
      return new PdfException(message, this);
    }


    /// <summary>
    /// Used to throw an exception which also shows the file content at the present reading position.
    /// </summary>
    internal PdfException Exception(string message, Exception innerException) {
      return new PdfException(message, innerException, this);
    }
    #endregion


    #region Methods for Tokens
    //      ------------------

    public bool IsEndOfBuffer() {
      return (bytesIndex+1)>=bytes.Length;
    }


    /// <summary>
    /// Get byte at present reading position
    /// </summary>
    public byte GetByte() {
      return bytes[bytesIndex];
    }


    /// <summary>
    /// Increment reading position by 1 and return that next byte
    /// </summary>
    public byte GetNextByte() {
      return bytes[++bytesIndex];
    }


    /// <summary>
    /// Return next byte without incrementing the reading position
    /// </summary>
    public byte LookaheadByte() {
      return bytes[bytesIndex+1];
    }


    internal Token GetToken(ObjectId objectId, int address) {
      var previousBytesIndex = bytesIndex;
      bytesIndex = address;
      var token = GetNextToken(null, objectId);
      bytesIndex = previousBytesIndex;
      return token;
    }


    /// <summary>
    /// Find the next token (number, string, name, array, dictionary, ...) at present reading location. Skip blank space.
    /// </summary>
    /// <param name="objectId">if token is part of an object definition, objectId is its unique ObjectNumber and Generation
    /// number. </param>
    /// <param name="objectIdExpected">The objectId the new object should have</param>
    public Token GetNextToken(
      ObjectId? objectId = null, 
      ObjectId? objectIdExpected = null, 
      bool isThrowExceptionWhenError = true) 
    {
      var b = SkipWhiteSpace();
      switch (b) {
      case (byte)'f':
      case (byte)'t': return new BoolToken(this, objectId);

      case (byte)'+':
      case (byte)'-':
      case (byte)'0':
      case (byte)'1':
      case (byte)'2':
      case (byte)'3':
      case (byte)'4':
      case (byte)'5':
      case (byte)'6':
      case (byte)'7':
      case (byte)'8':
      case (byte)'9':
      case (byte)'.': return processNumber(objectId, objectIdExpected, isThrowExceptionWhenError);

      case (byte)'(': return new StringToken(this, objectId);
      case (byte)'<':
        if (bytes[bytesIndex+1]=='<') {
          return new DictionaryToken(this, objectId);
        } else {
          return new StringToken(this, objectId);
        }

      case (byte)'/': return new NameToken(this, objectId);

      case (byte)'[': return new ArrayToken(this, objectId);

      case (byte)'n': return new NullToken(this, objectId);
      default:
        if (isThrowExceptionWhenError) {
          throw new PdfException($"NextToken(): unexpected character '{(char)b}'.", this);
        } else {
          return null!;
        }
      }
    }


    private Token processNumber(
      ObjectId? objectId, 
      ObjectId? objectIdExpected, 
      bool isThrowExceptionWhenError = true) 
    {
      //if there are 2 numbers followed by R or object, then it is not a NumberToken, but a Token with a reference
      var numberToken1 = new NumberToken(this, objectId);//in case this is not a reference, it is a numberToken which might have an objectId 
      var token2Index = bytesIndex;
      if (!numberToken1.HasReferenceFormat) {
        //not an object reference
        return numberToken1;

      } else { 
        //check if next token is also a positive integer
        var token2 = GetNextToken(isThrowExceptionWhenError: false);//token2 gets discarded if not a part of reference
        if (!(token2 is NumberToken numberToken2) || !numberToken2.HasReferenceFormat) {
          //not an object reference, just return first number
          bytesIndex = token2Index;
          return numberToken1;
        }

        var b = SkipWhiteSpace();
        var objectIdRef = new ObjectId(numberToken1.Integer!.Value, numberToken2.Integer!.Value);
        if (b=='R') {
          //its a reference:
          //128 0 R
          bytesIndex++;
          return new RefToken(this, objectIdRef);

        } else if (bytes[bytesIndex++]=='o' && bytes[bytesIndex++]=='b' && bytes[bytesIndex++]=='j') {
          //it's an object:
          //1 0 obj
          //<</Type/Catalog /Pages 2 0 R/Lang(en-SG) /StructTreeRoot 128 0 R>>
          //endobj
          SkipWhiteSpace();
          var startIndex = bytesIndex;
          var token = GetNextToken(objectIdRef, null, isThrowExceptionWhenError);
          b = SkipWhiteSpace();
          if (b!='e' ||
            bytes[++bytesIndex]!='n' ||
            bytes[++bytesIndex]!='d' ||
            bytes[++bytesIndex]!='o' ||
            bytes[++bytesIndex]!='b' ||
            bytes[++bytesIndex]!='j') {
            throw new PdfException("Indirect object format error, string 'endobj' missing.", this);
          }
          bytesIndex++;
          return token;
        }

      }
      //not a reference
      bytesIndex = token2Index;
      return numberToken1;
    }


    /// <summary>
    /// Returns the token an object reference points to. The ObjetId is used to find the object's address in the
    /// xref table.
    /// </summary>
    public Token GetToken(ObjectId objectIdRef) {
      return pdfXRefTable[objectIdRef];
    }


    /// <summary>
    /// Adds the token to PdfXRefTable. Gets called when a token is read from the pdf file and is marked as object. When
    /// the same token is needed in the future, it can be quickly found in PdfXRefTable by its ObjectId.
    /// </summary>
    internal void AddToTokens(Token token) {
      pdfXRefTable.Add(token);
    }


    static byte[] whiteCharBytes = { /*blank*/32, /*line feed*/10, /*carriage return*/13, /*tab*/9, /*form feed*/12, 0};


    public static bool IsWhiteSpace(byte b) {
      for (int whiteCharBytesIndex = 0; whiteCharBytesIndex < whiteCharBytes.Length; whiteCharBytesIndex++) {
        if (b==whiteCharBytes[whiteCharBytesIndex]) {
          return true;
        }
      }
      if (b=='%') {
        return true;
      }
      return false;
    }


    /// <summary>
    /// Moves the reading position to the first none white space character
    /// </summary>
    public byte SkipWhiteSpace() {
      while (true) {
        var isWhiteSpace = false;
        var b = bytes[bytesIndex];
        while (b=='%') {
          do {
            b = bytes[++bytesIndex];
          } while (b!=lf && b!=cr);
          if (b==cr && bytes[bytesIndex+1]==lf) {
            bytesIndex++;
          }
          b = bytes[++bytesIndex];
        }
        for (int whiteCharBytesIndex = 0; whiteCharBytesIndex < whiteCharBytes.Length; whiteCharBytesIndex++) {
          if (b==whiteCharBytes[whiteCharBytesIndex]) {
            isWhiteSpace = true;
            bytesIndex++;
            break;
          }
        }
        if (!isWhiteSpace) return b;

      }
    }


    static byte[] delimiterBytes = {
      (byte)'%',
      (byte)'(',
      (byte)')',
      (byte)'<',
      (byte)'>',
      (byte)'[',
      (byte)']',
      (byte)'{',
      (byte)'}',
      (byte)'/',
    };


    /// <summary>
    /// Returns in the current reading position points to a delimiter
    /// </summary>
    public bool IsDelimiterByte() {
      var b = bytes[bytesIndex];
      for (int whiteCharBytesIndex = 0; whiteCharBytesIndex < whiteCharBytes.Length; whiteCharBytesIndex++) {
        if (b==whiteCharBytes[whiteCharBytesIndex]) return true;
        
      }
      for (int delimiterBytesIndex = 0; delimiterBytesIndex < delimiterBytes.Length; delimiterBytesIndex++) {
        if (b==delimiterBytes[delimiterBytesIndex]) return true;
      
      }
      return false;
    }


    public static bool IsDelimiterByte(byte b) {
      for (int whiteCharBytesIndex = 0; whiteCharBytesIndex < whiteCharBytes.Length; whiteCharBytesIndex++) {
        if (b==whiteCharBytes[whiteCharBytesIndex]) return true;

      }
      for (int delimiterBytesIndex = 0; delimiterBytesIndex < delimiterBytes.Length; delimiterBytesIndex++) {
        if (b==delimiterBytes[delimiterBytesIndex]) return true;

      }
      return false;
    }


    /// <summary>
    /// If the present reading position is a stream, determine start and length of the bytes in the stream, without 
    /// reading those bytes.
    /// </summary>
    internal int GetStreamStartIndex(DictionaryToken dictionaryToken, out int length) {
      var startBytesIndex = bytesIndex;
      SkipWhiteSpace();
      if (bytes[bytesIndex++]!='s' ||
        bytes[bytesIndex++]!='t' ||
        bytes[bytesIndex++]!='r' ||
        bytes[bytesIndex++]!='e' ||
        bytes[bytesIndex++]!='a' ||
        bytes[bytesIndex++]!='m') 
      {
        //something else is following the dictionary than a stream
        bytesIndex = startBytesIndex;
        length = int.MinValue;
        return int.MinValue;
      }
      while (true) {
        //search for next lf. according to specification the last char before the actual stream content should be lf. But
        //some pdf writers use only a cr before the stream content. Others do it properly and have a cr followed by a lf.
        var c = bytes[bytesIndex++];
        if (c==lf) break;

        if (c==cr) {
          c = bytes[bytesIndex];
          if (c==lf) {
            //lf should follow cr
            bytesIndex++;
          }
          break;
        }
      }

      var streamStartIndex = bytesIndex;
      var lengthToken = dictionaryToken["Length"];
      if (lengthToken is NumberToken lengthNumberToken) {
        //check if length really points to endstream
        length = lengthNumberToken.Integer!.Value;
        var endstreamIndex = bytesIndex + length;
        if (endstreamIndex>bytes.Length-20) {
          dictionaryToken.StreamLengthProblem +=
            $"Pdf content stream: Length {length} points after last byte {bytes.Length} in pdf file." + Environment.NewLine +
            ShowBufferContentAtIndex();
        } else {
          bytesIndex += length;
          SkipWhiteSpace();
          endstreamIndex = bytesIndex;
          if (bytes[bytesIndex++]=='e' &&
            bytes[bytesIndex++]=='n' &&
            bytes[bytesIndex++]=='d' &&
            bytes[bytesIndex++]=='s' &&
            bytes[bytesIndex++]=='t' &&
            bytes[bytesIndex++]=='r' &&
            bytes[bytesIndex++]=='e' &&
            bytes[bytesIndex++]=='a' &&
            bytes[bytesIndex++]=='m') {
            return streamStartIndex;
          }

          //length did not point to endstream
          bytesIndex = endstreamIndex;
          dictionaryToken.StreamLengthProblem +=
            $"Pdf content stream: Length {length} does not point to endstream." + Environment.NewLine +
            ShowBufferContentAtIndex();
          bytesIndex = streamStartIndex;
        }
      }

      //just search endstream
      const string endstream = "endstream";
      var endStreamIndex = 0;
      do {
        var searchByte = bytes[bytesIndex++];
        if (searchByte==endstream[endStreamIndex++]) {
          if (endStreamIndex==endstream.Length) {
            //endstream found
            length = bytesIndex - streamStartIndex - endstream.Length - /*lf*/1;
            if (bytes[bytesIndex-endstream.Length-2]==cr) {
              length--;
            }
            return streamStartIndex;
          }
        } else {
          endStreamIndex = 0;
        }
      } while (true);

      //throw new PdfException($"'endstream' could not be found after the stream bytes. '{(char)bytes[bytesIndex - 1]}' was found instead.", this);
    }


    internal enum ErrorEnum {
      Bool,
      Number,
    }


    static readonly string[] errorMessages = {
      "Bool format error",
      "Integer format error",
    };


    /// <summary>
    /// throws an exception if the current reading position is not a delimiter or white space character
    /// </summary>
    internal void ValidateDelimiter(ErrorEnum errorEnum) {
      var b = bytes[bytesIndex];
      for (int whiteCharBytesIndex = 0; whiteCharBytesIndex < whiteCharBytes.Length; whiteCharBytesIndex++) {
        if (b==whiteCharBytes[whiteCharBytesIndex]) return;

      }
      for (int delimiterBytesIndex = 0; delimiterBytesIndex < delimiterBytes.Length; delimiterBytesIndex++) {
        if (b==delimiterBytes[delimiterBytesIndex]) return;

      }

      throw new PdfException(errorMessages[(int)errorEnum] + $": Character after token should be a delimiter but was '{(char)b}'.", this);
    }
    #endregion


    #region Stream Methods
    //      --------------

    public (DictionaryToken?, ReadOnlyMemory<byte>?)? GetStream(ObjectId objectId) {
      var token = GetToken(objectId);
      if (token is NullToken) {
        //couldn't find in xref. Search in bytes
        var searchText = $"{objectId.ObjectNumber} {objectId.Generation} obj";
        var searchIndex = 0;
        var c = searchText[0];
        for (bytesIndex = 0; bytesIndex < bytes.Length; bytesIndex++) {
          var b = bytes[bytesIndex];
          if (b==c) {
            searchIndex++;
            if (searchIndex==searchText.Length) {
              //object found
              bytesIndex++;
              pdfXRefTable.Remove(objectId);//remove NullToken
              var dictionaryToken2 = new DictionaryToken(this, objectId);
              dictionaryToken2.GetStreamBytes();
              return (null, streamBytes);
            }
            c = searchText[searchIndex];
          } else {
            if (searchIndex!=0) {
              searchIndex = 0;
              c = searchText[searchIndex];
            }
          }
        }
        return null;

      }
      if (token is DictionaryToken dictionaryToken) {
        if (dictionaryToken.StreamLengthProblem is not null) {
          //something is wrong with stream length. Return dictionaryToken so that caller can display StreamLengthProblem
          return (dictionaryToken, null);
        }
        dictionaryToken.GetStreamBytes();
        return (dictionaryToken, streamBytes);
      }

      return null;
    }


    public enum FilterEnum {
      None,
      FlateDecode
    }

    ReadOnlyMemory<byte> streamBytes;
    int streamIndex;


    //private void traceBytes(byte[] bytes, int streamStartIndex, int length) {
    //  StringBuilder.Clear();
    //  for (int bytesIndex = streamStartIndex; bytesIndex < streamStartIndex+length; bytesIndex++) {
    //    StringBuilder.Append(bytes[bytesIndex].ToString() + ' ');
    //  }
    //  System.Diagnostics.Debug.WriteLine(StringBuilder.ToString());
    //}


    internal void FillStreamBytes(DictionaryToken dictionaryToken, FilterEnum filter) {
      bytesIndex = dictionaryToken.StreamStartIndex;
      argumentsStartIndex = int.MinValue;
      if (globalEncryptionKey is not null && !dictionaryToken.IsDecrypted) {
        //decrypt streamBytes
        dictionaryToken.IsDecrypted = true;
        RC4.Encrypt(getRc4EncryptionKey(dictionaryToken.ObjectId!.Value),
          bytes, dictionaryToken.StreamStartIndex, dictionaryToken.Length);
        //////if (dictionaryToken.ObjectId!.Value.ObjectNumber==1290) {
        //////  traceBytes(bytes, dictionaryToken.StreamStartIndex, 32);
        //////  RC4.Encrypt(getRc4EncryptionKey(dictionaryToken.ObjectId!.Value),
        //////    bytes, dictionaryToken.StreamStartIndex, dictionaryToken.Length);
        //////  traceBytes(bytes, dictionaryToken.StreamStartIndex, 32);
        //////  System.Diagnostics.Debugger.Break();
        //////} else {
        //////  RC4.Encrypt(getRc4EncryptionKey(dictionaryToken.ObjectId!.Value),
        //////    bytes, dictionaryToken.StreamStartIndex, dictionaryToken.Length);
        //////}
      }

      switch (filter) {
      case FilterEnum.None:
        streamBytes = new ReadOnlyMemory<byte>(bytes, dictionaryToken.StreamStartIndex, dictionaryToken.Length);
        streamIndex = 0;
        return;

      case FilterEnum.FlateDecode:
        /*
        Drop ZLIB header to get to the FLATE encoded data
        The DeflateStream class can decode a naked FLATE compressed stream (as per RFC 1951) but the content of 
        PDF streams with FlateDecode filter actually is presented in the ZLIB Compressed Data Format (as per 
        RFC 1950) wrapping FLATE compressed data.

        Fortunately it is pretty easy to jump to the FLATE encoded data therein, one simply has to drop the first 
        two bytes. (Strictly speaking there might be a dictionary identifier between them and the FLATE encoded 
        data but this appears to be seldom used.)
        */
        var memoryStream = new MemoryStream(bytes, dictionaryToken.StreamStartIndex+2, dictionaryToken.Length-2);
        var stream = new DeflateStream(memoryStream, CompressionMode.Decompress);
        var bytesCount = stream.Read(workingBuffer, 0, workingBuffer.Length);
        if (bytesCount==workingBuffer.Length) 
          throw new PdfStreamException($"Reading stream, internal {bytesCount} bytes buffer overflow.", this);

        streamBytes = new ReadOnlyMemory<byte>(workingBuffer, 0, bytesCount);
        streamIndex = 0;
        return;

      default:
        throw new NotSupportedException();
      }
    }


    internal string DecryptString(ObjectId objectId, string encryptedString) {
      if (IsDecryptionError) {
        return "Decryption error";
      }

      var dataBytes = new byte[encryptedString.Length];
      for (int byteIndex = 0; byteIndex < encryptedString.Length; byteIndex++) {
        dataBytes[byteIndex] = (byte)encryptedString[byteIndex];
      }
      RC4.Encrypt(getRc4EncryptionKey(objectId), dataBytes, 0, dataBytes.Length);
      return ASCIIEncoding.ASCII.GetString(dataBytes);
    }


    private byte[] getRc4EncryptionKey(ObjectId objectId) {
      /*
      also: https://www.cs.cmu.edu/~dst/Adobe/Gallery/anon21jul01-pdf-encryption.txt
      Algorithm 1: Encryption of data using the RC4 or AES algorithms
      a) Obtain the object number and generation number from the object identifier of the string or stream to be
      encrypted (see 7.3.10, "Indirect Objects"). If the string is a direct object, use the identifier of the indirect
      object containing it.
      b) For all strings and streams without crypt filter specifier; treating the object number and generation number
      as binary integers, extend the original n-byte encryption key to n + 5 bytes by appending the low-order 3
      bytes of the object number and the low-order 2 bytes of the generation number in that order, low-order byte
      first. (n is 5 unless the value of V in the encryption dictionary is greater than 1, in which case n is the value
      of Length divided by 8.)
      If using the AES algorithm, extend the encryption key an additional 4 bytes by adding the value “sAlT”,
      which corresponds to the hexadecimal values 0x73, 0x41, 0x6C, 0x54. (This addition is done for backward
      compatibility and is not intended to provide additional security.)
      c) Initialize the MD5 hash function and pass the result of step (b) as input to this function.
      d) Use the first (n + 5) bytes, up to a maximum of 16, of the output from the MD5 hash as the key for the RC4
      or AES symmetric key algorithms, along with the string or stream data to be encrypted.
      If using the AES algorithm, the Cipher Block Chaining (CBC) mode, which requires an initialization vector,
      is used. The block size parameter is set to 16 bytes, and the initialization vector is a 16-byte random
      number that is stored as the first 16 bytes of the encrypted stream or string.
      The output is the encrypted data to be stored in the PDF file.        */
      var objectHashData = new byte[globalEncryptionKey!.Length + 5];
      var offset = globalEncryptionKey.Length;
      Buffer.BlockCopy(globalEncryptionKey, 0, objectHashData, 0, offset);
      var objectNumber = objectId.ObjectNumber;
      objectHashData[offset++] = (byte)objectNumber;
      objectHashData[offset++] = (byte)(objectNumber>>8);
      objectHashData[offset++] = (byte)(objectNumber>>16);
      var generation = objectId.Generation;
      objectHashData[offset++] = (byte)generation;
      objectHashData[offset++] = (byte)(generation>>8);
      var rc4EncryptionKey = MD5.HashData(objectHashData);
      if (offset<16) {
        return rc4EncryptionKey[0..offset];
      }
      return rc4EncryptionKey;
    }


    public bool SkipStreamWhiteSpace() {
      return skipWhiteSpace(streamBytes.Span);
    }


    private bool skipWhiteSpace(ReadOnlySpan<byte> span) {
      while (true) {
        var isWhiteSpace = false;
        if (streamIndex>=span.Length) {
          return false;
        }
        var b = span[streamIndex];
        while (b=='%') {
          do {
            b = span[++bytesIndex];
          } while (b!=lf && b!=cr);
          if (b==cr && span[streamIndex+1]==lf) {
            streamIndex++;
          }
          b = span[++streamIndex];
        }
        for (int whiteCharBytesIndex = 0; whiteCharBytesIndex < whiteCharBytes.Length; whiteCharBytesIndex++) {
          if (b==whiteCharBytes[whiteCharBytesIndex]) {
            isWhiteSpace = true;
            streamIndex++;
            break;
          }
        }
        if (!isWhiteSpace) return true;

      }
    }


    public void SkipStreamArgument() {
      var span = streamBytes.Span;
      skipWhiteSpace(span);//no check if end of file, because an operator must follow argument
      var b = span[streamIndex++];
      if (b=='(') {
        //skip string
        do {
          streamIndex++;
          var bracketsCount = 1;
          b = span[streamIndex];
          while (true) {
            if (b=='\\') {
              b = span[++streamIndex];
            } else {
              if (b=='(') {
                bracketsCount++;
              } else if (b==')') {
                bracketsCount--;
                if (bracketsCount==0) {
                  break;
                }
              }
            }
            b = span[++streamIndex];
          }
        } while (!IsWhiteSpace(span[streamIndex]));

        streamIndex++;
        return;
      }

      if (b=='<') {
        //skip hex string
        while (span[streamIndex]!='>') {
          streamIndex++;
        }

        streamIndex++;
        return;
      }

      //skip name, numbers and bool
      if (b!='/' && b!='+' && b!='-' && b<'0' && b>'0') {
        if (!(b=='t' && span[streamIndex+1]=='r' && span[streamIndex+1]=='u' && span[streamIndex+1]=='e') &&
          !(b=='f' && span[streamIndex+1]=='a' && span[streamIndex+1]=='l' && span[streamIndex+1]=='s' && span[streamIndex+1]=='e')) 
        {
          throw new PdfStreamException("Content Stream: Argument expected.", this);
        }
      }

      while (!IsWhiteSpace(span[streamIndex])) {
        streamIndex++;
      }
    }


    int argumentsStartIndex;


    private void skipNumber(ReadOnlySpan<byte> span) {
      while (true) {
        var b = span[streamIndex];
        if ((b<'0' || b>'9') && b!='.') {
          return;
        }
        streamIndex++;
      }
    }


    private void skipName(ReadOnlySpan<byte> span) {
      while (true) {
        var b = span[streamIndex];
        if (IsDelimiterByte(b)) {
          return;
        }
        streamIndex++;
      }
    }


    private void skipHexString(ReadOnlySpan<byte> span) {
      byte b;
      do {
        b = span[streamIndex++];
      } while (b!='>');
    }


    private void skipString(ReadOnlySpan<byte> span) {
      var bracketsCount = 1;
      if (streamIndex>=span.Length) return;

      var b = span[streamIndex++];
      while (true) {
        if (b=='\\') {
          streamIndex++;
        } else {
          if (b=='(') {
            bracketsCount++;
          } else if (b==')') {
            bracketsCount--;
            if (bracketsCount==0) {
              break;
            }
          }
        }
        if (streamIndex>=span.Length) return;

        b = span[streamIndex++];
      }
    }


    private void skipArray(ReadOnlySpan<byte> span) {
      while (true) {
        var b = span[streamIndex++];
        if (b==']') {
          return;

        } else if (b=='<') {
          b = span[streamIndex++];
          if (b=='<') {
            skipDictionary(span);
          } else {
            streamIndex--;
            skipHexString(span);
          }

        } else if (b=='[') {
          skipArray(span);

        } else if (b=='(') {
          skipString(span);
        }
      }
    }


    private void skipDictionary(ReadOnlySpan<byte> span) {
      while (true) {
        var b = span[streamIndex++];
        if (b=='<') {
          b = span[streamIndex++];
          if (b=='<') {
            skipDictionary(span);
          } else {
            streamIndex--;
            skipHexString(span);
          }

        } else if (b=='>') {
          b = span[streamIndex++];
          if (b=='>') {
            return;
          } else if (b!='>') {
            throw new PdfStreamException("Stream: expected '>>'.", this);
          }

        } else if (b=='[') {
          skipArray(span);

        } else if (b=='(') {
          skipString(span);
        }
      }
    }


    internal void ContentStreamSkipInlineImage() {
      /*
      BI
      /CS/RGB
      /W 4
      /H 4
      /BPC 8
      /F/Fl
      /DP<</Predictor 15
      /Columns 4
      /Colors 3>>
      ID xœcd@\x2„(œÿÿÿcç\x0\x0iM\x5ÿ
      EI Q
      */
      //note the open bracket '(' after ID which has no closing ')'. 

      var opCode = GetStreamOpCode()!;//cannot return null (end of stream), because opCode DI must follow
      var opCodeSpan = opCode.Value.Span;
      if (opCodeSpan.Length!=2 || opCodeSpan[0]!='I' || opCodeSpan[1]!='D') 
        throw new PdfStreamException("Content Stream: Inline image operator 'BI' should be followed by 'ID'.", this);

      var span = streamBytes.Span;
      while (true) {
        var b = span[streamIndex++];
        if (b=='E' && span[streamIndex]=='I' && IsDelimiterByte(span[streamIndex+1])) {
          if (!IsDelimiterByte(span[streamIndex-2])) {
            System.Diagnostics.Debugger.Break();
          }
          streamIndex++; // skip 'I'
          return;
        }
      }
    }


    internal (decimal x, decimal y, decimal width, decimal height)? ContentStreamGetClippingRegion() {
      //322.8 648.48 31.92001 44.64001 re W n
      var span = streamBytes.Span;
      try {
        if (streamIndex+3>=span.Length || span[streamIndex+1]!='W' || span[streamIndex+3]!='n') return null;

        StartStreamArgumentReading();
        var x = GetStreamNumber();
        var y = GetStreamNumber();
        var width = GetStreamNumber();
        var height = GetStreamNumber();
        EndStreamArgumentReading();
        return (x, y, width, height);
      } catch (Exception ex) {

        return null;
      }
    }


    internal Token GetToken(ObjectId objectId, int streamId, int streamObjectIndex) {
      var tempBytes = bytes;
      var tempBytesIndex = bytesIndex;
      if (!objectStreams.TryGetValue(streamId, out var objectStream)) {
        var objectStreamDictionaryToken = (DictionaryToken)pdfXRefTable[new ObjectId(streamId, 0)];
        if (!objectStreamDictionaryToken.TryGetNumber("First", out var firstNumberToken))
          throw new PdfException("Pdf object stream: 'First' entry is missing.", this);
        if (!objectStreamDictionaryToken.TryGetNumber("N", out var nNumberToken))
          throw new PdfException("Pdf object stream: 'N' entry is missing.", this);
        objectStreamDictionaryToken.GetStreamBytes();
        var objectOffsets = new (int ObjectId, int Offset)[nNumberToken.Integer!.Value];
        for (int objectOffsetsIndex = 0; objectOffsetsIndex<objectOffsets.Length; objectOffsetsIndex++) {
          objectOffsets[objectOffsetsIndex] = (GetStreamInt(), GetStreamInt() + firstNumberToken.Integer!.Value);
        }
        objectStream = (objectOffsets, streamBytes.ToArray());
        objectStreams.Add(streamId, objectStream);
      }
      bytes = objectStream.Bytes;
      (int streamObjectId, int offset) = objectStream.Offsets[streamObjectIndex];
      if (streamObjectId!=objectId.ObjectNumber)
        throw new PdfException($"Pdf object stream: stream {streamId} should contain {objectId.ObjectNumber} at " +
          $"{streamObjectIndex}, but was {streamObjectId}.", this);

      bytesIndex = offset;
      var token = GetNextToken(objectId);
      bytes = tempBytes;
      bytesIndex = tempBytesIndex;
      return token;
    }


    internal void ApplyPredictorUp(int bytesPerRow) {
      var rowCount = streamBytes.Length / (bytesPerRow+1);
      var filteredStreamBytes = new byte[rowCount * bytesPerRow];
      var streamBytesSpan = streamBytes.Span;
      if (streamBytesSpan[0]!=2) throw new PdfStreamException($"Pdf stream, ApplyPredictorUp(): Filter type 2: Up expected, " +
        $"but was {filteredStreamBytes[0]}.", this);
      streamIndex = 1;
      int filteredIndex = 0;
      for (; filteredIndex < bytesPerRow; filteredIndex++) {
        filteredStreamBytes[filteredIndex] = streamBytesSpan[streamIndex++];
      }
      for (int rowIndex = 1; rowIndex < rowCount; rowIndex++) {
        if (streamBytesSpan[streamIndex]!=2) throw new PdfStreamException($"Pdf stream, ApplyPredictorUp(): Filter type 2: Up expected, " +
          $"but was {filteredStreamBytes[streamIndex]}.", this);
        streamIndex++;
        for (int colIndex = 0; colIndex < bytesPerRow; colIndex++) {
          filteredStreamBytes[filteredIndex] = 
            (byte)(filteredStreamBytes[filteredIndex-bytesPerRow] + streamBytesSpan[streamIndex++]);
          filteredIndex++;
        }
      }
      streamBytes = filteredStreamBytes;
      streamIndex = 0;
    }


    public ReadOnlyMemory<byte>? GetStreamOpCode(string? searchCode = null) {
      var span = streamBytes.Span;
      int startOpCode;
      while (true) {
        if (!skipWhiteSpace(span)) return null;

        if (argumentsStartIndex==streamIndex)
          throw new PdfStreamException("Endless loop: Trying to process the same op code again.", this);

        argumentsStartIndex = streamIndex;
        byte b;
        //skip the leading arguments before OpCode
        while (true) {
          b = span[streamIndex++];
          if (b=='/') {
            skipName(span);

          } else if (b=='<') {
            b = span[streamIndex++];
            if (b=='<') {
              skipDictionary(span);
            } else {
              streamIndex--;
              skipHexString(span);
            }

          } else if (b=='(') {
            skipString(span);

          } else if (b=='[') {
            skipArray(span);

          } else if ((b>='0' && b<='9') || b=='-' || b=='.' || b=='+') {
            skipNumber(span);

          } else if (b=='t' && span[streamIndex]=='r' && span[streamIndex+1]=='u' && span[streamIndex+2]=='e') {
            streamIndex +=3;

          } else if (b=='f' && span[streamIndex]=='a' && span[streamIndex+1]=='l' && span[streamIndex+2]=='s' && span[streamIndex+3]=='e') {
            streamIndex +=4;

          } else {
            break;
          }
          if (!skipWhiteSpace(span)) return null;
        }

        streamIndex--;
        startOpCode = streamIndex;
        while (!IsDelimiterByte(b)) {
          streamIndex++;
          if (streamIndex>=span.Length) break;
          b = span[streamIndex];
        }

        if (searchCode==null) {
          break;
        } else {
          if (streamIndex-startOpCode==searchCode.Length) {
            var searchCodeIndex = 0;
            for (; searchCodeIndex<searchCode.Length; searchCodeIndex++) {
              if (searchCode[searchCodeIndex]!=span[startOpCode+searchCodeIndex]) {
                break;
              }
            }
            if (searchCodeIndex==searchCode.Length) break; //correct op code found
          }
        }
      }
      return streamBytes[startOpCode..streamIndex];
    }


    int streamMarkIndex = -1;


    public void SetStreamMark() {
      streamMarkIndex = streamIndex;
    }


    public string GetStreamMarkedText() {
      if (streamMarkIndex<0 || streamMarkIndex>streamIndex) throw new Exception();

      var span = streamBytes.Span;
      StringBuilder.Clear();
      for (int byteIndex = streamMarkIndex; byteIndex < streamIndex; byteIndex++) {
        StringBuilder.Append((char)span[byteIndex]);
      }
      return StringBuilder.ToString();
    }


    public string GetStreamName() {
      StringBuilder.Clear();
      var span = streamBytes.Span;
      var b = span[streamIndex++];
      if (b!='/') throw new PdfStreamException("Stream: Name should have a leading '/'.", this);

      while (true) {
        b = span[streamIndex];
        if (IsDelimiterByte(b)) break;

        StringBuilder.Append((char)b);
        streamIndex++;
      }
      return StringBuilder.ToString();
    }


    int previousStreamIndex;


    public void StartStreamArgumentReading() {
      previousStreamIndex = streamIndex;
      streamIndex = argumentsStartIndex;
    }


    public void EndStreamArgumentReading() {
      streamIndex = previousStreamIndex;
    }


    int streamStartOfTextFragmentIndex;


    public void MarkStreamStartOfTextFragment() {
      streamStartOfTextFragmentIndex = streamIndex;
    }


    public string GetStreamTextFragment() {
      StringBuilder.Clear();
      var endIndex = streamIndex - 2; //remove trailing ET
      while (streamStartOfTextFragmentIndex<endIndex) {
        StringBuilder.Append((char)streamBytes.Span[streamStartOfTextFragmentIndex++]);
      }
      return StringBuilder.ToString();
    }


    public string GetStreamString(PdfFont? font) {
      StringBuilder.Clear();
      getStreamString(streamBytes.Span, font);
      return StringBuilder.ToString();
    }


    private void getStreamString(ReadOnlySpan<byte> span, PdfFont? font) {
      byte b = span[streamIndex++];
      if (b=='<') {
        //read hex string
        b = span[streamIndex++];
        while (b!='>') {
          var charNumber = 0;
          for (int i = 0; i < 2; i++) {
            while (IsWhiteSpace(b)) {
              b = span[streamIndex++];
            }
            if (b>='0' && b<='9') {
              charNumber += b-'0';
            } else if (b>='A' && b<='F') {
              charNumber += b-'A' + 10;
            } else if (b>='a' && b<='f') {
              charNumber += b-'a' + 10;
            } else {
              throw new PdfStreamException("Stream content: Invalid character in Hex string.", this);
            }
            if (i<1) {
              charNumber *= 16;
            }
            b = span[streamIndex++];
          }
          append((char)charNumber, font);
        }

      } else if (b=='(') {
        //read normal string
        var bracketsCount = 1;
        b = span[streamIndex++];
        while (true) {
          if (b=='\\') {
            b = span[streamIndex++];
            if (b>='0' && b<='7') {
              //octal character
              var chNumber = 0;
              var digitsCount = 0;
              while (true) {
                chNumber += b - '0';
                b = span[streamIndex++];
                if (b<'0' || b>'7') break;

                //if (digitsCount++>2) throw new PdfStreamException("More than 3 digits after '/' in stream string.", this);
                if (digitsCount++==2) break;

                chNumber *= 8;
              }
              append((char)chNumber, font);

            } else if (b==0xA) {
              //skip '/' at end of line, followed by line feed
              b = span[streamIndex++];

            } else if (b==0xD) {
              //skip '/' at end of line, followed by carriage return
              b = span[streamIndex++];

            } else {
              var ch = b switch
              {
                (byte)'n' => (char)0xA,
                (byte)'r' => (char)0xD,
                (byte)'t' => (char)0x9,
                (byte)'b' => (char)0x8,
                (byte)'f' => (char)0xC,
                (byte)'(' => '(',
                (byte)')' => ')',
                (byte)'\\' => '\\',
                _ => throw new PdfStreamException("Illegal character after '/' in stream string.", this),
              };
              append(ch, font);
              b = span[streamIndex++];
            }

          } else {
            if (b=='(') {
              bracketsCount++;
            } else if (b==')') {
              bracketsCount--;
              if (bracketsCount==0) {
                break;
              }
            }

            append((char)b, font);
            b = span[streamIndex++];
          }
        }

      } else {
        throw new PdfStreamException("A string in a stream should start with '<' or '('.", this);
      }
    }


    private void append(char ch, PdfFont? font) {
      if (ch>0xff) {
        System.Diagnostics.Debugger.Break();
      }
      if (font?.Encoding8Bit!=null) {
        ch = font.Encoding8Bit[ch];
      }
      StringBuilder.Append(ch);
    }


    public string GetStreamArrayString(PdfFont? font) {
      StringBuilder.Clear();
      var span = streamBytes.Span;
      var b = span[streamIndex++];
      if (b!='[') throw new PdfStreamException("Read string array in stream, '[' expected.", this);

      do {
        b = span[streamIndex++];
        if (b=='(' || b=='<') {
          streamIndex--;
          getStreamString(span, font);
          b = span[streamIndex++];
        }
      } while (b!=']');
      return StringBuilder.ToString();
    }


    public int GetStreamInt() {
      var sign = 1;
      int value = 0;
      var span = streamBytes.Span;
      skipWhiteSpace(span);
      var b = span[streamIndex++];
      if (b=='+') {
        b = span[streamIndex++];
      } else if (b=='-') {
        sign = -1;
        b = span[streamIndex++];
      }
      while (true) {
        if (b>='0' && b<='9') {
          value = 10 * value + b -'0';
        } else {
          break;
        }
        b = span[streamIndex++]; 
      }
      return sign*value;
    }


    public decimal GetStreamNumber() {
      var sign = 1;
      decimal value = 0;
      var divider = 0m;
      var span = streamBytes.Span;
      skipWhiteSpace(span);
      var b = span[streamIndex++];
      if (b=='+') {
        b = span[streamIndex++];
      } else if (b=='-') {
        sign = -1;
        b = span[streamIndex++];
      }
      while (true) {
        if (b>='0' && b<='9') {
          if (divider==0m) {
            //so far integer
            value = 10 * value + b -'0';
          } else {
            //decimal point was found
            value += (b -'0') / divider;
            divider *= 10;
          }
        } else if (b=='.') {
          if (divider!=0) {
            throw new PdfStreamException($"Reading number error: Second decimal point found.", this);
          }
          divider = 10;
        } else {
          break;
        }
        b = span[streamIndex++];
      }
      return sign*value;
    }


    /// <summary>
    /// Reads pdf Character Identifier. Example: <1A> returns 0x1A
    /// </summary>
    public byte GetStreamCid() {
      int value = 0;
      var span = streamBytes.Span;
      skipWhiteSpace(span);
      var b = span[streamIndex++];
      if (b!='<') 
        throw new PdfStreamException("Hexadecimal integer expected in the form of '<1A>', but leading '<' was missing.", this);
      
      b = span[streamIndex++];
      while (true) {
        if (b>='0' && b<='9') {
          value = 16 * value + b -'0';
        } else if (b>='A' && b<='F') {
          value = 16 * value + b -'A' + 10;
        } else if (b>='a' && b<='f') {
          value = 16 * value + b -'a' + 10;
        } else {
          break;
        }
        b = span[streamIndex++];
      }

      if (b!='>')
        throw new PdfStreamException("Character IDentifier CID expected in the form of '<1A>', but leading '<' was missing.", this);

      if (value<0 || value> 0xFF)
        throw new PdfStreamException("Reading Character IDentifier CID from stream, should have only " +
        "2 hex digits.", this);

      return (byte)value;
    }


    /// <summary>
    /// Reads Unicode for CID (pdf Character Identifier). Should only use four bytes.  Example: <89AB> returns 0x89AB
    /// </summary>
    public ushort GetStreamUnicode() {
      int value = 0;
      var span = streamBytes.Span;
      skipWhiteSpace(span);
      var b = span[streamIndex++];
      if (b!='<')
        throw new PdfStreamException("Hexadecimal integer expected in the form of '<89AB>', but leading '<' was missing.", this);

      b = span[streamIndex++];
      var digitsCount = 0;
      while (true) {
        if (b>='0' && b<='9') {
          value = 16 * value + b -'0';
        } else if (b>='A' && b<='F') {
          value = 16 * value + b -'A' + 10;
        } else if (b>='a' && b<='f') {
          value = 16 * value + b -'a' + 10;
        } else {
          break;
        }
        b = span[streamIndex++];
        digitsCount++;
        if (digitsCount>3 && b!='>') {
          //some CIDs can convert to several Unicode characters, like ﬄ to ffl. Pack them back to one Unicode
          if (value=='f') {
            if (b=='0' && span[streamIndex]=='0' && span[streamIndex+1]=='6' && span[streamIndex+2]=='6') {
              //<00660066 found, which is ff
              if (span[streamIndex+3]=='>') {
                value = 'ﬀ';
                streamIndex += 3;
                b = span[streamIndex++];
                break;

              } else if (span[streamIndex+3]=='0' && span[streamIndex+4]=='0' && span[streamIndex+5]=='6'
                && span[streamIndex+6]=='9' && span[streamIndex+7]=='>') 
              {
                //<006600660069 found, which is ffi
                value = 'ﬃ';
                streamIndex += 7;
                b = span[streamIndex++];
                break;
              } else if (span[streamIndex+3]=='0' && span[streamIndex+4]=='0' && span[streamIndex+5]=='6'
                && span[streamIndex+6]=='C' && span[streamIndex+7]=='>') 
              {
                //<00660066006C found, which is ffl
                value = 'ﬄ';
                streamIndex += 7;
                b = span[streamIndex++];
                break;
              }
            }
          }
          throw new PdfStreamException("Reading Unicode for Character IDentifier CID from stream, should have only " +
          "4 hex digits.", this);
        }
      }

      if (b!='>')
        throw new PdfStreamException("Hexadecimal integer expected in the form of '<89AB>', but leading '<' was missing.", this);

      if (value<0 || value> 0xFFFF)
        throw new PdfStreamException($"Illegal hexadecimal integer value '{value:X}'.", this);

      return (ushort)value;
    }


    public string ShowStreamContent() {
      StringBuilder.Clear();
      var span = streamBytes.Span;
      for (int spanIndex = 0; spanIndex<span.Length; spanIndex++) {
        append(StringBuilder, span[spanIndex]);
      }
      return StringBuilder.ToString();
    }


    internal string PdfStreamExceptionMessage(string message) {
      return message + Environment.NewLine + ShowStreamContentAtIndex();
    }


    /// <summary>
    /// Shows the stream content at the present reading position
    /// </summary>
    public string ShowStreamContentAtIndex() {
      var span = streamBytes.Span;
      var displayIndex = Math.Max(0, streamIndex);
      displayIndex = Math.Min(span.Length, displayIndex);
      var startEarlier = Math.Max(0, displayIndex-100);
      var endLater = Math.Min(span.Length, displayIndex+100);
      var sb = new StringBuilder();
      int showIndex = startEarlier;
      for (; showIndex < displayIndex; showIndex++) {
        append(sb, span[showIndex]);
      }
      //sb.AppendLine();
      sb.Append("==>");
      if (showIndex<span.Length) {
        append(sb, span[showIndex++]);
      }
      sb.Append("<==");
      for (; showIndex < endLater; showIndex++) {
        append(sb, span[showIndex]);
      }
      sb.AppendLine();
      return sb.ToString();
    }


    /// <summary>
    /// Used to throw an exception which also shows the file content at the present reading position.
    /// </summary>
    internal PdfStreamException StreamException(string message) {
      return new PdfStreamException(message, this);
    }


    /// <summary>
    /// Used to throw an exception which also shows the file content at the present reading position.
    /// </summary>
    internal PdfStreamException StreamException(string message, Exception innerException) {
      return new PdfStreamException(message, innerException, this);
    }
    #endregion


    #region Other Methods
    //      -------------
    #endregion
  }
}
