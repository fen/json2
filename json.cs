using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;

using static System.Console;

public static unsafe class Json {
  public enum JType : byte {
    Undefined,
    Object,
    Array,
    String,
    Primitive,
    /*
    Number,
    Boolean,
    Null
    */
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct JToken {
    public JType    Type;
    public int      Start;
    public int      End;
    public int      Size; 
    public int      Parent;
  }

  [StructLayout(LayoutKind.Sequential)]
  struct JParser {
    public int    Position;
    public int    SuperToken;
    public int    NextToken;
    public int    Count;
  }

  const byte ObjectOpen     = (byte)'{';
  const byte ObjectClose    = (byte)'}';
  const byte ArrayOpen      = (byte)'[';
  const byte ArrayClose     = (byte)']';
  const byte DoubleQuote    = (byte)'"';
  const byte Comma          = (byte)',';
  const byte Colon          = (byte)':';
  const byte BackSlash      = (byte)'\\';
  const byte LF             = (byte)'\n';
  const byte CR             = (byte)'\r';
  const byte TAB            = (byte)'\t';
  const byte SPACE          = (byte)' ';

  public static int ParseJson(utf8 json, JToken* tokens, int numberOfTokens) 
  {
    var parser = new JParser {
      Position = 0,
      SuperToken = -1,
      NextToken = 0,
      Count = 0
    };

    int count = parser.NextToken;

    for (; parser.Position < json.Length; parser.Position++) {
      byte c = json[parser.Position];
      switch (c) {
        case ObjectOpen:
        case ArrayOpen: {
          count += 1;
          var tok = Allocate(ref parser, tokens, numberOfTokens);
          if (parser.SuperToken != -1)  {
            tokens[parser.SuperToken].Size += 1;
            tok->Parent = parser.SuperToken;
          }
          tok->Type = c == ObjectOpen ? JType.Object : JType.Array;
          tok->Start = parser.Position;
          parser.SuperToken = parser.NextToken - 1;
        } break;
        case ObjectClose:
        case ArrayClose: {
          if (parser.NextToken < 1) return 0; //throw new System.Exception("ERROR");
          var type = c == ObjectClose ? JType.Object : JType.Array;
          var tok = &tokens[parser.NextToken - 1];
          for (;;) {
            if (tok->Start != -1 && tok->End == -1) {
              if (tok->Type != type) return 0; //throw new System.Exception("ERROR");
              tok->End = parser.Position + 1;
              parser.SuperToken = tok->Parent;
              break;
            }
            if (tok->Parent == -1) {
              if (tok->Type != type || parser.SuperToken == -1)
                return 0; //throw new System.Exception("ERROR");
            }
            tok = &tokens[tok->Parent];
          }
        } break;

        case DoubleQuote:
          ParseString(ref parser, json, tokens, numberOfTokens);
          count += 1;
          if (parser.SuperToken != -1)
            tokens[parser.SuperToken].Size += 1;
          break;

        case Comma:
          if (parser.SuperToken != -1 && tokens[parser.SuperToken].Type != JType.Array
                                      && tokens[parser.SuperToken].Type != JType.Object) 
          {
            parser.SuperToken = tokens[parser.SuperToken].Parent;
          }
          break;

        case Colon:
          parser.SuperToken = parser.NextToken - 1;
          break;

        case LF:
        case CR:
        case TAB:
        case SPACE:
          break;
        default:
          ParsePrimitive(ref parser, json, tokens, numberOfTokens);
          count += 1;
          if (parser.SuperToken != -1)
            tokens[parser.SuperToken].Size += 1;
          break;
      }
    }

    for (int i = parser.NextToken - 1; i >= 0; i--) {
      if (tokens[i].Start != -1 && tokens[i].End == -1) 
        return 0; //throw new System.Exception("ERROR");
    }

    return count;
  }

  static void ParseString(ref JParser parser, utf8 json, JToken* tokens, int numberOfTokens) 
  {
    int start = parser.Position;

    parser.Position += 1;

    for (; parser.Position < json.Length; parser.Position++) {
      byte c = json[parser.Position];

      // end quote
      if (c == DoubleQuote) {
        var token = Allocate(ref parser, tokens, numberOfTokens);
        Fill(token, JType.String, start+1, parser.Position);
        token->Parent = parser.SuperToken;
        return;
      }

      if (c == BackSlash && parser.Position + 1 < json.Length) {
        parser.Position += 1;
        switch (json[parser.Position]) {
          case (byte)'\"':
          case (byte)'/':
          case (byte)'\\':
          case (byte)'b':
          case (byte)'f':
          case (byte)'r':
          case (byte)'n':
          case (byte)'t':
            break;
          case (byte)'u':
            parser.Position += 1;
            for (int i = 0; i < 4 && parser.Position < json.Length; i++) {
              if ((json[parser.Position] >= 48 && json[parser.Position] <= 58) ||
                  (json[parser.Position] >= 65 && json[parser.Position] <= 70) ||
                  (json[parser.Position] >= 97 && json[parser.Position] <= 102))
              {
                parser.Position = start;
                return; //throw new System.Exception("ERROR");
              }
              parser.Position += 1;
            }
            break;
          default:
            parser.Position = start;
            return; //throw new System.Exception("ERROR");
        }
      }
    }

    parser.Position = start;
    //throw new System.Exception("ERROR");
  }

  static void ParsePrimitive(ref JParser parser, utf8 json, JToken* tokens, int numberOfTokens) 
  {
    int start = parser.Position;

    for (; parser.Position < json.Length; parser.Position++) {
      switch(json[parser.Position]) {
        case TAB:
        case CR:
        case LF:
        case SPACE:
        case Comma:
        case ObjectClose:
        case ArrayClose:
          goto found;
      }
      if (json[parser.Position] < 32 || json[parser.Position] >= 127) {
        parser.Position = start;
        return; //throw new System.Exception("ERROR");
      }
    }

found:
    var token = Allocate(ref parser, tokens, numberOfTokens);
    Fill(token, JType.Primitive, start, parser.Position);
    token->Parent = parser.SuperToken;

    parser.Position -= 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void Fill(JToken* t, JType type, int start, int end) 
  {
    t->Type = type;
    t->Start = start;
    t->End = end;
    t->Size = 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static JToken* Allocate(ref JParser parser, JToken* tokens, int numberOfTokens) 
  {
    if (parser.NextToken >= numberOfTokens) return null;
    var tok = &tokens[parser.NextToken++];
    tok->Start = tok->End = -1;
    tok->Size = 0;
    return tok;
  }

  // ---

  public static utf8 GetString(this JToken self, utf8 json) 
  {
    // @TODO ensure self is string
    return json.Substring(self.Start, self.End - self.Start);
  }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe class utf8 {
  int   _length;
  byte* _ptr;

  public utf8(byte* ptr, int length) {
    _length = length;
    _ptr = ptr;
  }

  public byte this[int i] {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => _ptr[i];
  }

  public int Length {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => _length;
  }

  public utf8 Substring(int start, int length) {
    Contract.Assert(_length >= start + length);
    Contract.Assert(start >= 0);
    Contract.Assert(length >= 0);
    if (length > 0) {
        var sc = this[start] & 0b1100_0000;
        Contract.Assert(sc == 0b1100_0000 || sc <= 0b0111_1111, "tried to take a substring in the middle of a character");
    }

    return new utf8(_ptr + start, length);
  }

  public override string ToString() {
    if (_ptr == null) return string.Empty;
    return Encoding.UTF8.GetString(_ptr, _length);
  }
}
