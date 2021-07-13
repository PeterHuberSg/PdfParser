using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
//using System.Diagnostics.CodeAnalysis;
using System.Text;


namespace PdfParserLib {


  public class PdfXRefTable: IEnumerable<Token>, IReadOnlyDictionary<ObjectId, Token> {

    #region Properties
    //      ----------

    public IReadOnlyDictionary<ObjectId, Token> Tokens => tokens;
    
    
    public int Count => tokens.Count;


    public IEnumerable<ObjectId> Keys => tokens.Keys;


    public IEnumerable<Token> Values => tokens.Values;


    public Token this[ObjectId objectId] {
      get {
        if (tokens.TryGetValue(objectId, out var existingToken)) {
          return existingToken;
        }

        if (addresses.TryGetValue(objectId, out var address)) {
          if (address.IsAddress) {
            return tokeniser.GetToken(objectId, address.Address);
          } else {
            return tokeniser.GetToken(objectId, address.StreamId, address.StreamObjectIndex);
          }

        } else {
          //didn't find object with objectIdRef. Return nullToken instead
          var returnToken = new NullToken(tokeniser, objectId, isErrorNull: true);
          return returnToken;
        }
      }
    }
    #endregion


    #region Constructor
    //      -----------

    readonly Tokeniser tokeniser;
    readonly Dictionary<ObjectId, PdfObjectAddress> addresses;
    readonly Dictionary<ObjectId, Token> tokens;


    public PdfXRefTable(Tokeniser tokeniser) {
      this.tokeniser = tokeniser;
      addresses = new Dictionary<ObjectId, PdfObjectAddress>();
      tokens = new Dictionary<ObjectId, Token>();
    }
    #endregion


    #region Methods
    //      -------

    public void Add(ObjectId objectId, int address) {
      //if there is already an address for objectId, do not overwrite it. The first one written is actually in the
      //latest XRef and therefore the one to be used.
      addresses.TryAdd(objectId, new PdfObjectAddress(address));
    }


    public void Add(ObjectId objectId, int streamId, int streamObjectIndex) {
      //if there is already an address for objectId, do not overwrite it. The first one written is actually in the
      //latest XRef and therefore the one to be used.
      addresses.TryAdd(objectId, new PdfObjectAddress(streamId, streamObjectIndex));
    }


    internal void Add(Token token) {
      if (token is RefToken refToken) {
        throw new Exception($"RefTokens should not get added to PdfXRefTable: '{token}'.");
      }
      tokens.Add(token.ObjectId!.Value, token);
    }


    public bool ContainsKey(ObjectId key) {
      //return !(this[key] is NullToken);
      return addresses.ContainsKey(key);
    }


    public bool TryGetValue(ObjectId key, [MaybeNullWhen(false)] out Token value) {
      var token = this[key];
      if (token is NullToken) {
        value = null;
        return false;
      }
      value = token;
      return true;
    }


    public IEnumerator<Token> GetEnumerator() {
      return tokens.Values.GetEnumerator();
    }


    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }


    IEnumerator<KeyValuePair<ObjectId, Token>> IEnumerable<KeyValuePair<ObjectId, Token>>.GetEnumerator() {
      return tokens.GetEnumerator();
    }


    internal void RemoveAddress(ObjectId objectId) {
      addresses[objectId] = new PdfObjectAddress('\0');
    }

    internal void Remove(ObjectId objectId) {
      tokens.Remove(objectId);
    }
    #endregion
  }


  public struct PdfObjectAddress {
    public int Address { get; }
    public int StreamId { get; }
    public int StreamObjectIndex { get; }

    public readonly bool IsAddress;


    public PdfObjectAddress(char dummy) {
      this.Address = -1;
      this.StreamId = -1;
      this.StreamObjectIndex = -1;
      IsAddress = true;
    }


    public PdfObjectAddress(int address) {
      this.Address = address;
      this.StreamId = -1;
      this.StreamObjectIndex = -1;
      IsAddress = true;
    }

    public PdfObjectAddress(int streamId, int streamOffset) {
      this.Address = -1;
      this.StreamId = streamId;
      this.StreamObjectIndex = streamOffset;
      IsAddress = false;
    }

    public override string ToString() {
      if (IsAddress) {
        return $"Address: {Address}";
      } else {
        return $"StreamId: {StreamId}; StreamObjectIndex: {StreamObjectIndex}";
      }
    }
  }
}