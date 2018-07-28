// copyright (c) 2018-present github.com/jens0

#define release_build
#define parse_attached_doc

#if release_build
#else
#define diagnostic_mode
// #define override_user_options
#endif

namespace program
{

#region using

  using System;
  using static System.Reflection.Assembly;
  using static System.Globalization.NumberStyles;
  using static System.FormattableString;
  using System.IO;
  using System.Xml;
  using System.Collections.Generic;
  using System.Linq;
  using StringExtensionMethods;
  using ChecksumCrc32;

#endregion

  class Program
  {

    #region variables

    #if (override_user_options)
    static void overrideUserOptions()
    {
      user.wants.shorten  = true;
      user.wants.wideEof = true;
      // user.wants.noBuffer = true;
      // user.wants.noFilter = true;
    }
    #endif

    const string helpString = "   url:      https://github.com/jens0/{0}"
    + LF + "   usage:    {0} [commands] [options] [file]"
    + LF + "   commands:"
    + LF + nameof(task.parse) + "        parse matroska file (read-only mode)."
    + LF + nameof(task.write.suid) + " <hex>   write 128-bit hexadecimal to file's suid."
    + LF + nameof(task.write.suid) + " void    void file's suid (overwrite suid)."
    + LF + "   " + nameof(task.parse) + " options:"
    + LF + nameof(task.noCrc) + "        don't validate crc checksum."
    + LF + "   " + nameof(task.write) + " options:"
    + LF + nameof(task.keepDate) + "     keep file's date when writing."
    + LF + "   common options:"
    + LF + nameof(task.noBuffer) + "     don't use buffer when reading file."
    + LF + nameof(task.noFilter) + "     don't use junk filter when reading file."
    + LF + nameof(task.noBroken) + "     don't process broken files."
    + LF + nameof(task.help) + "         show help."
    + LF + nameof(task.advanced) + "     show advanced options.";

    const string moreString = "   display options:"
    + LF + nameof(task.skipVer) + "      skip display of program version."
    + LF + nameof(task.skipPath) + "     skip display of file's path."
    + LF + nameof(task.shorten) + "      shorten display width."
    + LF + nameof(task.skipNote) + "     skip note at eof (end of file)."
    + LF + nameof(task.detailed) + "     show detailed note at eof."
    + LF + "   advanced " + nameof(task.parse) + " options:"
    + LF + nameof(task.noConfig) + "     don't load config.xml."
    + LF + nameof(task.descend) + "      set view=\"descend\" at file level."
    + LF + nameof(task.unhide) + "       disable view=\"hidden\"."
    + LF + nameof(task.fullTop) + "      set reoc=\"full\" for top level elements."
    + LF + nameof(task.fullHigh) + "     set reoc=\"full\" for high level elements."
    + LF + nameof(tasksAka.full) + "         aka '" + nameof(task.fullTop) + " " + nameof(task.fullHigh) + "'."
    + LF + nameof(tasksAka.cached) + "       aka '" + nameof(task.descend) + " " + nameof(task.unhide)
    + " " + nameof(task.fullHigh) + "'."
    + LF + nameof(tasksAka.verbose) + "      aka '" + nameof(task.descend) + " " + nameof(task.unhide)
    + " " + nameof(tasksAka.full) + "'.";

    const string LF = "\n";

    const int
    ID_EOF            = 0x1A,
    ID_EBML           = 0x1A45DFA3, // EBML
    ID_VOID           = 0xEC,       // *\Void
    ID_CRC            = 0xBF,       // *+1\CRC-32
    ID_SEGMENT        = 0x18538067, // SEGMENT
    ID_SEEKHEAD       = 0x114D9B74, // SEGMENT\SeekHead
    ID_SEEK_ID        = 0x53AB,     // SEGMENT\SeekHead\Seek\SeekID
    ID_INFO           = 0x1549A966, // SEGMENT\Info
    ID_SEGMENT_UID    = 0x73A4,     // SEGMENT\Info\SegmentUID
    ID_TIMESTAMPSCALE = 0x2AD7B1,   // SEGMENT\Info\TimestampScale
    ID_DURATION       = 0x4489,     // SEGMENT\Info\Duration
    ID_CLUSTER        = 0x1F43B675, // SEGMENT\Cluster
    ID_TIMESTAMP      = 0xE7,       // SEGMENT\Cluster\Timestamp
    ID_BLOCK_DURATION = 0x9B,       // SEGMENT\Cluster\BlockGroup\Duration
    ID_REF_BLOCK      = 0xFB,       // SEGMENT\Cluster\BlockGroup\RefBlock
    ID_DIS_PADDING    = 0x75A2,     // SEGMENT\Cluster\BlockGroup\Padding
    ID_REF_TIMESTAMP  = 0xCA,       // SEGMENT\Cluster\RefFrame\RefTimestamp
    ID_DFLT_DURATION  = 0x23E383,   // SEGMENT\Tracks\TrackEntry\DfltDuration
    ID_CODEC_DELAY    = 0x56AA,     // SEGMENT\Tracks\TrackEntry\CodecDelay
    ID_SEEK_PRE_ROLL  = 0x56BB,     // SEGMENT\Tracks\TrackEntry\SeekPreRoll
    ID_COMPRESSION    = 0x5034,     // SEGMENT\Tracks\TrackEntry\Encodings\Encoding\Compression
    ID_CUE_TIME       = 0xB3,       // SEGMENT\Cues\CuePoint\CueTime
    ID_CUE_DURATION   = 0xB2,       // SEGMENT\Cues\CuePoint\Positions\CueDuration
    ID_ATTACHED_FILE  = 0x61A7,     // SEGMENT\Attachments\AttachedFile
    ID_FILE_NAME      = 0x466E,     // SEGMENT\Attachments\AttachedFile\FileName
    ID_FILE_MIME_TYPE = 0x4660,     // SEGMENT\Attachments\AttachedFile\FileMimeType
    ID_FILE_DATA      = 0x465C,     // SEGMENT\Attachments\AttachedFile\FileData
    ID_EDITION_ENTRY  = 0x45B9,     // SEGMENT\Chapters\EditionEntry
    ID_TIME_START     = 0x91,       // SEGMENT\Chapters\EditionEntry\ChapterAtom\TimeStart
    ID_TIME_END       = 0x92,       // SEGMENT\Chapters\EditionEntry\ChapterAtom\TimeEnd
    ID_TARGETS        = 0x63C0,     // SEGMENT\Tags\Tag\Targets
    ID_TYPE_VALUE     = 0x68CA,     // SEGMENT\Tags\Tag\Targets\TypeValue
    ID_TAG_EDITN_UID  = 0x63C9,     // SEGMENT\Tags\Tag\Targets\EditionUID
    ID_TAG_CHAP_UID   = 0x63C4,     // SEGMENT\Tags\Tag\Targets\ChapterUID
    ID_DIVX_TIME      = 0x4A10;     // SEGMENT\DivX\Point\Time

    const int TimestampScale_default = 1_000_000;
    static int TimestampScale;
    static (double? asDouble, string asHex) Duration;
    static bool Duration_passed;
    static bool unknownSizedCluster = false;
    static bool enterBroken = false;

    const short high = 2;
    const short top = 1;
    const short root = 0;
    const short allLevel = -1;
    const short undefinedLevel = -2;
    const short adjustedConfig = -3;

    const long unknownSize_shortVINT = 0b_01111111;
    const long unknownSize = 0x00FFFFFFFFFFFFFF; // unique 'unknown size' identifier
    static readonly long[] unknownSizes = // 'unknown size' identifiers for different vint sizes
    {
      unknownSize_shortVINT,
      0b_00111111_11111111,
      0b_00011111_11111111_11111111,
      0b_00001111_11111111_11111111_11111111,
      0b_00000111_11111111_11111111_11111111_11111111,
      0b_00000011_11111111_11111111_11111111_11111111_11111111,
      0b_00000001_11111111_11111111_11111111_11111111_11111111_11111111,
      unknownSize
    };

    // eof note
    static int countClusters = -1;
    static bool reocNoteShown = false;
    static int countWarnings = 0;
    static List<string> warnOffsets = new List<string>();
    static int countCrcMismatches = 0;
    static int countUnknownElements = 0;
    static List<int> elementsInFile = new List<int>();

    // parsed suid data used by write command
    const byte SUID_dataSize = 0x10;
    static bool SUID_ready4write;
    static Guid SUID_data;
    static long SUID_dataOffset;
    static long SUID_elemOffset;
    static bool SUID_equal;

    // parsed crc data used by write command
    const byte CRC_dataSize = 0x04;
    static bool InfoCRC_exists;
    static int InfoCRC_data;
    static long InfoCRC_dataOffset;
    static long InfoCRC_elemOffset;
    static byte[] Info_data; // without crc element
    static long Info_dataOffset;
    static bool CRC_processingFailed;

    ref struct chunk
    {
      public BinaryReader reader;
      public Span<byte> buffer;
      public long dataSize;
    }

#endregion

#region console & error handling

    const int shortLineWidth = 79;
    static string console;
    static bool show = true;

    static void consoleWriteLine(string s = "")
    {
      if (show)
        foreach (string line in (console + s).Split('\n'))
          if (user.wants.shorten && (line.Length > shortLineWidth))
            Console.WriteLine(line.Remove(shortLineWidth).TrimEnd());
          else
            Console.WriteLine(line.TrimEnd());
      console = "";
    }

    static void consoleWriteVersion()
    {
    }

    static void error(string s, int exitCode = 1)
    {
      if (!show)
      {
        console = "";
        show = true;
      }
      consoleWriteLine($"{" ".If((console != "") && !console.EndsWith('\n') && !console.EndsWith(' '))}{{error: {s}}}");
      System.Environment.Exit(exitCode);
    }

    #if (diagnostic_mode)
    static int countDiagnostics = 0;
    static int countNotShownDiagnostics = 0;
    static readonly int[] commonElems =
    {
      #if parse_attached_doc
      0x207273  , 0x207274  , 0x207262  ,
      #endif
      0x44B4    , 0x5387    , 0x2AD1D3  , 0x30A1D9  , 0x4A10    , 0x4A11    , 0x4A12    , 0x4A13    ,
      0x1A45DFA3, 0x4286    , 0x42F7    , 0x42F2    , 0x42F3    , 0x4282    , 0x4287    , 0x4285    ,
      0xEC      , 0xBF      , 0x18538067, 0x114D9B74, 0x4DBB    , 0x53AB    , 0x53AC    , 0x1549A966,
      0x73A4    , 0x2AD7B1  , 0x4489    , 0x4461    , 0x7BA9    , 0x4D80    , 0x5741    , 0x1F43B675,
      0xE7      , 0xA7      , 0xAB      , 0xA3      , 0xA0      , 0xA1      , 0x9B      , 0xFB      ,
      0x75A2    , 0x1654AE6B, 0xAE      , 0xD7      , 0x73C5    , 0x83      , 0xB9      , 0x88      ,
      0x55AA    , 0x9C      , 0x6DE7    , 0x6DF8    , 0x23E383  , 0x23314F  , 0x55EE    , 0x536E    ,
      0x22B59C  , 0x86      , 0x63A2    , 0x258688  , 0xAA      , 0x56AA    , 0x56BB    , 0xE0      ,
      0x9A      , 0x53B8    , 0xB0      , 0xBA      , 0x54B0    , 0x54BA    , 0x54B2    , 0x54B3    ,
      0x55B0    , 0x55B1    , 0x55B7    , 0x55B8    , 0x55B9    , 0x55BA    , 0x55BB    , 0x55BC    ,
      0x55BD    , 0x55D0    , 0x55D1    , 0x55D2    , 0x55D3    , 0x55D4    , 0x55D5    , 0x55D6    ,
      0x55D7    , 0x55D8    , 0x55D9    , 0x55DA    , 0xE1      , 0xB5      , 0x78B5    , 0x9F      ,
      0x6264    , 0xC6      , 0x6D80    , 0x6240    , 0x5034    , 0x4254    , 0x4255    , 0x1C53BB6B,
      0xBB      , 0xB3      , 0xB7      , 0xF7      , 0xF1      , 0xF0      , 0xB2      , 0x1941A469,
      0x61A7    , 0x466E    , 0x4660    , 0x465C    , 0x46AE    , 0x1043A770, 0x45B9    , 0x45BC    ,
      0x45BD    , 0x45DB    , 0x45DD    , 0xB6      , 0x73C4    , 0x91      , 0x92      , 0x98      ,
      0x4598    , 0x80      , 0x85      , 0x437C    , 0x437E    , 0x1254C367, 0x7373    , 0x63C0    ,
      0x68CA    , 0x63CA    , 0x63C5    , 0x67C8    , 0x45A3    , 0x447A    , 0x4484    , 0x4487
    };
    #endif
    static int highestLevel = 0;
    static int countCrcMatches = 0;
    static int countFileReads = 0;
    static int countBufferReads = 0;
    static int countShortVINTs = 0;
    static int countLongVINTs = 0;

    static void pushDiagnostic(string s, bool eof = false)
    {
      #if (diagnostic_mode)
      console += string.Format("{{diag{1}|{0}}}", s.Brace2Bracket(),
      $":{countDiagnostics}{$"[{countNotShownDiagnostics}]".If(countNotShownDiagnostics != 0)}".If(eof));
      if (show)
        countDiagnostics++;
      else
        countNotShownDiagnostics++;
      #endif
    }

#endregion

#region read / push element data

    // read

    static Span<byte> readData(ref chunk data, long preview = 0, bool fill = false)
    {
      long size64 = preview != 0 ? preview : data.dataSize;
      if (size64 > Int32.MaxValue)
        if (!user.wants.noBuffer)
          throw new System.IO.InternalBufferOverflowException("buffer overflow (try again w/ buffer turned off)");
        else
          throw new System.NotSupportedException("element's scope is invalid or not supported");
      int size32 = (int)size64;
      if (data.buffer.IsEmpty)
      {
        countFileReads++;
        Span<byte> buffer = new Span<byte>();
        ref Span<byte> bytes = ref (fill ? ref data.buffer : ref buffer);
        bytes = data.reader.ReadBytes(size32);
        if (bytes.Length != size32)
          throw new System.IO.IOException(
          $"only {bytX(bytes.Length)} bytes of {bytX(size32)} bytes could be read @ {offX(currentOffset)}");
        if (preview != 0)
          data.reader.BaseStream.Seek(-size64, SeekOrigin.Current);
        return bytes;
      }
      else
      {
        countBufferReads++;
        Span<byte> dummy = data.buffer.Slice(0, size32);
        if (preview == 0)
          data.buffer = data.buffer.Slice(size32);
        return dummy;
      }
    }

    static long readInteger(ref chunk data)
    {
      if (data.dataSize > sizeof(long))
        throw new System.IO.InvalidDataException("invalid integer type");
      Span<byte> toGo = readData(ref data);
      long l = 0;
      foreach (byte beit in toGo)
        l = (l << 8) + beit;
      return l;
    }

    static (double? asDouble, string asHex) readFloat(ref chunk data)
    {
      if (data.dataSize != sizeof(float) && data.dataSize != sizeof(double) && data.dataSize != 10)
        throw new System.IO.InvalidDataException("invalid float type");
      Span<byte> toGo = readData(ref data);
      string s = toGo.ToHex();
      if (data.dataSize == 10) // 80-bit float not supported
        return (asDouble: null, asHex: s);
      toGo.Reverse();
      return (asDouble: data.dataSize == sizeof(double) ? BitConverter.ToDouble(toGo) :
        Convert.ToDouble(BitConverter.ToSingle(toGo)), asHex: s);
    }

    static void skipData(ref chunk data)
    {
      if (data.buffer.IsEmpty)
        data.reader.BaseStream.Seek(data.dataSize, SeekOrigin.Current);
      else
        data.buffer = data.buffer.Slice((int)data.dataSize);
    }

    // read & push

    #if parse_attached_doc
    static string readpushreturnString(ref chunk data)
    {
      Span<byte> toGo = readData(ref data);
      (string printable, int quality) text = toGo.ToPrintable(StringExtensionMethods.asciiDataInput);
      pushData(prefix: text.quality == 100 ? ' ' : '~', main: text.printable);
      return text.printable;
    }
    #endif

    static void readpushString(ref chunk data)
    {
      Span<byte> toGo = readData(ref data);
      (string printable, int quality) text = toGo.ToPrintable(StringExtensionMethods.asciiDataInput);
      pushData(prefix: text.quality == 100 ? ' ' : '~', main: text.printable);
    }

    static void readpushUnknown(ref chunk data)
    {
      long allData = data.dataSize;
      long printedData = allData > dataPrintLength ? dataPrintLength : allData;
      long skippedData = allData - printedData;

      if (printedData > 0)
      {
        data.dataSize = printedData;
        Span<byte> toGo = readData(ref data);
        (string printable, int quality) text = toGo.ToPrintable();
        if ((text.printable.Length >= 1 + 4 + 1) && (text.quality >= 75))
          pushData($"[{text.printable}]{"..".If(skippedData > 0)}");
        // else
        //   pushData("<unknown type>");
      }
      // else
      // if (skippedData > 0) // todo: always true since allData != 0
      //   pushData("<unknown type>");

      if (skippedData > 0)
      {
        data.dataSize = skippedData;
        skipData(ref data);
      }

      data.dataSize = allData;
    }

    static void readpushBinary(ref chunk data, int minLength = 0)
    {
      if (data.dataSize <= 16)
      {
        Span<byte> toGo = readData(ref data);
        string s = toGo.ToHex();
        pushData("0x" + (minLength == 0 ? s : s.PadLeft(minLength, '0')));
        if ((minLength > 0) && (minLength < s.Length))
          pushDiagnostic(minLength + "<" + s.Length);
      }
      else
      {
        skipData(ref data);
        pushData("<binary>");
      }
    }

    static Guid readpushSUID(ref chunk data)
    {
      Span<byte> toGo = readData(ref data);
      pushData(toGo.To0xHex());
      (string printable, int quality) text = toGo.ToPrintable();
      if ((text.printable.Length >= 1 + 8 + 1) && (text.quality >= 75))
        pushDiagnostic(text.printable);
      return new Guid(toGo);
    }

    static int readpushCrc(ref chunk data)
    {
      if (data.dataSize != CRC_dataSize)
        throw new System.IO.InvalidDataException("invalid CRC-32 type");
      Span<byte> toGo = readData(ref data);
      int crc = BitConverter.ToInt32(toGo);
      pushData($"0x{crc:X8}");
      return crc;
    }

    static void readpushDate(ref chunk data)
    {
      if (data.dataSize != sizeof(Int64))
        throw new System.IO.InvalidDataException("invalid DateTime type");
      Span<byte> toGo = readData(ref data);
      toGo.Reverse();
      long l = BitConverter.ToInt64(toGo);
      pushData(new DateTime(631139040000000000).AddTicks(l / 100).ToString());
    }

    static void readpushDurationScaled(ref chunk data, bool signed)
    {
      if (data.dataSize > sizeof(long))
        throw new System.IO.InvalidDataException("invalid integer type");
      Span<byte> toGo = readData(ref data);
      (char sign, long l) = (' ', 0);
      if (signed && ((toGo[0] & 0b_10000000) != 0))
        (sign, l) = ('-', ~0);
      foreach (byte beit in toGo)
        l = (l << 8) + beit;
      string s = TimeSpan.FromTicks(l * (TimestampScale / 100)).ToString(@"hh\:mm\:ss\.FFFFFFF");
      pushData(prefix: sign, main: s.PadRight(12, '0'));
    }

    // push

    static void pushDurationUnscaled(long l)
    {
      string s = TimeSpan.FromTicks(l / 100).ToString(@"hh\:mm\:ss\.FFFFFFF");
      pushData(s.PadRight(12, '0'));
    }

    static void pushDuration((double? asDouble, string asHex) data)
    {
      if (data.asDouble.HasValue)
      {
        long l = Convert.ToInt64(data.asDouble.Value * (TimestampScale / 100));
        string s = TimeSpan.FromTicks(l).ToString(@"hh\:mm\:ss\.FFFFFFF");
        pushData(s.PadRight(12, '0'));
        if (/* s.Length - s.LastIndexOf('.') > 4 || */ (data.asHex.Length == 16 && !data.asHex.EndsWith("0000000")))
          pushDiagnostic(data.asHex);
      }
      else
        pushData($"<float type not supported: 0x{data.asHex}>");
    }

    static void pushDurationUnscaledMs(long l)
    {
      string s = TimeSpan.FromMilliseconds(l).ToString(@"hh\:mm\:ss\.fff");
      pushData(s);
    }

    static void pushNanosec(long l, bool fraction)
    {
      string s = l.ToString();
      if (s.Length <= 9)
      {
        s = "0." + s.PadLeft(9, '0').TrimEnd('0').PadRight(3, '0');
        if (fraction)
        {
          double d = 1_000_000_000d / l;
          string g = Invariant($"{d:G}");
          string f = Invariant($"{d:F3}");
          s += $" {(g.Length <= f.Length ? '=' : '~')} 1/{f}";
        }
      }
      else
        s += " ns";
      pushData(s);
    }

    static void pushMapping(ulong key, Dictionary<ulong, string> dict)
    {
      if (dict.ContainsKey(key))
        pushData(dict[key]);
      else
      {
        pushData($"{key}");
        pushDiagnostic("unmapped");
      }
      if (key > Byte.MaxValue)
        pushDiagnostic("long");
    }

    static void pushSeekID(long id, int idSize)
    {
      pushData(elements.ContainsKey((int)id) ? elements[(int)id].name : string.Format("0x{0:X" + (idSize << 1) + "}", id));
      if (idSize > sizeof(int))
        pushDiagnostic("int+");
    }

    static void pushInteger(long l) =>
    pushData($"{l}");

    static void pushPosition(long l) =>
    pushData(offX(l));

    static string offX(long l) =>
    string.Format("[{0:X" + elemOffsetLength + "}]", l);

    static string bytX(long l) =>
    string.Format("0x{0:X" + elemOffsetLength + "}", l);

    static void pushFloat((double? asDouble, string asHex) data) =>
    pushData(data.asDouble.HasValue ? Invariant($"{data.asDouble.Value:F3}") : $"<float type not supported: 0x{data.asHex}>");

    static void pushData(string main, char prefix = ' ') =>
    console += $"{" ".If(!main.StartsWith(StringExtensionMethods.asciiDataInput))}{prefix}{main}";

    #endregion

    #region process master

    const int supportedSubElements = 999_999; // = 10 ^ elemNumberLength - 1
    const byte elemNumberLength = 6;
    const byte elemOffsetLength = 10;
    const byte elemNameLength = 15;
    const char reocNote = '\'';
    const char enterRootMaster = ':';
    const byte dataPrintLength = 28;
    const byte toleratedJunkData = 250; // should be: dataPrintLength <= toleratedJunkData < 0x100
    static byte[] junkData = new byte[dataPrintLength];
    static long currentOffset = 0; // does not equal physical offset if using buffer
    static long rootElement_dataOffset = 0;
    static int countTopElements = 0;
    static bool parseNoteShown = false;
    static bool firstEBML_passed = false;
    static bool firstSEGMENT_passed = false;
    enum nest : byte { enterMaster = (byte)'\\', skipMaster = (byte)'>', no = (byte)'|', done = (byte)'~' };
    enum warn : byte { false_ = (byte)' ', false_adjustedConfig = (byte)'*', true_ = (byte)'+',
    true_enterBroken = (byte)enterRootMaster, true_crcMismatch = (byte)'C', true_badLevel = (byte)'~' };

    static void processMaster(ref chunk element, bool viewDescend, bool isMasterShown,
    int masterID = default, int level = root, bool recursive = false, bool user_wants_full = false)
    {
      int countSubElements = 0, prevElementID = 0;
      long endOfScopeOffset = currentOffset + element.dataSize, elementOffset;
      bool filter = false;
      if (highestLevel < level)
        highestLevel++;
      while ((currentOffset < endOfScopeOffset) && (++countSubElements <= supportedSubElements))
      {
        warn warning = warn.false_;
        show = isMasterShown;
        if (level <= top)
        {
          filter = !user.wants.noFilter;
          if (level == root)
            countTopElements = 0;
          else
          {
            if (countTopElements == 0)
              rootElement_dataOffset = currentOffset;
            countTopElements++;
          }
        }

      nextID:
        elementOffset = currentOffset;
        int elementID = readVINT(ref element);

        if (unknownSizedCluster && elementID >= 0x200000)
        {
          unknownSizedCluster = false;
          element.reader.BaseStream.Seek(elementOffset - currentOffset, SeekOrigin.Current);
          endOfScopeOffset = currentOffset = elementOffset;
          break;
        }

        if (!filter && (/* element.dataSize == 0 || */ element.dataSize == unknownSize))
        { // todo: 'VINT_DATA component set as all zero values or all one values MUST be ignored' ChapterDisplay=0b1_0000000
          string I = "";
          string L = "";
          switch (level)
          {
            case 0: L = "/RL"; break;
            case 1: L = "/TL"; break;
            default: I = "!".PadLeft(level); break;
          }
          pushVINTs($" {I}INVALID ID{L}".ToShortTitleCase(elemNameLength + 2), currentOffset - elementOffset, -1, size.x1x4);
          consoleWriteLineElement(isWarning: true);
          if (currentOffset == endOfScopeOffset)
            break;
          goto nextID;
        }
        filter = false;

        if (level <= top)
        {
          if (level == root)
            switch (elementID)
            {
              case ID_EBML:
                firstEBML_passed = true;
                break;
              case ID_SEGMENT:
                firstSEGMENT_passed = true;
                // file is maybe ebml stream
                TimestampScale = TimestampScale_default;
                Duration_passed = false;
                InfoCRC_exists &= SUID_ready4write;
                CRC_processingFailed &= SUID_ready4write;
                break;
              default:
                if ((elementID != ID_VOID) && !firstSEGMENT_passed)
                  throw new System.IO.InvalidDataException("matroska not found");
                break;
            }
          if (elementID == ID_EOF)
            break;
        }
        else
        if (elementID == ID_EDITION_ENTRY && prevElementID == ID_EDITION_ENTRY)
          pushDiagnostic("multi");

        if (currentOffset == endOfScopeOffset)
          beyondScopeException("element data size");

        readVINT(ref element);

        if (!(
          #if parse_attached_doc
          attachedDoc ? attachedElements[attachedDocName] :
          #endif
          elements).TryGetValue(elementID, out elementProperties elementProp))
        {
          if (!elementsInFile.Contains(elementID))
            countUnknownElements++;
          elementProp = new elementProperties { name = $"<0x{elementID:X}>", level = undefinedLevel, reoc = reoc.hidden };
        }

        if (!elementsInFile.Contains(elementID))
          elementsInFile.Add(elementID);

        string elementName;
        if (elementProp.level == level)
          elementName = elementProp.name;
        else
        {
          if ((elementProp.level >= root) && !((recursive || elementProp.recursive) && (elementProp.level < level)))
            warning = warn.true_badLevel;
          if (elementProp.level == adjustedConfig)
            warning = warn.false_adjustedConfig;
          elementName = elementProp.name.ToShortTitleCase(maxLength: elemNameLength - level + elementProp.occurs1);
        }

        nest nesting;
        if (elementProp.type != type.Master || element.dataSize == 0)
          nesting = nest.no;
        else
        if ((elementProp.view >= view.child || viewDescend) && (warning != warn.true_badLevel))
          nesting = nest.enterMaster;
        else
          nesting = nest.skipMaster;

        if (elementProp.view == view.hidden && !user.wants.unhide)
          show = false;
        else
        if (level == root)
        {
          if (nesting == nest.enterMaster)
            elementName += enterRootMaster;
        }
        else
        if (elementProp.reoc >= reoc.hidden && !user_wants_full)
          if (elementID != prevElementID)
          {
            if (elementName.Length > elemNameLength - level)
              elementName = elementName.Remove(elemNameLength - level);
            elementName += reocNote;
            reocNoteShown |= show;
          }
          else
          {
            show = false;
            if (elementProp.reoc == reoc.skipped && nesting == nest.enterMaster)
              nesting = nest.skipMaster;
          }

        pushVINTs($"{(char)warning}".PadLeft(level) + $"{(char)nesting}".If(level >= high) + elementName,
        currentOffset - elementOffset, element.dataSize, elementProp.size);

        if (element.dataSize == unknownSize)
        {
          switch ((level, elementID))
          {
            case var t when t == (root, ID_SEGMENT): break;
            case var t when t == (top, ID_CLUSTER): unknownSizedCluster = true; nesting = nest.enterMaster; break;
            default: throw new System.NotSupportedException($"unknown-sized element w/ id = 0x{elementID:X} not supported");
          }
          element.dataSize = endOfScopeOffset - currentOffset;
        }

        if ((level, masterID) == (top, ID_SEGMENT))
        {
          if (countClusters == -1)
            countClusters = 0;
          if (elementID == ID_CLUSTER)
            countClusters++;
          else
          if (elementID == ID_SEEKHEAD && elementOffset <= 0x100 && element.dataSize > 0x70 && element.dataSize < 0x200)
            pushDiagnostic($"seek:0x{element.dataSize:X2}");
        }

        long bytesBeyondScope;
        if ((bytesBeyondScope = currentOffset + element.dataSize - endOfScopeOffset) > 0)
        {
          element.dataSize -= bytesBeyondScope;
          if (level == root)
          {
            if (enterBroken = (!user.wants.noBroken && nesting == nest.enterMaster))
              warning = warn.true_enterBroken;
            pushData(string.Format("{{warning: file is broken, element @ {0} is missing {1} bytes{2}}}",
            offX(elementOffset), bytX(bytesBeyondScope), ". skipping rest of file".If(!enterBroken)));
          }
          else
            pushData(string.Format("{{warning: element @ {0} exceeds {1}'s end @ {2} by {3} bytes. skipping rest of {1}}}",
            offX(elementOffset), enterBroken ? "broken file" : "master", offX(endOfScopeOffset), bytX(bytesBeyondScope)));
          if (warning != warn.true_enterBroken)
          {
            skipData(ref element);
            currentOffset += element.dataSize;
            consoleWriteLineElement(isWarning: true);
            break;
          }
        }

        if (nesting == nest.enterMaster)
        {
          consoleWriteLineElement(warning >= warn.true_);
          if (element.buffer.IsEmpty && !user.wants.noBuffer && level != 0 && (!unknownSizedCluster || elementID != ID_CLUSTER))
          {
            readData(ref element, fill: true);
            // // aka
            // element.buffer = readData(ref element);
            // // aka
            // countFileReads++;
            // element.buffer = element.reader.ReadBytes((int)element.dataSize);
          }
          processMaster(ref element, isMasterShown: show, masterID: elementID,
          level: level + 1, recursive: recursive || elementProp.recursive,
          user_wants_full: level == root ? user.wants.fullTop : user.wants.fullHigh,
          viewDescend: viewDescend || elementProp.view == view.descend);
          #if parse_attached_doc
          if (elementID == ID_ATTACHED_FILE)
            attachedDoc = false;
          #endif
        }
        else
        {
          #if parse_attached_doc
          if (attachedDoc)
          {
            {
              {
                switch (elementProp.type)
                {
                  case type.Unknown: readpushUnknown(ref element); break;
                  case type.Binary: readpushBinary(ref element); break;
                  case type.Master: skipData(ref element); break;
                  case type.Uinteger:
                  case type.Integer: pushInteger(readInteger(ref element)); break;
                  case type.Utf8:
                  case type.String: readpushString(ref element); break;
                  case type.Float: pushFloat(readFloat(ref element)); break;
                  case type.Date: readpushDate(ref element); break;
                  default:
                    readpushUnknown(ref element);
                    pushDiagnostic("unreachable");
                    break;
                }
              }
            }
            currentOffset += element.dataSize;
          }
          else
          #endif
          if (element.dataSize != 0)
          {
            bool writeIssue = user.wants.write.suid &&
            (level, masterID) == (high, ID_INFO) && !SUID_ready4write && !CRC_processingFailed;
            switch (elementID)
            {
              case ID_SEGMENT_UID when element.dataSize == SUID_dataSize && writeIssue:
                SUID_data = readpushSUID(ref element);
                if (!user.SUID.HasValue || !(SUID_equal = (SUID_data == user.SUID.Value)))
                {
                  SUID_ready4write = true;
                  SUID_dataOffset = currentOffset;
                  SUID_elemOffset = elementOffset;
                }
                break;
              case ID_CRC when element.dataSize == CRC_dataSize:
                CRC_processingFailed |= writeIssue && InfoCRC_exists || (level, masterID) == (top, ID_SEGMENT);
                int CRC_data = readpushCrc(ref element);
                if (!user.wants.noCrc || writeIssue)
                {
                  if (endOfScopeOffset - currentOffset - CRC_dataSize > Int32.MaxValue)
                  {
                    if (!user.wants.noCrc)
                    {
                      warning = warn.true_crcMismatch;
                      countCrcMismatches++;
                      console += "..UNCHECKED";
                    }
                    CRC_processingFailed |= writeIssue;
                  }
                  else
                  {
                    Span<byte> toGo = readData(ref element, preview: endOfScopeOffset - currentOffset - CRC_dataSize);
                    if (!user.wants.noCrc)
                      if (toGo.Crc() == CRC_data)
                      {
                        countCrcMatches++;
                        console += "..ok";
                      }
                      else
                      {
                        warning = warn.true_crcMismatch;
                        countCrcMismatches++;
                        console += "..FAILED";
                      }
                    if (writeIssue)
                    {
                      InfoCRC_exists = true;
                      InfoCRC_data = CRC_data;
                      InfoCRC_dataOffset = currentOffset;
                      InfoCRC_elemOffset = elementOffset;
                      Info_data = toGo.ToArray();
                      Info_dataOffset = currentOffset + CRC_dataSize;
                    }
                  }
                }
                break;
              case ID_TIMESTAMPSCALE:
                pushNanosec(TimestampScale = (int)readInteger(ref element), false);
                if (Duration_passed && Duration.asDouble.HasValue && (TimestampScale != TimestampScale_default))
                {
                  pushData("->");
                  pushDuration(Duration);
                }
                break;
              case ID_DURATION:
                pushDuration(Duration = readFloat(ref element));
                Duration_passed = true;
                break;
              case ID_SEEK_ID: pushSeekID(readInteger(ref element), (int)element.dataSize); break;
              case ID_SEEK_PRE_ROLL:
              case ID_DFLT_DURATION: pushNanosec(readInteger(ref element), fraction: true); break;
              case ID_DIS_PADDING:
              case ID_CODEC_DELAY: pushNanosec(readInteger(ref element), fraction: false); break;
              case ID_TIME_START:
              case ID_TIME_END: pushDurationUnscaled(readInteger(ref element)); break;
              case ID_CUE_TIME:
              case ID_CUE_DURATION:
              case ID_TIMESTAMP:
              case ID_REF_TIMESTAMP:
              case ID_BLOCK_DURATION: readpushDurationScaled(ref element, signed: false); break;
              case ID_REF_BLOCK: readpushDurationScaled(ref element, signed: true); break;
              case ID_DIVX_TIME: pushDurationUnscaledMs(readInteger(ref element)); break;
              case ID_VOID: skipData(ref element); break;
              default:
                switch (elementProp.type)
                {
                  case type.Unknown: readpushUnknown(ref element); break;
                  case type.Binary: readpushBinary(ref element); break;
                  case type.Master: skipData(ref element); break;
                  case type.Uinteger:
                    switch (elementProp.Uint)
                    {
                      case Uint.dec:
                        if ((element.dataSize > 1) && elementName.EndsWith("UID"))
                          readpushBinary(ref element);
                        else
                        if ((elementID >= ID_TAG_CHAP_UID) && (elementID <= ID_TAG_EDITN_UID))
                        {
                          long l;
                          pushInteger(l = readInteger(ref element));
                          if (l == 0)
                            pushDiagnostic("all");
                        }
                        else
                          pushInteger(readInteger(ref element));
                        break;
                      case Uint.position: pushPosition(readInteger(ref element) + rootElement_dataOffset); break;
                      case Uint.hex6: readpushBinary(ref element, minLength: 6); break;
                      case Uint.hex: readpushBinary(ref element); break;
                      case Uint.map:
                        ulong key;
                        pushMapping(key = (ulong)readInteger(ref element), mappings[elementID]);
                        if ((elementID == ID_TYPE_VALUE) && (key != 50))
                          pushDiagnostic("!default");
                        break;
                      case Uint.hex10: readpushBinary(ref element, minLength: 10); break;
                      default:
                        pushInteger(readInteger(ref element));
                        pushDiagnostic("unreachable");
                        break;
                    }
                    break;
                  case type.Integer: pushInteger(readInteger(ref element)); break;
                  case type.Utf8:
                    #if parse_attached_doc
                    if ((masterID, elementID) == (ID_ATTACHED_FILE, ID_FILE_NAME))
                      attachedDocName = readpushreturnString(ref element);
                    else
                      readpushString(ref element);
                    break;
                    #endif
                  case type.String:
                    #if parse_attached_doc
                    if ((masterID, elementID) == (ID_ATTACHED_FILE, ID_FILE_MIME_TYPE))
                    {
                      if (readpushreturnString(ref element) == "'application/octet-stream'")
                        attachedDoc = attachedElements.ContainsKey(attachedDocName);
                    }
                    else
                    #endif
                      readpushString(ref element); break;
                  case type.Float: pushFloat(readFloat(ref element)); break;
                  case type.Date: readpushDate(ref element); break;
                  default:
                    readpushUnknown(ref element);
                    pushDiagnostic("unreachable");
                    break;
                }
                break;
            }
            currentOffset += element.dataSize;
          }
          else
          {
            switch (elementProp.type)
            {
              case type.Master when elementID == ID_TARGETS: pushData("<all>"); break;
              case type.Uinteger:
              case type.Integer: pushData("0"); pushDiagnostic("zero"); break;
              case type.Utf8:
              case type.String: pushData("<empty>"); break;
              case type.Float: pushData("0.000"); pushDiagnostic("zero"); break;
              default:
                pushData("<empty>");
                if (elementID != ID_COMPRESSION)
                  pushDiagnostic("zero");
                break;
            }
          }
          consoleWriteLineElement(warning >= warn.true_);
        }
        prevElementID = elementID;
      }
      if (currentOffset != endOfScopeOffset)
        if (countSubElements > supportedSubElements)
          throw new System.NotSupportedException("file exceeds supported size");
        else
          throw new System.IO.InvalidDataException($"unexpected error @ level = {level}"+
          $", currentOffset = {offX(currentOffset)}, endOfScopeOffset = {offX(endOfScopeOffset)}" +
          ". this error should not appear. plz report this error to the program's author.");

      int readVINT(ref chunk octets)
      {
        const int octetSize = 1;
        nextVINT:
        int octet = octets.buffer.IsEmpty ? octets.reader.ReadByte() : octets.buffer[0];
        if (filter) // filter junk data at root and top level
        {
          int countJunkData = 0;
          while
          (
            (octet != ID_VOID) &&
            (
              (level == root) && (octet != ID_EOF) &&
              (
                (!firstEBML_passed) || (octet != 0x18) // segment must follow ebml header
              )
              ||
              (level == top) && (octet != ID_CRC) &&
              (
                (masterID == ID_EBML) && (octet != 0x42) // id must start with 0x42
                ||
                (masterID == ID_SEGMENT) && // id must have size of 3 or 4 octets
                (
                  ((octet & 0b_1100_0000) != 0)
                  ||
                  ((octet & 0b_0011_0000) == 0)
                )
              )
            )
          )
          {
            countJunkData++;
            currentOffset++;
            if (countJunkData <= toleratedJunkData)
            {
              if (countJunkData <= dataPrintLength)
                junkData[countJunkData - 1] = (byte)octet;
              if (currentOffset == endOfScopeOffset)
                break;
              octet = octets.reader.ReadByte();
            }
            else
              break;
          }

          if (countJunkData != 0)
          {
            pushVINTs(string.Format(" UNKNOWN DATA/{0}L", level == root ? 'R' : 'T').ToShortTitleCase(elemNameLength + 1),
            countJunkData, -1, size.x1x4);
            int printedJunkData = countJunkData > dataPrintLength ? dataPrintLength : countJunkData;
            (string printable, int quality) text = junkData.AsSpan().Slice(0, printedJunkData).ToPrintable();
            if ((text.printable.Length >= 1 + 4 + 1) && (text.quality >= 75))
              pushData($"[{text.printable}]{"..".If(countJunkData > printedJunkData)}");
            // else
            //   pushData("<unknown type>");
            if (countJunkData > toleratedJunkData)
            {
              if (elementOffset == 0)
              {
                consoleWriteLine();
                throw new System.IO.InvalidDataException("matroska not found");
              }
              octets.dataSize = endOfScopeOffset - currentOffset;
              skipData(ref octets);
              currentOffset += octets.dataSize;
              pushData(string.Format("{{warning: unknown data excess @ {0}. skipping rest of {1}}}",
              offX(elementOffset), level == root ? "file" : "master"));
              consoleWriteLineElement(isWarning: true);
              return ID_EOF;

            }
            consoleWriteLineElement(isWarning: true);
            if ((elementOffset = currentOffset) == endOfScopeOffset)
            {
              if (level == top)
                pushDiagnostic("end@" + offX(elementOffset));
              return ID_EOF;
            }
          }
        }

        currentOffset++;
        if (octet == 0)
        {
          console += $"{nameof(task.parse).If(parseNoteShown ^ (parseNoteShown = true)),-elemNumberLength}{offX(elementOffset)}"
          + " ".PadLeft(level) + $"{{warning: VINT_MARKER not found. skipping invalid VINT}}";
          consoleWriteLineElement(isWarning: true);
          if (!octets.buffer.IsEmpty)
            octets.buffer = octets.buffer.Slice(octetSize);
          elementOffset = currentOffset;
          goto nextVINT;
        }
        int VINT_MARKER = 0b_1_00000000;
        int VINT_WIDTH = 0;
        while ((octet & (VINT_MARKER >>= 1)) == 0)
          VINT_WIDTH++;
        if ((currentOffset += VINT_WIDTH) > endOfScopeOffset)
          beyondScopeException("VINT_DATA");
        if (VINT_WIDTH == 0) // short vint is easier to handle
        {
          countShortVINTs++;
          if ((octets.dataSize = octet ^ VINT_MARKER) == unknownSize_shortVINT)
            octets.dataSize = unknownSize;
          if (!octets.buffer.IsEmpty)
            octets.buffer = octets.buffer.Slice(octetSize);
          return octet;
        }
        countLongVINTs++;
        Span<byte> remainingOctets;
        if (octets.buffer.IsEmpty)
          remainingOctets = octets.reader.ReadBytes(VINT_WIDTH);
        else
        {
          remainingOctets = octets.buffer.Slice(octetSize , VINT_WIDTH);
          octets.buffer = octets.buffer.Slice(octetSize + VINT_WIDTH);
        }
        Span<byte> VINT = stackalloc byte[sizeof(long)];
        remainingOctets.CopyTo(VINT.Slice(sizeof(long) - VINT_WIDTH));
        VINT.Reverse();
        ref byte bigEnd = ref VINT[VINT_WIDTH];
        bigEnd = (byte)(octet ^ VINT_MARKER);
        if ((octets.dataSize = BitConverter.ToInt64(VINT)) == unknownSizes[VINT_WIDTH])
          octets.dataSize = unknownSize;
        bigEnd = (byte)octet;
        return BitConverter.ToInt32(VINT);
      }

      void pushVINTs(string elementName, long VINTsSize, long dataSize, size sizeType)
      {
        bool dataSize_diag = false;
        string elementNumber = $"{countTopElements,elemNumberLength - 1}".If(countTopElements != 0 && dataSize != -1);
        if (!parseNoteShown && show)
        {
          elementNumber = nameof(task.parse);
          parseNoteShown = true;
        }
        string elementSize;
        if ((sizeType == size.x1x4_x6) && (dataSize <= 0xFFFF))
          sizeType = size.x1x4;
        if ((sizeType <= size.x1x10) || (dataSize == unknownSize))
        {
          if (VINTsSize > toleratedJunkData)
            elementSize = "..";
          else
            elementSize = $"{VINTsSize,2:X}";
          if (dataSize >= 0)
            if (dataSize == unknownSize)
              elementSize += "+<unknown size>";
            else
            if (sizeType == size.x1x10)
              elementSize += $"+{dataSize:X10}"; // todo: elemOffsetLength
            else
            {
              elementSize += $"+{dataSize:X4}";
              dataSize_diag = dataSize > 0xFFFF;
            }
        }
        else
        {
          elementSize = $" {VINTsSize + dataSize:X6}";
          dataSize_diag = dataSize > 0xFFFFFF;
        }
        console +=
        string.Format("{0,-" + elemNumberLength + "}[{1:X" + elemOffsetLength + "}]{2,-" + (elemNameLength + 2) + "}{3,-8}",
        elementNumber, elementOffset, elementName, elementSize);
        // if (dataSize_diag /* && elements.ContainsKey(elementID) */)
        //   pushDiagnostic("size");
      }

      void consoleWriteLineElement(bool isWarning)
      {
        const int maxInEof = 15;
        const int maxWarnings = 100;
        if (isWarning)
        {
          if (user.wants.parse)
          {
            if (countWarnings < maxInEof)
              warnOffsets.Add(offX(elementOffset));
            else
            if (countWarnings == maxInEof)
              warnOffsets.Add("and more");
            if (show)
              consoleWriteLine();
            else
            {
              show = true;
              console = skipped() + '\n' + console + '\n' + skipped();
              consoleWriteLine();
              show = false;
            }
          }
          else
            console = "";
          countWarnings++;
          if (countWarnings >= maxWarnings)
            throw new System.ComponentModel.WarningException(
            user.wants.parse ? $"too many warnings ({maxWarnings})" : "bad file");
        }
        else
          consoleWriteLine();
        string skipped() =>
        $"{nameof(task.parse).If(parseNoteShown ^ (parseNoteShown = true)),-elemNumberLength}[{"..]",elemOffsetLength + 1}";
      }

      void beyondScopeException(string s)
      {
        if (level == root)
          throw new System.IO.EndOfStreamException($"broken file ({s})");
        else
          throw new System.IO.InvalidDataException($"{s} exceeds scope");
      }
    }

    static void processFile(chunk file)
    {
      consoleWriteLine(($"{"file",1 - elemNumberLength}{bytX(file.dataSize)} " + (user.wants.skipPath ?
      Path.GetFileName(user.filepath) : user.filepath)).AddShortLineIf(user.wants.help || user.wants.advanced));

      if (user.wants.parse || user.wants.write.suid)
      {
        if (file.dataSize == 0) // todo: minimum size of matroska?
          throw new System.IO.InvalidDataException("matroska not found (empty file)");

        processMaster(ref file, isMasterShown: user.wants.parse, viewDescend: user.wants.descend);

        show = true; // maybe false by "hidden" or "skipped"

        if (user.wants.parse && !user.wants.skipNote)
        {
          List<string> eof = new List<string>();
          if (user.wants.detailed)
            eof.Add(offX(currentOffset) + " " + Path.GetFileName(user.filepath));
          if (countClusters != -1)
            eof.Add(countClusters + " cluster" + "s".If(countClusters > 1));
          if (reocNoteShown)
            eof.Add("'1st");
          if (countWarnings != 0)
            eof.Add(countWarnings + " warning" + "s".If(countWarnings > 1) +
            $" w/ {countCrcMismatches} crc mismatch{"es".If(countCrcMismatches > 1)}".If(countCrcMismatches != 0) +
            " @ " + String.Join(" ", warnOffsets));
          if (countUnknownElements != 0)
            eof.Add(countUnknownElements + " unknown element" + "s".If(countUnknownElements > 1));
          console += $"{"eof",elemNumberLength - 2}{": ".If(eof.Count != 0) + String.Join(", ", eof)}.";

          #if (diagnostic_mode)
          List<string> uncommonElems = new List<string>();
          foreach (int id in elementsInFile)
            if (!commonElems.Contains(id) && elements.ContainsKey(id))
              uncommonElems.Add($"0x{id:X}");
          pushDiagnostic(eof: true, s: string.Format("{0}/{1}<{2}>{3}{4}{5}" /* + "+{6}|{7}:{8}={9}%{10}" */,
          elementsInFile.Count, highestLevel, elements.Count, $"{countUnknownElements}".If(countUnknownElements != 0),
          $"|{countCrcMatches}ok".If(countCrcMatches != 0),
          // countFileReads, countBufferReads,
          // countShortVINTs, countLongVINTs, countShortVINTs * 100 / (countShortVINTs + countLongVINTs),
          $"|{String.Join("?", uncommonElems)}?".If(uncommonElems.Count != 0)));
          #endif

          consoleWriteLine();
        }
      }
      file.reader.Close();
    }

#endregion

#region initialize element properties

    #if parse_attached_doc
    static bool attachedDoc = false;
    static string attachedDocName = "";
    static Dictionary<string, Dictionary<int, elementProperties>> attachedElements =
    new Dictionary<string, Dictionary<int, elementProperties>>();
    #endif

    static Dictionary<int, elementProperties> elements = new Dictionary<int, elementProperties>();

    struct elementProperties
    {
      public string name;
      public short level;
      public short occurs1;
      public bool recursive;
      public type type;
      public reoc reoc;
      public view view;
      public size size;
      public Uint Uint;
    }

    enum type { Unknown, Binary, Master, Uinteger, Integer, Utf8, String, Float, Date };
    enum reoc { full, hidden, skipped };
    enum view { itself, hidden, child, descend };
    enum size { x1x4, x1x10, x6, x1x4_x6 };
    enum Uint { dec, hex, hex6, hex10, position, map };

    static readonly List<string> typeList = new List<string>
    { "binary", "master", "uinteger", "integer", "utf-8", "string", "float", "date" };
    static readonly List<string> reocList = new List<string>
    { "hidden", "skipped" };
    static readonly List<string> viewList = new List<string>
    { "hidden", "child", "descend" };
    static readonly List<string> sizeList = new List<string>
    { "x1+x10", "x6", "x1+x4/x6" };
    static readonly List<string> UintList = new List<string>
    { "hex", "hex6", "hex10", "position" };

    static Dictionary<int, Dictionary<ulong, string>> mappings = new Dictionary<int, Dictionary<ulong, string>>();

    static void loadMapping(XmlDocument doc)
    {
      foreach (XmlNode map in doc.GetElementsByTagName("mapping"))
        if (int.TryParse(map.Attributes["id"]?.Value.Replace("0x", ""), AllowHexSpecifier, null, out int id))
        {
          Dictionary<ulong, string> dic = new Dictionary<ulong, string>();
          foreach (XmlNode Enum in map)
            if ((Enum.Name == "enum") && ulong.TryParse(Enum.Attributes["value"]?.Value, Integer, null, out ulong key))
              dic.Add(key, Enum.Attributes["label"]?.Value ?? "");
          if (mappings.ContainsKey(id))
            mappings.Remove(id);
          mappings.Add(id, dic);
        }
    }

    static void loadRestriction(System.Xml.XmlNode restriction, int id)
    {
      Dictionary<ulong, string> dic = new Dictionary<ulong, string>();
      foreach (XmlNode Enum in restriction)
        if (Enum.Name == "enum" && ulong.TryParse(Enum.Attributes["value"]?.Value, Integer, null, out ulong key))
          dic.Add(key, Enum.Attributes["label"]?.Value ?? "");
      if (dic.Count != 0)
        mappings.Add(id, dic);
    }

    static void loadElementList(XmlDocument doc)
    {
      foreach (XmlNode elementlist in doc.GetElementsByTagName("elementlist"))
        if (elementlist.ChildNodes.Count == 1 && elementlist.FirstChild.Name == "#text" &&
        tryLoadResource(elementlist.InnerText, out XmlDocument docList))
          loadElement(docList.LastChild);
    }

    static bool tryLoadResource(string filename, out XmlDocument doc)
    {
      doc = null;
      string existingFile;
      if ((existingFile = find(filename)) == null)
        return false;
      try {
        (doc = new XmlDocument()).Load(existingFile); } // todo: doc.Schemas.Add
      catch {
        return false; }
      return true;
      string find(string file) =>
      File.Exists(file) ||
      File.Exists(file = Path.Combine(new Uri(Path.GetDirectoryName(GetExecutingAssembly().CodeBase)).LocalPath, file)) ?
      file : null;
    }

    static void loadElement(System.Xml.XmlNode table)
    {
      Dictionary<int, elementProperties> elemDummy = new Dictionary<int, elementProperties>();

      foreach (XmlNode element in table.ChildNodes)
        if (element.Name == "element")
        {
          int id = 0;
          elementProperties prop = new elementProperties { level = undefinedLevel };
          string cppname = "";
          foreach (XmlNode attr in element.Attributes)
            switch (attr.Name)
            {
              case "id":
                int.TryParse(attr.InnerText.Replace("0x", ""), AllowHexSpecifier, null, out id);
                break;
              case "name":
                prop.name = attr.InnerText;
                break;
              case "cppname":
                cppname = attr.InnerText;
                break;
              case "path":
                prop.level = (short)(attr.InnerText.Split('\\').Length - 2);
                break;
              case "level":
                if (short.TryParse(attr.InnerText, Integer, null, out short level) && (level >= allLevel))
                  prop.level = level;
                break;
              case "maxOccurs":
                prop.occurs1 = (short)(attr.InnerText == "1" ? 1 : 0);
                break;
              case "recursive":
                prop.recursive = attr.InnerText == "1";
                break;
              case "type":
                prop.type = (type)(typeList.IndexOf(attr.InnerText) + 1);
                break;
            }
          if (prop.level >= root)
          {
            if (cppname == "")
              cppname = prop.name;
            if ((prop.name = prop.name.ToSpecificCase(length: elemNameLength - prop.level + prop.occurs1)).Length <= 1)
              prop.name = cppname;
          }
          if (id != 0 && !elemDummy.ContainsKey(id))
          {
            elemDummy.Add(id, prop);
            if (element.HasChildNodes && element.LastChild.Name == "restriction")
              loadRestriction(element.LastChild, id);
          }
        }

      if (table.Attributes["attached"] != null)
      {
        #if parse_attached_doc
        string attached = $"'{table.Attributes["attached"].InnerText}'";
        if (!attachedElements.ContainsKey(attached))
          attachedElements.Add(attached, elemDummy);
        #endif
        return;
      }

      foreach ((int id, elementProperties prop) in elemDummy)
        if (!elements.ContainsKey(id))
          elements.Add(id, prop);
    }

    static void loadParse(XmlDocument doc)
    {
      foreach (XmlNode parse in doc.GetElementsByTagName("parse"))
      {
        int id = 0;
        elementProperties prop = new elementProperties();
        foreach (XmlNode attr in parse.Attributes)
          switch (attr.Name)
          {
            case "id":
              int.TryParse(attr.InnerText.Replace("0x", ""), AllowHexSpecifier, null, out id);
              break;
            case "name":
              prop.name = attr.InnerText;
              break;
            case "reoc":
              prop.reoc = (reoc)(reocList.IndexOf(attr.InnerText) + 1);
              break;
            case "view":
              prop.view = (view)(viewList.IndexOf(attr.InnerText) + 1);
              break;
            case "size":
              prop.size = (size)(sizeList.IndexOf(attr.InnerText) + 1);
              break;
            case "uint":
              prop.Uint = (Uint)(UintList.IndexOf(attr.InnerText) + 1);
              break;
          }
        if ((id != 0) && elements.ContainsKey(id))
        {
          elementProperties dummy = elements[id];
          if (prop.name != default) // <parse> overrides <element>
            dummy.name = prop.name;
          dummy.reoc = prop.reoc;
          dummy.view = prop.view;
          dummy.size = prop.size;
          if (prop.Uint != default) // <parse> overrides <mapping>
            dummy.Uint = prop.Uint;
          elements[id] = dummy;
        }
      }
    }

    static void loadConfig()
    {
      if (!tryLoadResource("config.xml", out XmlDocument config))
        return;

      loadElementList(config);

      foreach (XmlNode table in config.GetElementsByTagName("table"))
        loadElement(table);

      loadMapping(config);

      foreach (int id in mappings.Keys)
        if (elements.ContainsKey(id))
        {
          elementProperties dummy = elements[id];
          dummy.Uint = Uint.map;
          elements[id] = dummy;
        }

      loadParse(config);

      foreach (int id in elements.Keys.ToList())
      {
        elementProperties prop = elements[id];
        if (prop.level >= root)
        {
          prop.name = prop.name.ToShortTitleCase(maxLength: elemNameLength - prop.level + prop.occurs1);
          elements[id] = prop;
        }
      }
      // // aka
      // for (int i = 0; i < elements.Count; i++)
      // {
      //   (int id, elementProperties prop) = elements.ElementAt(i);
      //   if (prop.level >= root)
      //   {
      //     prop.name = prop.name.ToShortTitleCase(maxLength: elemNameLength - prop.level);
      //     elements[id] = prop;
      //   }
      // }
      elements.TrimExcess();
      mappings.TrimExcess();
      #if parse_attached_doc
      attachedElements.TrimExcess();
      #endif
    }

    static void adjustConfig(short level, int id, string name)
    {
      if (!elements.ContainsKey(id))
        elements.Add(id, new elementProperties { name = name, level = adjustedConfig, type = type.Master, view = view.child });
      else
      {
        elementProperties dummy = elements[id];
        if (dummy.level != level)
          dummy.level = adjustedConfig;
        if (dummy.type != type.Master)
        {
          dummy.type = type.Master;
          dummy.level = adjustedConfig;
        }
        if (dummy.view < view.child)
        {
          dummy.view = view.child;
          dummy.level = adjustedConfig;
        }
        if (dummy.level == adjustedConfig)
          elements[id] = dummy;
      }
    }

#endregion

#region process command-line arguments

    static userInterface user;

    struct userInterface
    {
      public string filepath;
      public task wants;
      public Guid? SUID;
    }

    struct task
    {
      public struct writ
      {
        public bool suid;
      }
      public bool parse;
      public writ write;
      public bool noCrc;
      public bool keepDate;
      public bool noBuffer;
      public bool noFilter;
      public bool noBroken;
      public bool help;
      public bool advanced;
      public bool version;
      public bool skipVer;
      public bool skipPath;
      public bool shorten;
      public bool skipNote;
      public bool detailed;
      public bool noConfig;
      public bool descend;
      public bool unhide;
      public bool fullTop;
      public bool fullHigh;
    }

    #pragma warning disable CS0649
    struct tasksAka
    {
      public bool full;
      public bool cached;
      public bool verbose;
    }
    #pragma warning restore CS0649

    static userInterface processCommandLineArguments()
    {
      userInterface ui = new userInterface { filepath = "" };
      bool requireArgument = false;
      try
      {
        foreach (string s in Environment.GetCommandLineArgs().Skip(1))
        {
          string sClean = s.ToLower().Replace("-", "").Replace("/", "").Replace(",", "").Replace(" ", "");
          if (requireArgument)
          {
            ui.SUID = hex2SUID(sClean.Replace("{", "").Replace("}", "")
            .Replace("0x", "").Replace("x", "").Replace("$", "").Replace("h", "").Replace("_", ""));
            ui.wants.write.suid = true;
            requireArgument = false;
          }
          else if (sClean == nameof(task.parse).ToLower()) ui.wants.parse = true;
          else if (sClean == nameof(task.write.suid).ToLower()) requireArgument = true;
          else if (sClean == nameof(task.noCrc).ToLower()) ui.wants.noCrc = true;
          else if (sClean == nameof(task.keepDate).ToLower()) ui.wants.keepDate = true;
          else if (sClean == nameof(task.noBuffer).ToLower()) ui.wants.noBuffer = true;
          else if (sClean == nameof(task.noFilter).ToLower()) ui.wants.noFilter = true;
          else if (sClean == nameof(task.noBroken).ToLower()) ui.wants.noBroken = true;
          else if (sClean == nameof(task.help).ToLower()) ui.wants.help = true;
          else if (sClean == nameof(task.advanced).ToLower()) ui.wants.advanced = true;
          else if (sClean == nameof(task.version).ToLower()) ui.wants.version = true;
          else if (sClean == nameof(task.skipVer).ToLower()) { ui.wants.skipVer = true; console = ""; }
          else if (sClean == nameof(task.skipPath).ToLower()) ui.wants.skipPath = true;
          else if (sClean == nameof(task.shorten).ToLower()) ui.wants.shorten = true;
          else if (sClean == nameof(task.skipNote).ToLower()) ui.wants.skipNote = true;
          else if (sClean == nameof(task.detailed).ToLower()) ui.wants.detailed = true;
          else if (sClean == nameof(task.noConfig).ToLower()) ui.wants.noConfig = true;
          else if (sClean == nameof(task.descend).ToLower()) ui.wants.descend = true;
          else if (sClean == nameof(task.unhide).ToLower()) ui.wants.unhide = true;
          else if (sClean == nameof(task.fullTop).ToLower()) ui.wants.fullTop = true;
          else if (sClean == nameof(task.fullHigh).ToLower()) ui.wants.fullHigh = true;
          else if (sClean == nameof(tasksAka.full).ToLower()) ui.wants.fullTop = ui.wants.fullHigh = true;
          else if (sClean == nameof(tasksAka.cached).ToLower())
            ui.wants.descend = ui.wants.unhide = ui.wants.fullHigh = true;
          else if (sClean == nameof(tasksAka.verbose).ToLower())
            ui.wants.descend = ui.wants.unhide = ui.wants.fullTop = ui.wants.fullHigh = true;
          else
          if (ui.filepath == "")
            ui.filepath = s;
          else
            throw new System.ArgumentException($"invalid command/option: '{s}' / taken as file: '{ui.filepath}'");
        }
        if (requireArgument)
          throw new System.ArgumentException(nameof(task.write.suid) + " command requires <hex> or void argument");
      }
      catch (Exception e)
      {
        error("command-line argument: " + e.Message, e.HResult);
      }
      return ui;

      Guid? hex2SUID(string hex)
      {
        const short two = 2;
        if (hex == "void") return null;
        if (hex.Length != SUID_dataSize * two)
          throw new System.FormatException(
          $"<hex> = '{hex.ToUpper()}' is invalid (must be hexadecimal string w/ length of 32 characters)");
        ReadOnlySpan<char> chars = hex;
        Span<byte> bytes = stackalloc byte[SUID_dataSize];
        foreach (ref byte beit in bytes)
        {
          if (!byte.TryParse(chars.Slice(0, two), AllowHexSpecifier, null, out beit))
            throw new System.FormatException($"could not process <hex> = '{hex.ToUpper()}' as hexadecimal");
          chars = chars.Slice(two);
        }
        return new Guid(bytes);
      }
    }

#endregion

#region write to matroska file

    static int writeSUID()
    {
      Span<byte> xSUID_data, xVoid_elem;
      int Void_elemSize = (int)(SUID_dataOffset - SUID_elemOffset + SUID_dataSize);
      if (InfoCRC_exists)
      {
        xSUID_data = Info_data.AsSpan().Slice((int)(SUID_dataOffset - Info_dataOffset), SUID_dataSize);
        xVoid_elem = Info_data.AsSpan().Slice((int)(SUID_elemOffset - Info_dataOffset), Void_elemSize);
      }
      else
      {
        xSUID_data = SUID_data.ToByteArray();
        xVoid_elem = new byte[Void_elemSize];
      }

      consoleWriteLine(" was: suid " + xSUID_data.To0xHex() + $" crc 0x{InfoCRC_data:X8}".If(InfoCRC_exists));

      if (user.SUID.HasValue)
      {
        if (!user.SUID.Value.TryWriteBytes(xSUID_data))
          error(nameof(task.write) + " internal");
        // // aka
        // user.SUID.Value.ToByteArray().CopyTo(userSUID_data);
        console = " now: suid " + xSUID_data.To0xHex();
      }
      else
      {
        Span<byte> xVoid_data = xVoid_elem.Slice(2);
        xVoid_elem[0] = ID_VOID;
        xVoid_elem[1] = (byte)(xVoid_data.Length | 0b_10000000);
        xVoid_data.Fill(0);
        console = " now: void  0   0   0   0   0   0   0   0   0";
      }

      if (InfoCRC_exists)
        console += $" crc 0x{InfoCRC_data = Info_data.AsSpan().Crc():X8}";

      int countWrittenBytes = 0;
      try
      {
        using (BinaryWriter writer = new BinaryWriter(File.Open(user.filepath, FileMode.Open, FileAccess.Write)))
        {
          if (InfoCRC_exists)
          {
            writer.BaseStream.Seek(InfoCRC_dataOffset, SeekOrigin.Begin);
            writer.Write(InfoCRC_data); // update checksum @ SEGMENT\Info\CRC-32
            countWrittenBytes = CRC_dataSize;
          }
          if (user.SUID.HasValue)
          {
            writer.BaseStream.Seek(SUID_dataOffset, SeekOrigin.Begin);
            writer.Write(xSUID_data); // write suid @ SEGMENT\Info\SegmentUID
            countWrittenBytes += xSUID_data.Length;
          }
          else
          {
            writer.BaseStream.Seek(SUID_elemOffset, SeekOrigin.Begin);
            writer.Write(xVoid_elem); // void suid @ SEGMENT\Info\SegmentUID
            countWrittenBytes += xVoid_elem.Length;
          }
          writer.Close();
        }
      }
      catch (Exception e)
      {
        console = "";
        error("could not write to file: " + e.Message, e.HResult);
      }
      return countWrittenBytes;
    }

#endregion

#region Main

    static void Main(string[] args)
    {
      string assemblyName = GetExecutingAssembly().GetName().Name;

      string assemblyVersion = string.Format(" v{0}.{1}"
      #if release_build
      #else
      + ".test_build"
      #endif
      , GetExecutingAssembly().GetName().Version.Major, GetExecutingAssembly().GetName().Version.Minor);

      console = assemblyName + assemblyVersion + " copyright (c) 2018-present github.com/jens0\n";

      user = processCommandLineArguments();

      #if (override_user_options)
      overrideUserOptions();
      #endif

      if (user.wants.version)
      {
        Console.Write(assemblyName + assemblyVersion);
        System.Environment.Exit(0);
      }

      if (user.wants.help)
        consoleWriteLine(string.Format(helpString, assemblyName).AddLineIf(!user.wants.skipVer));

      if (user.wants.advanced)
        consoleWriteLine(moreString.AddLineIf(!user.wants.skipVer || user.wants.help));

      if (user.filepath == "") // commands require file, else exit
      {
        if (console != "")
          consoleWriteLine($"for help use: {assemblyName} {nameof(task.help)}");
        System.Environment.Exit(0);
      }

      if (!File.Exists(user.filepath))
        error("file not found: " + user.filepath, exitCode: -2147024894); // 0x80070002 = FileNotFound

      if (user.wants.parse && !user.wants.noConfig) // prepare parse command
        loadConfig();

      if (user.wants.write.suid) // prepare write command
      {
        adjustConfig(level: 0, ID_SEGMENT, "SEGMENT"); // make sure that parse reaches suid
        adjustConfig(level: 1, ID_INFO, "Info");
        user.wants.noCrc |= !user.wants.parse; // skip needless crc validation
      }

      DateTime keepDate = new DateTime();
      if (user.wants.keepDate)
        try {
          keepDate = File.GetLastWriteTime(user.filepath); }
        catch (Exception e) {
          error("could not access file's date: " + e.Message, e.HResult); }

      try {
        using (BinaryReader reader = new BinaryReader(
        File.Open(user.filepath, FileMode.Open, FileAccess.Read, FileShare.Read)))
        processFile(new chunk { reader = reader, dataSize = reader.BaseStream.Length }); }
      catch (Exception e) {
        error((user.wants.parse ? nameof(task.parse) : "read") + " matroska: " + e.Message, e.HResult); }

      if (user.wants.write.suid)
      {
        if (SUID_ready4write)
        {
          consoleWriteLine(($"{nameof(task.write)} [{SUID_elemOffset:X10}]".PadRight(45) +
          $"[{InfoCRC_elemOffset:X10}]  ".PadLeft(15).If(InfoCRC_exists)).AddShortLineIf(user.wants.parse));

          consoleWriteLine($"..{writeSUID()} bytes written.");

          if (user.wants.keepDate)
            try {
              File.SetLastWriteTime(user.filepath, keepDate); }
            catch (Exception e) {
              error("could not restore file's date: " + e.Message, e.HResult); }
        }
        else
        if (CRC_processingFailed)
          error("could not write to file: crc processing failed");
        else
          consoleWriteLine(
          $"{nameof(task.write)} command not performed: suid not {(SUID_equal ? "different" : "found in file")}."
          .AddShortLineIf(user.wants.parse));
      }
    }

#endregion

  }
}
namespace StringExtensionMethods
{

#region StringExtensionMethods

  using System;
  using System.Linq;

  public static class StringExtensionMethods
  {
    public static string Brace2Bracket(this string s)
    {
      Span<char> chars = s.ToCharArray();
      foreach (ref char ch in chars)
        if (ch == '{' || ch == '}') ch -= ' ';
      return chars.ToString();
    }

    public static string If(this string s, bool condition) =>
    condition ? s : "";

    public static string AddShortLineIf(this string s, bool precursor) =>
    precursor ? String.Concat(Enumerable.Repeat("- ", ((s.Length > 79 ? 79 : s.Length) + 1) / 2)) + '\n' + s : s;

    public static string AddLineIf(this string s, bool precursor) =>
    precursor ? String.Concat(Enumerable.Repeat("- ", (s.IndexOf('\n') + 1) / 2)) + '\n' + s : s;

    public static string ToShortTitleCase(this string s, int maxLength) // s = TitlesWithoutSpaceSeparator
    {
      int i = 0;
    nextTitle:
      if (s.Length - i <= maxLength)
        return s.Remove(0, i);
      while (i < s.Length - 1)
        if (char.IsLower(s[i++]) && char.IsUpper(s[i]))
          goto nextTitle;
      return s.Remove(maxLength - 2) + "..";
    }

    public static string ToSpecificCase(this string s, int length)
    {
      int a = 0;
      while (a < s.Length - 1 && char.IsUpper(s[a++]) && char.IsUpper(s[a])) ;
      if (s.Length - a-- <= 1)
        a = 0;
      int o;
    nextTitle:
      if (s.Length - (o = a) <= length)
        return s.Remove(0, a);
      while (a < s.Length - 1)
        if (char.IsLower(s[a++]) && char.IsUpper(s[a]))
          goto nextTitle;
      return s.Remove(o);
    }

    public static string ToHex(this Span<byte> span) =>
    BitConverter.ToString(span.ToArray()).Replace("-", "");

    public static string To0xHex(this Span<byte> span) =>
    "0x" + ToHex(span);

    public const char asciiDataInput = '\'';

    public const char unknownDataInput = '~';

    public static (string printable, int quality) ToPrintable(this Span<byte> span, char input = unknownDataInput)
    {
      int countLetters = 0;
      int countPrintableLetters = 0;
      string s = "";
      foreach (byte beit in span)
        if (beit == 0x00)
          break;
        else
        {
          countLetters++;
          if (beit > 0x7E || beit < 0x20)
            s += ' ';
          else
          {
            countPrintableLetters++;
            s += (char)beit;
          }
        }
      if (countLetters == 0)
        if (input == asciiDataInput)
          return ("<empty>", 100);
        else
          return ("", 0);
      return (printable: input + s + input, quality: countPrintableLetters * 100 / countLetters);
    }
  }

#endregion

}
namespace ChecksumCrc32
{

#region ChecksumCrc32

  using System;

  public static class ChecksumCrc32
  {
    public static int Crc(this Span<byte> data)
    {
      uint hash = uint.MaxValue;
      foreach (byte beit in data)
        hash = (hash >> 8) ^ table[beit ^ (byte)hash];
      return (int)~hash;
    }

    private static readonly uint[] table =
    {
      0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
      0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
      0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
      0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
      0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
      0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
      0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
      0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924, 0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
      0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
      0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
      0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
      0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
      0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2, 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
      0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
      0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
      0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
      0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
      0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
      0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
      0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
      0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
      0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
      0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236, 0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
      0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
      0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
      0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
      0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
      0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
      0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
      0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
      0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
      0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
    };
  }

#endregion

}