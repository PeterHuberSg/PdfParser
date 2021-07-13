using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace PdfParserLib {


  public abstract class Token {

    public ObjectId? ObjectId { get; }
    //public int Address { get; private set; }


    /// <summary>
    /// c# object created out of Token
    /// </summary>
    public object? PdfObject { get; internal set; }


    protected Token(Tokeniser tokeniser, ObjectId? objectId) {
      ObjectId = objectId;
      //Address = -1;
      if (objectId!=null && !(this is RefToken)) {
        tokeniser.AddToTokens(this);
      }
    }


    protected void AppendTokenOrReference(StringBuilder sb, Token token) {
      if (token.ObjectId.HasValue) {
        sb.Append($"ref {token.ObjectId.Value.ObjectNumber} {token.ObjectId.Value.Generation}");
      } else {
        token.ToStringBuilder(sb);
      }
    }


    public abstract void ToStringBuilder(StringBuilder sb);


    //internal void SetAddress(int address) {
    //  Address = address;
    //}


    protected void AddReference(StringBuilder sb) {
      if (ObjectId!=null) {
        sb.Append($" obj {ObjectId.Value.ObjectNumber} {ObjectId.Value.Generation}");
      }
    }


    public sealed override string ToString() {
      var sb = new StringBuilder();
      ToStringBuilder(sb);
      return sb.ToString();
    }
  }


  public class BoolToken: Token {

    public bool Value { get; }

    public BoolToken(Tokeniser tokeniser, ObjectId? objectId) : base(tokeniser, objectId) {
      //true
      //false
      var b = tokeniser.SkipWhiteSpace();
      if (b=='t') {
        b = tokeniser.GetNextByte();
        if (b=='r') {
          b = tokeniser.GetNextByte();
          if (b=='u') {
            b = tokeniser.GetNextByte();
            if (b=='e') {
              Value = true;
              tokeniser.GetNextByte();
              tokeniser.ValidateDelimiter(Tokeniser.ErrorEnum.Bool);
              return;
            }
          }
        }

      } else if (b=='f') {
        b = tokeniser.GetNextByte();
        if (b=='a') {
          b = tokeniser.GetNextByte();
          if (b=='l') {
            b = tokeniser.GetNextByte();
            if (b=='s') {
              b = tokeniser.GetNextByte();
              if (b=='e') {
                Value = false;
                tokeniser.GetNextByte();
                tokeniser.ValidateDelimiter(Tokeniser.ErrorEnum.Bool);
                return;
              }
            }
          }
        }
      }
      throw tokeniser.Exception($"Bool not valid, should be 'true' or 'false'. Invalid character: {(char)b}");
    }


    public override void ToStringBuilder(StringBuilder sb) {
      sb.Append($"{Value}");
      AddReference(sb);
    }
  }


  public class NumberToken: Token {

    public int? Integer { get; }
    public decimal Decimal { get; }
    public bool HasReferenceFormat { get { return (Integer??-1)>=0; } }

    public NumberToken(Tokeniser tokeniser, ObjectId? objectId) : base(tokeniser, objectId) {
      //+999999
      //32
      //+0
      //0
      //-0
      //-123
      //+123.4
      //34.5
      //34.
      //+.2
      //.1
      //0.0
      //-.002
      //-3.62
      var sign = 1;
      decimal value = 0;
      var divider = 0m;
      var b = tokeniser.SkipWhiteSpace();
      if (b=='+') {
        b = tokeniser.GetNextByte();
      } else if (b=='-') {
        sign = -1;
        b = tokeniser.GetNextByte();
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
            throw tokeniser.Exception($"Reading number error: Second decimal point found.");
          }
          divider = 10;
        } else {
          break;
        }
        b = tokeniser.GetNextByte();
      }
      Decimal = sign * value;
      if (divider<=10) {
        Integer = (int)Decimal;
      }
      tokeniser.ValidateDelimiter(Tokeniser.ErrorEnum.Number);
    }


    public NumberToken(Tokeniser tokeniser, decimal number) : base(tokeniser, objectId: null) {
      Decimal = number;
    }


    public NumberToken(Tokeniser tokeniser, int number): base(tokeniser, objectId: null) {
      Decimal = number;
      Integer = number;
    }


    public override void ToStringBuilder(StringBuilder sb) {
      if (Integer.HasValue) {
        sb.Append($"{Integer}");
      } else {
        sb.Append($"{Decimal}");
      }
      AddReference(sb);
    }
  }


  public class StringToken: Token {


    public string Value { get; private set; }
    public byte[]? HexBytes { get; }


    public StringToken(Tokeniser tokeniser, ObjectId? objectId) : base(tokeniser, objectId) {
      //(this is a string)
      //(a string can be\r\n on 2 lines or more)
      //(a string can contain ()matched brackets)
      //(a string with an unpaired \( bracket needs a slash before the bracket)
      //<E0F0>
      tokeniser.StringBuilder.Clear();
      var b = tokeniser.SkipWhiteSpace();
      if (b=='(') {
        //literal string
        var bracketsCount = 1;
        b = tokeniser.GetNextByte();
        while (true) {
          if (b=='\\') {
            tokeniser.StringBuilder.Append((char)b);
            b = tokeniser.GetNextByte();
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
          tokeniser.StringBuilder.Append((char)b);
          b = tokeniser.GetNextByte();
        }

      } else if (b=='<') {
        //hexadecimal string
        tokeniser.StringBuilder.Append('<');
        b = tokeniser.GetNextByte();
        while (b!='>') {
          tokeniser.StringBuilder.Append((char)b);
          b = tokeniser.GetNextByte();
        }
        tokeniser.StringBuilder.Append('>');
        if (tokeniser.StringBuilder.Length % 2 == 0) {
          HexBytes = new byte[(tokeniser.StringBuilder.Length-2) / 2];
          var sbIndex = 1;
          for (int HexBytesIndex = 0; HexBytesIndex < HexBytes.Length; HexBytesIndex++) {
            var char0 = tokeniser.StringBuilder[sbIndex++];
            var int0 = convertHexChar(char0);
            if (int0<0) {
              HexBytes = null;
              break;
            }
            var char1 = tokeniser.StringBuilder[sbIndex++];
            var int1 = convertHexChar(char1);
            if (int1<0) {
              HexBytes = null;
              break;
            }
            HexBytes[HexBytesIndex] = (byte)(int0 * 16 + int1);
          }
        }

      } else {
        throw tokeniser.Exception($"String format error, '(' or '<' expected as leading character, but was '{(char)b}'.");
      }
      if (tokeniser.IsStringNeedsDecryption) {
        Value = tokeniser.DecryptString(objectId!.Value, tokeniser.StringBuilder.ToString());

      } else {
        Value = tokeniser.StringBuilder.ToString();
      }
      tokeniser.GetNextByte();
      //tokeniser.ValidateDelimiter(Tokeniser.ErrorEnum.String);
    }


    internal void DecryptValue(ObjectId objectId, Tokeniser tokeniser) {
      Value = tokeniser.DecryptString(objectId, Value);
    }


    private int convertHexChar(char char0) {
      if (char0>='0' && char0<='9') {
        return char0-'0';
      } else if (char0>='A' && char0<='F') {
        return 10 + char0-'A';
      } else if (char0>='a' && char0<='f') {
        return 10 + char0-'a';
      } else {
        System.Diagnostics.Debugger.Break();
        return -1;
      }
    }


    public override void ToStringBuilder(StringBuilder sb) {
      sb.Append($"\"{Value}\"");
      AddReference(sb);
    }
  }


  public class NameToken: Token {

    public string Value { get; }


    public NameToken(Tokeniser tokeniser, ObjectId? objectId) : base(tokeniser, objectId) {
      // /Name
      tokeniser.StringBuilder.Clear();
      var b = tokeniser.SkipWhiteSpace();
      if (b!='/') throw tokeniser.Exception($"Name format error: First character should be '/' but was '{(char)b}'");

      b = tokeniser.GetNextByte();
      while (!tokeniser.IsDelimiterByte()) {
        tokeniser.StringBuilder.Append((char)b);
        b = tokeniser.GetNextByte();
      }
      Value = tokeniser.StringBuilder.ToString();
    }


    public override void ToStringBuilder(StringBuilder sb) {
      sb.Append($"/{Value}");
      AddReference(sb);
    }
  }


  public class ArrayToken: Token, IEnumerable<Token>, IReadOnlyList<Token> {


    List<Token> tokens;
    Tokeniser tokeniser;


    public int Count => tokens.Count;

    
    public ArrayToken(Tokeniser tokeniser, ObjectId? objectId) : base(tokeniser, objectId) {
      //[/someName false -0 (string)]
      this.tokeniser = tokeniser;
      tokens = new List<Token>();
      var b = tokeniser.SkipWhiteSpace();
      if (b!='[') throw tokeniser.Exception($"illegal array format, leading character '[' expected but was {(char)b}.");

      b = tokeniser.GetNextByte();
      while (b!=']') {
        var token = tokeniser.GetNextToken(isThrowExceptionWhenError: false);
        if (token!=null) {
          tokens.Add(token);
          b = tokeniser.SkipWhiteSpace();
        } else {
          b = tokeniser.GetByte();
          if (b!=']') {
            throw tokeniser.Exception($"NextToken(): unexpected character '{(char)b}'.");
          }
          //we come here when array is empty but has some whitespace
        }
      }
      tokeniser.GetNextByte();
    }


    public ArrayToken(Tokeniser tokeniser, Token token) : base(tokeniser, objectId: null) {
      this.tokeniser = tokeniser;
      tokens = new List<Token> {
        token
      };
    }


    public Token this[int index] {
      get {
        //var token = tokens[index];
        Token token;
        try {
          token = tokens[index];
        } catch (Exception ex) {

          throw;
        }
        if (token is RefToken refToken) {
          token = tokeniser.GetToken(refToken.ObjectId!.Value);
          tokens[index] = token;
        }
        return token;
      }
    }


    public IEnumerator<Token> GetEnumerator() {
      for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++) {
        yield return this[tokenIndex];
      }
    }


    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }


    public override void ToStringBuilder(StringBuilder sb) {
      if (sb.Length>0 && !(sb.Length>1 && sb[^2]!=Environment.NewLine[0] && sb[^1]!=Environment.NewLine[1])) {
        sb.AppendLine();
      }
      sb.Append('[');
      var isfirst = true;
      foreach (var token in tokens) {
        if (isfirst) {
          isfirst = false;
        } else { 
          sb.Append(' ');
        }
        AppendTokenOrReference(sb, token);
      }
      sb.Append(']');
      AddReference(sb);
      sb.AppendLine();
    }


    internal void Add(Token token) {
      tokens.Add(token);
    }
  }


  public class DictionaryToken: Token, IEnumerable<KeyValuePair<string, Token>> {

    public string? Type { get; }
    public int StreamStartIndex { get; }
    public int Length { get; }
    public bool IsStream { get{ return StreamStartIndex>=0; } }
    public bool IsDecrypted { get; internal set; }
    public string? StreamLengthProblem { get; internal set; }
    Dictionary<string, Token> tokens { get; }
    public IReadOnlyList<string> Keys => keys;

    readonly string[] keys; //GetEnumerator() needs a collection (well, array) which doesn't change. The iterator might change tokens during foreach loop.
    readonly Tokeniser tokeniser;


    public DictionaryToken(Tokeniser tokeniser, ObjectId? objectId) : base(tokeniser, objectId) {
      // <<
      //   /Name1 123
      //   /Name2 [(string) (array) 123]
      //   /Name3 <</subDictionaryName1 123 /subDictionaryName2 true>> 
      //   /Name4 (another string)
      //   /Name5 <112233EE>
      // >>
      this.tokeniser = tokeniser;
      var b = tokeniser.SkipWhiteSpace();
      if (b!='<' || tokeniser.GetNextByte()!='<') 
        throw tokeniser.Exception($"illegal dictionary format, leading characters '<<' expected, but was'{(char)b}{(char)tokeniser.LookaheadByte()}'.");

      //parse key
      tokens = new Dictionary<string, Token>();
      tokeniser.GetNextByte();
      b = tokeniser.SkipWhiteSpace();
      while (b!='>' && tokeniser.LookaheadByte()!='>') {
        if (b!='/') {
          throw tokeniser.Exception($"Invalid dictionary format, '/' expected as leading character for dictionary key name, but was {(char)b}.");
        }
        var key = new NameToken(tokeniser, null);
        var value = tokeniser.GetNextToken();
        if (key.Value=="Type" && value is NameToken typeNameToken) {
          Type = typeNameToken.Value;
        }
        if (tokens.TryGetValue(key.Value, out var existingToken)) {
          if (existingToken is ArrayToken existingArrayToken) {
            existingArrayToken.Add(value);
          } else {
            tokens[key.Value] = new ArrayToken(tokeniser, existingToken) {
              value
            };
          }
        } else {
          tokens.Add(key.Value, value);
        }
        b = tokeniser.SkipWhiteSpace();
      }
      tokeniser.GetNextByte();
      if (tokeniser.IsEndOfBuffer()) {
        StreamStartIndex = int.MinValue;
        Length = int.MinValue;
      } else {
        tokeniser.GetNextByte();
        StreamStartIndex = tokeniser.GetStreamStartIndex(this, out var length);
        Length = length;
      }
      keys = tokens.Keys.ToArray();
    }


    public Token this[string key] {
      get {
        var token = tokens[key];
        if (token is RefToken refToken) {
          token = tokeniser.GetToken(refToken.ObjectId!.Value);
          tokens[key] = token;
        }
        return token;
      }
    }


    public bool ContainsKey(string key) {
      return tokens.ContainsKey(key);
    }


    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Token token) {
      if (!tokens.ContainsKey(key)) {
        token = null;
        return false;
      }
      token = this[key];
      return true;
    }


    public bool TryGetName(string key, [MaybeNullWhen(false)] out string name) {
      if (!tokens.ContainsKey(key)) {
        name = null;
        return false;
      }

      name = (this[key] as NameToken)?.Value;
      if (name is null) {
        return false;
      }
      return true;
    }


    public bool TryGetNumber(string key, [MaybeNullWhen(false)] out NumberToken numberToken) {
      if (!tokens.ContainsKey(key)) {
        numberToken = null;
        return false;
      }

      numberToken = this[key] as NumberToken;
      if (numberToken is null) {
        return false;
      }
      return true;
    }


    public bool TryGetArray(string key, [MaybeNullWhen(false)] out ArrayToken token) {
      if (!tokens.ContainsKey(key)) {
        token = null;
        return false;
      }

      token = this[key] as ArrayToken;
      if (token is null) {
        return false;
      }
      return true;
    }


    public bool TryGetDictionary(string key, [MaybeNullWhen(false)] out DictionaryToken token) {
      if (!tokens.ContainsKey(key)) {
        token = null;
        return false;
      }

      token = this[key] as DictionaryToken;
      if (token is null) {
        return false;
      }
      return true;
    }


    public bool TryGetString(string key, [MaybeNullWhen(false)] out string stringValue) {
      if (!tokens.ContainsKey(key)) {
        stringValue = null;
        return false;
      }

      var stringToken = this[key] as StringToken;
      if (stringToken is null) {
        stringValue = null;
        return false;
      }
      stringValue = stringToken.Value;
      return true;
    }


    public bool TryGetHexBytes(string key, [MaybeNullWhen(false)] out byte[] hexBytes) {
      if (!tokens.ContainsKey(key)) {
        hexBytes = null;
        return false;
      }

      var stringToken = this[key] as StringToken;
      if (stringToken is null || stringToken.HexBytes is null) {
        hexBytes = null;
        return false;
      }

      hexBytes = stringToken.HexBytes;
      return true;
    }


    public IEnumerator<KeyValuePair<string, Token>> GetEnumerator() {
      foreach (var key in keys) {
        yield return KeyValuePair.Create(key, this[key]);
      }
    }


    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }


    public Tokeniser GetStreamBytes() {
      if (!IsStream) throw new Exception($"'{this}' is not a stream.");

      Tokeniser.FilterEnum filter;
      if (tokens.TryGetValue("Filter", out var filterToken)){
        if ((filterToken is ArrayToken filterArrayToken)) {
          if (filterArrayToken.Count!=1) {
            System.Diagnostics.Debugger.Break();
          }
          filterToken = filterArrayToken[0];
        }
        var filterString = ((NameToken)filterToken).Value;
        if (filterString=="") {
          filter = Tokeniser.FilterEnum.None;
        } else if (filterString=="FlateDecode") {
          filter = Tokeniser.FilterEnum.FlateDecode;
        } else {
          throw new NotSupportedException($"Stream filter {filterString} is not (yet) supported.");
        }
      } else {
        filter = Tokeniser.FilterEnum.None;
      }
      tokeniser.FillStreamBytes(this, filter);

      //translate stream with predictor if necessary
      if (TryGetDictionary("DecodeParms", out var decodeParmsDictionaryToken)) {

        if (!decodeParmsDictionaryToken.TryGetNumber("Columns", out var columnsNumber)) {
          throw tokeniser.Exception($"Stream DecodeParms are missing Columns parameter.");
        }

        if (!decodeParmsDictionaryToken.TryGetNumber("Predictor", out var predictorNumber)) {
          throw tokeniser.Exception($"Stream DecodeParms are missing Predictor parameter.");
        }
        if (predictorNumber.Integer!.Value!=12) {
          throw tokeniser.Exception($"Stream DecodeParms Predictor parameter should be 12.");
        }

        tokeniser.ApplyPredictorUp(columnsNumber.Integer!.Value);
      }

      return tokeniser;
    }


    public override void ToStringBuilder(StringBuilder sb) {
      if (sb.Length>0 && !(sb.Length>1 && sb[^2]!=Environment.NewLine[0] && sb[^1]!=Environment.NewLine[1])) {
        sb.AppendLine();
      }
      sb.AppendLine(">>");
      foreach (var tokenKeyValuePair in tokens) {
        sb.Append(' ');
        sb.Append('/' + tokenKeyValuePair.Key + ' ');
        AppendTokenOrReference(sb, tokenKeyValuePair.Value);
        sb.AppendLine();
      }
      sb.Append("<<");
      if (IsStream) {
        sb.AppendLine();
        sb.Append($"stream {StreamStartIndex}, {Length} endstream");
        AddReference(sb);
        sb.AppendLine();
      } else {
        AddReference(sb);
        sb.AppendLine();
      }
    }
  }


  public class NullToken: Token {


    public NullToken(Tokeniser tokeniser, ObjectId? objectId, bool isErrorNull=false) : base(tokeniser, objectId) {
      //null
      if (!isErrorNull) {
        var b = tokeniser.SkipWhiteSpace();
        if (b=='n') {
          b = tokeniser.GetNextByte();
          if (b=='u') {
            b = tokeniser.GetNextByte();
            if (b=='l') {
              b = tokeniser.GetNextByte();
              if (b=='l') {
                tokeniser.GetNextByte();
                tokeniser.ValidateDelimiter(Tokeniser.ErrorEnum.Bool);
                return;
              }
            }
          }
        }

        throw tokeniser.Exception($"Null token not valid, should be 'null'. Invalid character: {(char)b}");
      }
    }


    public override void ToStringBuilder(StringBuilder sb) {
      sb.Append($"null");
      AddReference(sb);
    }
  }


  /// <summary>
  /// RefToken is a placeholder for an actual object which is not read yet. When the RefToken is used for further 
  /// processing, the object is defined. ArrayToken and DictionaryToken will replace the RefToken with the actual token it
  /// points to.
  /// </summary>
  public class RefToken: Token {

    public RefToken(Tokeniser tokeniser, ObjectId? objectId, bool isErrorNull = false) : base(tokeniser, objectId) {
    }


    public override void ToStringBuilder(StringBuilder sb) {
      sb.Append("Ref ");
      AddReference(sb);
    }
  }

}
