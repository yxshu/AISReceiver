using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace AisConsoleReceiver;

internal static partial class Program
{
    private const string DefaultPcapPath = @"D:\20260408AIS-DATA-STREAM.pcapng";
    private const string DefaultGroup = "239.192.0.4";
    private const int DefaultPort = 60004;
    private const string DefaultLocalIp = "192.168.1.100";
    private static readonly string SixBitAscii = "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_ !\"#$%&'()*+,-./0123456789:;<=>?";

    private static int Main(string[] args)
    {
        var options = Options.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var decoder = new AisDecoder();
            if (options.ListenMode)
            {
                RunLive(decoder, options);
            }
            else
            {
                RunPcap(decoder, options.PcapPath);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"运行失败: {ex.Message}");
            return 1;
        }
    }

    private static void RunPcap(AisDecoder decoder, string pcapPath)
    {
        if (!File.Exists(pcapPath))
        {
            throw new FileNotFoundException($"找不到测试数据文件: {pcapPath}");
        }

        Console.WriteLine($"读取测试数据: {pcapPath}");
        var bytes = File.ReadAllBytes(pcapPath);
        var text = Encoding.ASCII.GetString(bytes);

        var count = 0;
        foreach (Match match in AivdmRegex().Matches(text))
        {
            var sentence = match.Value.Trim();
            foreach (var decoded in decoder.ProcessSentence(sentence))
            {
                count++;
                Console.WriteLine(FormatDecoded(decoded));
            }
        }

        Console.WriteLine();
        Console.WriteLine($"完成。共输出 {count} 条已解码 AIS 消息。");
    }

    private static void RunLive(AisDecoder decoder, Options options)
    {
        Console.WriteLine($"监听组播 {options.Group}:{options.Port}，本地网卡 {options.LocalIp}");
        Console.WriteLine("按 Ctrl+C 停止。");

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, options.Port));
        udp.JoinMulticastGroup(IPAddress.Parse(options.Group), IPAddress.Parse(options.LocalIp));

        while (true)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            var bytes = udp.Receive(ref remoteEndPoint);
            var text = Encoding.ASCII.GetString(bytes);

            foreach (Match match in AivdmRegex().Matches(text))
            {
                var sentence = match.Value.Trim();
                foreach (var decoded in decoder.ProcessSentence(sentence))
                {
                    Console.WriteLine(FormatDecoded(decoded));
                }
            }
        }
    }

    private static string FormatDecoded(DecodedMessage decoded)
    {
        var parts = new List<string>
        {
            $"Type={decoded.MessageType}",
            $"MMSI={decoded.Mmsi}"
        };

        if (!string.IsNullOrWhiteSpace(decoded.MessageName))
        {
            parts.Add($"Msg={decoded.MessageName}");
        }

        if (!string.IsNullOrWhiteSpace(decoded.ShipName))
        {
            parts.Add($"Name={decoded.ShipName}");
        }

        if (!string.IsNullOrWhiteSpace(decoded.CallSign))
        {
            parts.Add($"Call={decoded.CallSign}");
        }

        if (decoded.Latitude.HasValue && decoded.Longitude.HasValue)
        {
            parts.Add($"Lat={decoded.Latitude.Value:F6}");
            parts.Add($"Lon={decoded.Longitude.Value:F6}");
        }

        if (decoded.SpeedKnots.HasValue)
        {
            parts.Add($"SOG={decoded.SpeedKnots.Value:F1}kn");
        }

        if (decoded.CourseDegrees.HasValue)
        {
            parts.Add($"COG={decoded.CourseDegrees.Value:F1}deg");
        }

        if (decoded.HeadingDegrees.HasValue)
        {
            parts.Add($"HDG={decoded.HeadingDegrees.Value}deg");
        }

        if (!string.IsNullOrWhiteSpace(decoded.NavigationStatus))
        {
            parts.Add($"Status={decoded.NavigationStatus}");
        }

        if (!string.IsNullOrWhiteSpace(decoded.ShipType))
        {
            parts.Add($"ShipType={decoded.ShipType}");
        }

        if (!string.IsNullOrWhiteSpace(decoded.Destination))
        {
            parts.Add($"Dest={decoded.Destination}");
        }

        return string.Join(" | ", parts);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("AIS 控制台解析工具");
        Console.WriteLine();
        Console.WriteLine("默认行为：读取 D:\\20260408AIS-DATA-STREAM.pcapng 并输出解析结果。");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine("  AisConsoleReceiver.exe");
        Console.WriteLine("  AisConsoleReceiver.exe --pcap D:\\20260408AIS-DATA-STREAM.pcapng");
        Console.WriteLine("  AisConsoleReceiver.exe --listen --group 239.192.0.4 --port 60004 --local-ip 192.168.1.100");
        Console.WriteLine();
        Console.WriteLine("参数:");
        Console.WriteLine("  --pcap <path>       指定测试数据 pcapng 文件");
        Console.WriteLine("  --listen            切换到实时监听模式");
        Console.WriteLine("  --group <ip>        组播地址，默认 239.192.0.4");
        Console.WriteLine("  --port <port>       UDP 端口，默认 60004");
        Console.WriteLine("  --local-ip <ip>     本地网卡 IPv4，默认 192.168.1.100");
        Console.WriteLine("  --help              显示帮助");
    }

    [GeneratedRegex(@"!AI(?:VDM|VDO),[^\r\n*]+\*[0-9A-Fa-f]{2}")]
    private static partial Regex AivdmRegex();

    private sealed class Options
    {
        public bool ShowHelp { get; private set; }
        public bool ListenMode { get; private set; }
        public string PcapPath { get; private set; } = DefaultPcapPath;
        public string Group { get; private set; } = DefaultGroup;
        public int Port { get; private set; } = DefaultPort;
        public string LocalIp { get; private set; } = DefaultLocalIp;

        public static Options Parse(string[] args)
        {
            var options = new Options();

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    case "--listen":
                        options.ListenMode = true;
                        break;
                    case "--pcap":
                        options.PcapPath = ReadValue(args, ref i, "--pcap");
                        break;
                    case "--group":
                        options.Group = ReadValue(args, ref i, "--group");
                        break;
                    case "--port":
                        options.Port = int.Parse(ReadValue(args, ref i, "--port"));
                        break;
                    case "--local-ip":
                        options.LocalIp = ReadValue(args, ref i, "--local-ip");
                        break;
                    default:
                        throw new ArgumentException($"未知参数: {args[i]}");
                }
            }

            return options;
        }

        private static string ReadValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{option} 缺少值");
            }

            index++;
            return args[index];
        }
    }

    private sealed class AisDecoder
    {
        private readonly MultipartAssembler _assembler = new(TimeSpan.FromMinutes(2));

        public IEnumerable<DecodedMessage> ProcessSentence(string sentence)
        {
            if (!NmeaSentence.TryParse(sentence, out var nmea))
            {
                yield break;
            }

            var assembled = _assembler.Add(nmea);
            if (assembled is null)
            {
                yield break;
            }

            var bits = SixBitPayloadToBits(assembled.Payload, assembled.FillBits);
            yield return Decode(bits);
        }

        private static DecodedMessage Decode(string bits)
        {
            var messageType = GetUInt(bits, 0, 6);
            var repeat = GetUInt(bits, 6, 2);
            var mmsi = GetUInt(bits, 8, 30);

            var result = new DecodedMessage
            {
                MessageType = messageType,
                RepeatIndicator = repeat,
                Mmsi = mmsi
            };

            switch (messageType)
            {
                case 1:
                case 2:
                case 3:
                    result.MessageName = "Class A Position Report";
                    result.NavigationStatus = NavigationStatusName(GetUInt(bits, 38, 4));
                    FillPosition(result, bits, 61, 89, 50, 116, 128);
                    break;
                case 5:
                    result.MessageName = "Static and Voyage Related Data";
                    result.CallSign = GetSixBitText(bits, 70, 42);
                    result.ShipName = GetSixBitText(bits, 112, 120);
                    var shipType5 = GetUInt(bits, 232, 8);
                    result.ShipType = ShipTypeName(shipType5);
                    result.Destination = GetSixBitText(bits, 302, 120);
                    break;
                case 18:
                    result.MessageName = "Class B Position Report";
                    FillPosition(result, bits, 57, 85, 46, 112, 124);
                    break;
                case 19:
                    result.MessageName = "Extended Class B Position Report";
                    FillPosition(result, bits, 57, 85, 46, 112, 124);
                    result.ShipName = GetSixBitText(bits, 143, 120);
                    var shipType19 = GetUInt(bits, 263, 8);
                    result.ShipType = ShipTypeName(shipType19);
                    break;
                case 24:
                    result.MessageName = "Class B Static Data";
                    var partNumber = GetUInt(bits, 38, 2);
                    if (partNumber == 0)
                    {
                        result.ShipName = GetSixBitText(bits, 40, 120);
                    }
                    else if (partNumber == 1)
                    {
                        result.CallSign = GetSixBitText(bits, 90, 42);
                        var shipType24 = GetUInt(bits, 40, 8);
                        result.ShipType = ShipTypeName(shipType24);
                    }
                    break;
                default:
                    result.MessageName = $"AIS Message Type {messageType}";
                    break;
            }

            return result;
        }

        private static void FillPosition(DecodedMessage result, string bits, int lonStart, int latStart, int sogStart, int cogStart, int hdgStart)
        {
            result.Longitude = DecodeLongitude(GetInt(bits, lonStart, 28));
            result.Latitude = DecodeLatitude(GetInt(bits, latStart, 27));
            result.SpeedKnots = DecodeSpeed(GetUInt(bits, sogStart, 10));
            result.CourseDegrees = DecodeCourse(GetUInt(bits, cogStart, 12));
            result.HeadingDegrees = DecodeHeading(GetUInt(bits, hdgStart, 9));
        }
    }

    private sealed class MultipartAssembler
    {
        private readonly TimeSpan _timeout;
        private readonly Dictionary<string, MultipartState> _pending = new();

        public MultipartAssembler(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public AssembledPayload? Add(NmeaSentence sentence)
        {
            PurgeExpired();

            if (sentence.FragmentCount == 1)
            {
                return new AssembledPayload(sentence.Payload, sentence.FillBits);
            }

            var key = $"{sentence.Talker}|{sentence.Channel}|{sentence.SequenceId}|{sentence.FragmentCount}";
            if (!_pending.TryGetValue(key, out var state))
            {
                state = new MultipartState();
                _pending[key] = state;
            }

            state.UpdatedAtUtc = DateTime.UtcNow;
            state.Parts[sentence.FragmentNumber] = sentence.Payload;

            if (state.Parts.Count != sentence.FragmentCount)
            {
                return null;
            }

            var builder = new StringBuilder();
            for (var i = 1; i <= sentence.FragmentCount; i++)
            {
                if (!state.Parts.TryGetValue(i, out var part))
                {
                    return null;
                }

                builder.Append(part);
            }

            _pending.Remove(key);
            return new AssembledPayload(builder.ToString(), sentence.FillBits);
        }

        private void PurgeExpired()
        {
            var expired = _pending
                .Where(kvp => DateTime.UtcNow - kvp.Value.UpdatedAtUtc > _timeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
            {
                _pending.Remove(key);
            }
        }

        private sealed class MultipartState
        {
            public Dictionary<int, string> Parts { get; } = new();
            public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        }
    }

    private sealed record AssembledPayload(string Payload, int FillBits);

    private sealed class DecodedMessage
    {
        public int MessageType { get; init; }
        public int RepeatIndicator { get; init; }
        public int Mmsi { get; init; }
        public string MessageName { get; set; } = string.Empty;
        public string ShipName { get; set; } = string.Empty;
        public string CallSign { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? SpeedKnots { get; set; }
        public double? CourseDegrees { get; set; }
        public int? HeadingDegrees { get; set; }
        public string NavigationStatus { get; set; } = string.Empty;
        public string ShipType { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
    }

    private sealed class NmeaSentence
    {
        public string Talker { get; init; } = string.Empty;
        public int FragmentCount { get; init; }
        public int FragmentNumber { get; init; }
        public string SequenceId { get; init; } = string.Empty;
        public string Channel { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
        public int FillBits { get; init; }

        public static bool TryParse(string sentence, out NmeaSentence result)
        {
            result = null!;

            if (!IsChecksumValid(sentence))
            {
                return false;
            }

            var body = sentence[1..sentence.IndexOf('*')];
            var fields = body.Split(',');
            if (fields.Length < 7)
            {
                return false;
            }

            if (!int.TryParse(fields[1], out var fragmentCount) ||
                !int.TryParse(fields[2], out var fragmentNumber) ||
                !int.TryParse(fields[6], out var fillBits))
            {
                return false;
            }

            result = new NmeaSentence
            {
                Talker = fields[0],
                FragmentCount = fragmentCount,
                FragmentNumber = fragmentNumber,
                SequenceId = fields[3],
                Channel = fields[4],
                Payload = fields[5],
                FillBits = fillBits
            };
            return true;
        }

        private static bool IsChecksumValid(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence) || sentence[0] != '!' || !sentence.Contains('*'))
            {
                return false;
            }

            var starIndex = sentence.IndexOf('*');
            var body = sentence[1..starIndex];
            var checksumText = sentence[(starIndex + 1)..].Trim();
            if (checksumText.Length < 2)
            {
                return false;
            }

            var checksum = 0;
            foreach (var ch in body)
            {
                checksum ^= ch;
            }

            return int.TryParse(checksumText[..2], System.Globalization.NumberStyles.HexNumber, null, out var expected)
                   && checksum == expected;
        }
    }

    private static string SixBitPayloadToBits(string payload, int fillBits)
    {
        var builder = new StringBuilder(payload.Length * 6);
        foreach (var ch in payload)
        {
            var value = ch - 48;
            if (value > 40)
            {
                value -= 8;
            }

            builder.Append(Convert.ToString(value, 2).PadLeft(6, '0'));
        }

        if (fillBits > 0)
        {
            builder.Length -= fillBits;
        }

        return builder.ToString();
    }

    private static int GetUInt(string bits, int start, int length) => Convert.ToInt32(bits.Substring(start, length), 2);

    private static int GetInt(string bits, int start, int length)
    {
        var value = GetUInt(bits, start, length);
        var signBit = 1 << (length - 1);
        if ((value & signBit) != 0)
        {
            value -= 1 << length;
        }

        return value;
    }

    private static string GetSixBitText(string bits, int start, int length)
    {
        var builder = new StringBuilder();
        for (var offset = start; offset < start + length; offset += 6)
        {
            var idx = GetUInt(bits, offset, 6);
            builder.Append(SixBitAscii[idx]);
        }

        return builder.ToString().Replace('@', ' ').Trim();
    }

    private static double? DecodeLongitude(int raw) => raw == 0x6791AC0 ? null : Math.Round(raw / 600000.0, 6);

    private static double? DecodeLatitude(int raw) => raw == 0x3412140 ? null : Math.Round(raw / 600000.0, 6);

    private static double? DecodeSpeed(int raw) => raw is 1022 or 1023 ? null : raw / 10.0;

    private static double? DecodeCourse(int raw) => raw >= 3600 ? null : raw / 10.0;

    private static int? DecodeHeading(int raw) => raw >= 511 ? null : raw;

    private static string ShipTypeName(int code) => code switch
    {
        30 => "Fishing",
        31 => "Towing",
        32 => "Towing > 200m / >25m wide",
        33 => "Dredging or underwater ops",
        34 => "Diving ops",
        35 => "Military ops",
        36 => "Sailing",
        37 => "Pleasure craft",
        50 => "Pilot vessel",
        51 => "Search and rescue",
        52 => "Tug",
        53 => "Port tender",
        54 => "Anti-pollution",
        55 => "Law enforcement",
        58 => "Medical transport",
        59 => "Special craft",
        60 => "Passenger",
        70 => "Cargo",
        80 => "Tanker",
        90 => "Other",
        _ => $"Type {code}"
    };

    private static string NavigationStatusName(int code) => code switch
    {
        0 => "Under way using engine",
        1 => "At anchor",
        2 => "Not under command",
        3 => "Restricted manoeuverability",
        4 => "Constrained by draught",
        5 => "Moored",
        6 => "Aground",
        7 => "Fishing",
        8 => "Under way sailing",
        15 => "Not defined",
        _ => $"Status {code}"
    };
}
