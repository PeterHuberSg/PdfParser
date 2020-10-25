//https://resources.infosecinstitute.com/pdf-file-format-basic-structure/
//https://brendanzagaeski.appspot.com/0005.html
//https://www.oreilly.com/library/view/pdf-explained/9781449321581/ch04.html

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
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
    public IReadOnlyDictionary<ObjectId, Token> Tokens => tokens;


    /// <summary>
    /// Shared by tokens to convert pdf file bytes into strings
    /// </summary>
    internal StringBuilder StringBuilder { get; private set; } //used by tokens to produce strings


    /// <summary>
    /// Contains all bytes of the pdf file
    /// </summary>
    public IReadOnlyList<byte> PdfBytes => bytes;
    readonly byte[] bytes;

    /// <summary>
    /// Delimiter to separate 2 content section in a Text string
    /// </summary>
    public string ContentDelimiter { get; }
    #endregion


    #region Constructor
    //      -----------

    int bytesIndex; //points to the byte in bytes which gets presently parsed
    readonly byte[] workingBuffer;


    /// <summary>
    /// If several files need to get parsed, big data structures should be reused. Create workingBuffer and 
    /// stringBuilder once and reuse them for each call of the Tokeniser constructor. If workingBuffer is too
    /// small, an Exception gets raised. If none is provide, a default one gets constructed of 100 kBytes.
    /// </summary>
    public Tokeniser(byte[] pdBfytes, string contentDelimiter = "|", byte[]? workingBuffer = null, StringBuilder? stringBuilder = null) {
      bytes = pdBfytes;
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
          bytes[bytesIndex + 6]!='.'))
        {
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
            //// this code searches for xref based on Linearizion Parameter Dictionary entry T, which unfortunately points to an incomplete xref table.
            ////bytesIndex = ((NumberToken)linearizionParameterDictionaryToken.Tokens["T"]).Integer!.Value;
            //////search backwards for xref
            ////do {
            ////  bytesIndex--;
            ////} while (bytes[bytesIndex]!='x' || bytes[bytesIndex+1]!='r' || bytes[bytesIndex+2]!='e' || bytes[bytesIndex+3]!='f');
            ////xrefIndex = bytesIndex;
            ////return;

            //Linearizion Parameter Dictionary found, serch following xref
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


    Dictionary<ObjectId, int> xrefTable;
    Dictionary<ObjectId, Token> tokens;


    private void readXrefTable() {
      try {
        //read xref table
        bytesIndex = xrefIndex;
        xrefTable = new Dictionary<ObjectId, int>();
        tokens = new Dictionary<ObjectId, Token>();
        do {
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
                  if (!xrefTable.ContainsKey(objectId)) {
                    //add only the newest reference. Since the reading starts at the end of the file, the newest xrefs get read first.
                    xrefTable.Add(objectId, address);
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
                    xrefTable[objectId] = -1;
                  }
                } else {
                  throw new PdfException($"'n' or 'f' missing after ref {address} {generation}.", this);

                }
              }
              SkipWhiteSpace();
            } while (bytes[bytesIndex]!='t');
            verifyXrefTable();

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

              xrefStreamDictionaryToken.GetStreamBytes();
              return;
            } else {
              throw new PdfException("Cannot find cross reference table in pdf file.", this);
            }
          }

          //read trailer:
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
            var trailerDictionary = new DictionaryToken(this, null);
            foreach (var keyValueToken in trailerDictionary) {
              if (keyValueToken.Key!="Size" &&
                keyValueToken.Key!="Prev" &&
                keyValueToken.Key!="XRefStm" &&
                keyValueToken.Key!="ID") 
              {
                if (trailerEntries.TryGetValue(keyValueToken.Key, out var token)) {
                  if (token.GetType()!=keyValueToken.Value.GetType()) 
                    throw new PdfException($"Trailer: Token '{keyValueToken.Value}' for key '{keyValueToken.Key}' " +
                    $"in previous trailer table should be the same as the token '{token}' in the new table.", this);

                  if (keyValueToken.Value.ToString()!=token.ToString()) {
                    throw new PdfException($"Trailer: Token '{ keyValueToken.Value}' for key '{ keyValueToken.Key}' " + 
                      $"in previous trailer table should be the same as the token '{token}' in the new table.", this);
                  } else {
                    //nothing to do, trailerEntries has already that value
                  }
                } else {
                  trailerEntries.Add(keyValueToken.Key, keyValueToken.Value);
                }
              }
            }
            if (trailerDictionary.TryGetValue("Prev", out var previousXrefAddressToken)) {
              bytesIndex = ((NumberToken)previousXrefAddressToken).Integer!.Value;
            } else {
              bytesIndex = -1;
            }
          }
        } while (bytesIndex>=0);

        //there can be several trailers in a pdf file. When the first trailer gets read, it might contain a reference for 
        //an object which has no entry yet in the xrefTable. The trailer has a Prev entry, which links to a previous
        //trailer, which has more xref definitions. The reading of the first trailer will create nullToken for objects
        //that cannot be found. Once all trailers are read, try to replace the nullTokens.
        var trailerEntriesKeys = trailerEntries.Keys.ToArray();
        foreach (var trailerEntriesKey in trailerEntriesKeys) {
          var token = trailerEntries[trailerEntriesKey];
          if (token is NullToken nullToken && nullToken.ObjectId!=null) {
            tokens.Remove(nullToken.ObjectId.Value);
            token = GetReferencedToken(nullToken.ObjectId.Value);
            trailerEntries[trailerEntriesKey] = token;
          }
        }
        //bytesIndex = trailerDictionaryAddress;
        //TrailerDictionary = new DictionaryToken(this, null);

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

      } catch (PdfException) {
        throw;
      } catch (PdfStreamException) {
        throw;
      } catch (Exception ex) {
        throw new PdfException("Error in PdfParser Read Xref Table: " + ex.Message, ex, this);
      }
    }


    private void append(string documentInfo, Token infoToken) {
      var infoDictionary = (DictionaryToken)infoToken;
      foreach (var detailInfoToken in infoDictionary) {
        if (detailInfoToken.Value is StringToken detailInfoStringToken) {
          DocumentInfo += $"{detailInfoToken.Key}: {detailInfoStringToken.Value}; ";
        }
      }
    }


    private void verifyXrefTable() {
      var previousBytesIndex = bytesIndex;
      foreach (var xrefOjectIdAddress in xrefTable) {
        bytesIndex = xrefOjectIdAddress.Value;
        //todo: verify xreftable
      }
      bytesIndex = previousBytesIndex;
    }


    private void readPages(Token pagesToken) {
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
      foreach (var ch in verifyString) {
        if (bytes[bytesIndex++]!=ch) {
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
      var startEarlier = Math.Max(0, bytesIndex-100);
      var endLater = Math.Min(bytes.Length, bytesIndex+100);
      var sb = new StringBuilder();
      int showIndex = startEarlier;
      for (; showIndex < bytesIndex; showIndex++) {
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
    /// Shows the pdf file content at the present reading position
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
      if (b==cr || b==lf || (b>=20 && b<127)) {
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


    /// <summary>
    /// Find the next token (number, string, name, array, dictionary, ...) at present reading location. Skip blank space.
    /// </summary>
    /// <param name="objectId">if token is part of an object definition, objectId is its unique ObjectNumber and Generation
    /// number. </param>
    /// <param name="objectIdExpected">The objectId the new object should have</param>
    public Token GetNextToken(ObjectId? objectId = null, ObjectId? objectIdExpected = null, bool isThrowExceptionWhenError = true) {
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


    private Token processNumber(ObjectId? objectId, ObjectId? objectIdExpected, bool isThrowExceptionWhenError = true) {
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
    internal Token GetReferencedToken(ObjectId objectIdRef) {
      if (tokens!=null && tokens.TryGetValue(objectIdRef, out var existingToken)) {
        return existingToken;
      }
      var previousBytesIndex = bytesIndex;
      if (xrefTable!=null && xrefTable.TryGetValue(objectIdRef, out bytesIndex)) {
        var token = GetNextToken(null, objectIdRef, isThrowExceptionWhenError: true);
        if (!objectIdRef.Equals(token.ObjectId)) {
          throw new PdfException($"Expected to find object {objectIdRef}.", this);
        }
        bytesIndex = previousBytesIndex;
        return token;
      } else {
        //didn't find object with objectIdRef. return nullToken instead
        var token = new NullToken(this, objectIdRef, isErrorNull: true);
        bytesIndex = previousBytesIndex;
        return token;
      }
    }


    /// <summary>
    /// Adds the token to the Tokens dictionary. Gets called when a token is marked as an object in the pdf file. When
    /// the same token is needed in the future, it can be quickly found in the Tokens by its ObjectId.
    /// </summary>
    internal void AddToTokens(Token token) {
      //RefToken is not an c# object, but has the ObjectId of a token. When the RefToken is read from the pdf
      //file, the token it points to is not read yet. But when later the RefToken is used again in a different place,
      //the token is defined. ArrayToken and DictionaryToken will replace the RefToken with the actual token it
      //points to.
      if (!(token is RefToken)) { 
        tokens?.Add(token.ObjectId!.Value, token);
      }
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
      while (bytes[bytesIndex++]!=lf) { }
      var streamStartIndex = bytesIndex;
      var lengthToken = dictionaryToken["Length"];
      if (lengthToken is NullToken) {
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
      }
      length = ((NumberToken)lengthToken).Integer!.Value;
      /*
      Drop ZLIB header to get to the FLATE encoded data
      The DeflateStream class can decode a naked FLATE compressed stream (as per RFC 1951) but the content of 
      PDF streams with FlateDecode filter actually is presented in the ZLIB Compressed Data Format (as per 
      RFC 1950) wrapping FLATE compressed data.

      Fortunately it is pretty easy to jump to the FLATE encoded data therein, one simply has to drop the first 
      two bytes. (Strictly speaking there might be a dictionary identifier between them and the FLATE encoded 
      data but this appears to be seldom used.)
      */
      //MemoryStream memoryStream = new MemoryStream(bytes, bytesIndex+2, length-2);
      //var stream = new DeflateStream(memoryStream, CompressionMode.Decompress);
      //this.StringBuilder.Clear();
      //while (true) {
      //  var b = deflateStream.ReadByte();
      //  if (b<0) break;
      //  this.StringBuilder.Append((char)b);
      //}
      //return this.StringBuilder.ToString();
      bytesIndex += length;
      SkipWhiteSpace();
      //var b = bytes[bytesIndex++];
      //if (b==cr) {
      //  //after cr there must follow a lf according to specification, but in onne file there was only a cr
      //  if (bytes[bytesIndex]==lf) {
      //    bytesIndex++;
      //  }
      //} else if (b==lf) {
      //} else { 
      //  throw Exception($"Stream should have a line feed before 'endstream', but '{(char)b}' was found instead.");
      //}

      if (bytes[bytesIndex++]!='e' ||
        bytes[bytesIndex++]!='n' ||
        bytes[bytesIndex++]!='d' ||
        bytes[bytesIndex++]!='s' ||
        bytes[bytesIndex++]!='t' ||
        bytes[bytesIndex++]!='r' ||
        bytes[bytesIndex++]!='e' ||
        bytes[bytesIndex++]!='a' ||
        bytes[bytesIndex++]!='m') 
      {
        throw new PdfException($"'endstream' could not be found after the stream bytes. '{(char)bytes[bytesIndex - 1]}' was found instead.", this);
      }
      return streamStartIndex;
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

    public (DictionaryToken?, ReadOnlyMemory<byte>)? GetStream(ObjectId objectId) {
      var token = GetReferencedToken(objectId);
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
              tokens.Remove(objectId);//remove NullToken
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


    internal void FillStreamBytes(int streamStartIndex, int length, FilterEnum filter) {
      bytesIndex = streamStartIndex;
      argumentsStartIndex = int.MinValue;
      switch (filter) {
      case FilterEnum.None:
        streamBytes = new ReadOnlyMemory<byte>(bytes, streamStartIndex, length);
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
        var memoryStream = new MemoryStream(bytes, streamStartIndex+2, length-2);
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


    //internal (char, char?)? GetStreamOperator() {
    //  var span = streamBytes.Span;
    //  if (!skipWhiteSpace(span)) return null;

    //  var isChar1 = true;
    //  char char1 = default;
    //  char? char2 = null;
    //  while (true) {
    //    if (streamIndex>=span.Length) break;

    //    var b = span[streamIndex];
    //    if (IsWhiteSpace(b)) break;

    //    if (b<'A' || (b>'Z' && b<'a') || b>'z') throw StreamException("Content Stream: Operator expected.");

    //    if (isChar1) {
    //      isChar1 = false;
    //      char1 = (char)b;
    //    } else if (char2==null) {
    //      char2 = (char)b;
    //    } else {
    //      throw StreamException("Content Stream: Operator has more than 2 characters.");
    //    }
    //    streamIndex++;
    //  }

    //  if (isChar1)
    //    throw StreamException("Content Stream: Could not find operator.");

    //  return (char1, char2);
    //}


    int argumentsStartIndex;


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

          }else if (b=='t' && span[streamIndex]=='r' && span[streamIndex+1]=='u' && span[streamIndex+2]=='e') {
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


    internal void SkipInlineImage() {
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
        ch = font?.Encoding8Bit![ch];
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
