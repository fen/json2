using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using static Json;
using static System.Console;

unsafe class Program {
  static void Parse() 
  {
    var data = new byte[50 * 1024 * 1024];
    var gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
    var nativePointer = gcHandle.AddrOfPinnedObject();
    var tokens = new JToken[669_562];

    foreach (var file in Directory.GetFiles(@"c:\datafiles")) {
      using (var stream = File.OpenRead(file)) {

        using (var dest = new MemoryStream(data))
          stream.CopyTo(dest);

        var json = new utf8((byte*)nativePointer.ToPointer(), (int)stream.Length);

        for (int i = 0; i < 20; i++) {
          var sw = new Stopwatch();
          sw.Start();

          int count = 0;
          fixed (JToken* ptr = tokens) {
            count = ParseJson(json, ptr, tokens.Length);
          }

          sw.Stop();


          //WriteLine($"count :: {count:n0}");
          //WriteLine($"elapsed :: {sw.Elapsed}");
        }
      }
    }
  }

  static unsafe void Main(string[] args) 
  {
    var sw = new Stopwatch();
    sw.Start();
    Parse();
    sw.Stop();
    WriteLine($"elapsed :: {sw.Elapsed}");
      // object 
      //   string 'softTypes'
      //   array
      //     object
      //       string $id
      //       string 'DesignPart'
      //       string 'instances'
      //       array
      //        object
      //          string '$id'
      //          string '1'
      //   
#if false
      sw.Restart();

      ensure(tokens[0].Type == JType.Object);

      WriteLine($"property :: {tokens[1].GetString(json)}");
      ensure(tokens[2].Type == JType.Array);

      int softTypesArray = 2;

      int softType = -1;
      for (int i = 0; i < tokens.Length; i++) {
        if (tokens[i].Parent == softTypesArray && tokens[i].Type == JType.Object) {
          softType = i;
        }

        if (softType != -1) {
          // key
          WriteLine($"{tokens[softType + 1].GetString(json)}");
          // value
          WriteLine($"{tokens[softType + 2].GetString(json)}");

          // key
          WriteLine($"{tokens[softType + 3].GetString(json)}");
          WriteLine($"size :: {tokens[softType + 4].Size}");

          int arr = softType + 4;
          int j;
          for (j = arr + 1; j < tokens.Length; j++) {
            // instance
            if (tokens[j].Parent == arr && tokens[j].Type == JType.Object) {
              // $id ignore
              j += 1;
              // value
              WriteLine($"{tokens[j+1].GetString(json)}");
            }
          }
          i = j;

          softType = -1;
        }


      }
      /*for (int i = 0; i < 100; i++) {
        WriteLine($"token :: {tokens[i].Type}, start :: {tokens[i].Start}, end :: {tokens[i].End}, size :: {tokens[i].Size}");
      }*/

    }
#endif

    //ReadKey();
  }

  static void PrintToken(JToken token)
    => WriteLine($"token :: {token.Type}, start :: {token.Start}, end :: {token.End}, size :: {token.Size}");

  static void ensure(bool condition) {
    if (condition == false) throw new System.Exception("not ensured");
  }
}
