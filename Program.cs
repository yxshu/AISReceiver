using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace AisConsoleReceiver;

/// <summary>
/// AIS 控制台接收器入口，支持从抓包文件或组播流中提取并解码 AIS 报文。
/// </summary>
internal static partial class Program
{
    private const string DefaultPcapFileName = "20260408AIS-DATA-STREAM.pcapng";
    private const string DefaultGroup = "239.192.0.4";
    private const int DefaultPort = 60004;
    private const string DefaultLocalIp = "192.168.1.100";
    /// <summary>
    /// AIS 六位文本字段使用的自定义字符表。
    /// </summary>
    private static readonly string SixBitAscii = "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_ !\"#$%&'()*+,-./0123456789:;<=>?";

    /// <summary>
    /// 解析命令行参数并执行对应的 AIS 读取流程。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>成功返回 0，失败返回 1。</returns>
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
            Console.Error.WriteLine($"运行失败：{ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 从 pcapng 文件中提取 AIS 语句并输出解码结果。
    /// </summary>
    /// <param name="decoder">AIS 解码器。</param>
    /// <param name="pcapPath">抓包文件路径。</param>
    private static void RunPcap(AisDecoder decoder, string pcapPath)
    {
        var resolvedPcapPath = ResolvePcapPath(pcapPath);
        if (resolvedPcapPath is null)
        {
            throw new FileNotFoundException($"找不到 pcap 文件：{pcapPath}");
        }

        Console.WriteLine($"正在读取抓包文件：{resolvedPcapPath}");
        var bytes = File.ReadAllBytes(resolvedPcapPath);
        // 这里只需要按 ASCII 提取文本，因为抓包中的 AIS/NMEA 语句本身就是文本格式。
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
        Console.WriteLine($"完成，共解码 {count} 条 AIS 消息。");
    }

    /// <summary>
    /// 监听实时组播 AIS 数据并持续输出解码结果。
    /// </summary>
    /// <param name="decoder">AIS 解码器。</param>
    /// <param name="options">监听参数。</param>
    private static void RunLive(AisDecoder decoder, Options options)
    {
        Console.WriteLine($"正在监听组播 {options.Group}:{options.Port}，使用本地网卡 {options.LocalIp}");
        Console.WriteLine("按 Ctrl+C 停止。");

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, options.Port));
        // 指定本地网卡加入组播，确保从期望的网络接口接收数据。
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

    /// <summary>
    /// 将解码后的 AIS 消息格式化为单行文本，便于控制台输出。
    /// </summary>
    /// <param name="decoded">解码结果。</param>
    /// <returns>格式化后的文本。</returns>
    private static string FormatDecoded(DecodedMessage decoded)
    {
        var parts = new List<string>
        {
            $"类型={decoded.MessageType}",
            $"MMSI={decoded.Mmsi}"
        };

        if (!string.IsNullOrWhiteSpace(decoded.MessageName))
        {
            parts.Add($"消息={decoded.MessageName}");
        }

        if (!string.IsNullOrWhiteSpace(decoded.ShipName))
        {
            parts.Add($"船名={decoded.ShipName}");
        }

        if (!string.IsNullOrWhiteSpace(decoded.CallSign))
        {
            parts.Add($"呼号={decoded.CallSign}");
        }

        if (decoded.Latitude.HasValue && decoded.Longitude.HasValue)
        {
            parts.Add($"纬度={decoded.Latitude.Value:F6}");
            parts.Add($"经度={decoded.Longitude.Value:F6}");
        }

        if (decoded.SpeedKnots.HasValue)
        {
            parts.Add($"航速={decoded.SpeedKnots.Value:F1} 节");
        }

        if (decoded.CourseDegrees.HasValue)
        {
            parts.Add($"航向={decoded.CourseDegrees.Value:F1}°");
        }

        if (decoded.HeadingDegrees.HasValue)
        {
            parts.Add($"船首向={decoded.HeadingDegrees.Value}°");
        }

        if (!string.IsNullOrWhiteSpace(decoded.NavigationStatus))
        {
            parts.Add($"状态={decoded.NavigationStatus}");
        }

        if (!string.IsNullOrWhiteSpace(decoded.ShipType))
        {
            parts.Add($"船型={decoded.ShipType}");
        }

        if (!string.IsNullOrWhiteSpace(decoded.Destination))
        {
            parts.Add($"目的地={decoded.Destination}");
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// 输出命令行帮助信息。
    /// </summary>
    private static void PrintHelp()
    {
        var defaultPcap = ResolvePcapPath(DefaultPcapFileName) ?? DefaultPcapFileName;

        Console.WriteLine("AIS 控制台接收器");
        Console.WriteLine();
        Console.WriteLine($"默认行为：读取 {defaultPcap} 并输出解码后的 AIS 消息。");
        Console.WriteLine();
        Console.WriteLine("用法：");
        Console.WriteLine("  AisConsoleReceiver.exe");
        Console.WriteLine("  AisConsoleReceiver.exe --pcap C:\\path\\to\\capture.pcapng");
        Console.WriteLine("  AisConsoleReceiver.exe --listen --group 239.192.0.4 --port 60004 --local-ip 192.168.1.100");
        Console.WriteLine();
        Console.WriteLine("选项：");
        Console.WriteLine("  --pcap <path>       读取 pcapng 抓包文件");
        Console.WriteLine("  --listen            监听实时 AIS 组播数据");
        Console.WriteLine("  --group <ip>        组播地址，默认 239.192.0.4");
        Console.WriteLine("  --port <port>       UDP 端口，默认 60004");
        Console.WriteLine("  --local-ip <ip>     本地 IPv4 网卡地址，默认 192.168.1.100");
        Console.WriteLine("  --help              显示帮助");
    }

    [GeneratedRegex(@"!AI(?:VDM|VDO),[^\r\n*]+\*[0-9A-Fa-f]{2}")]
    private static partial Regex AivdmRegex();

    /// <summary>
    /// 命令行参数对象。
    /// </summary>
    private sealed class Options
    {
        /// <summary>
        /// 是否仅显示帮助信息。
        /// </summary>
        public bool ShowHelp { get; private set; }

        /// <summary>
        /// 是否启用实时监听模式。
        /// </summary>
        public bool ListenMode { get; private set; }

        /// <summary>
        /// 要读取的抓包文件路径。
        /// </summary>
        public string PcapPath { get; private set; } = DefaultPcapFileName;

        /// <summary>
        /// 监听使用的组播地址。
        /// </summary>
        public string Group { get; private set; } = DefaultGroup;

        /// <summary>
        /// 监听使用的 UDP 端口。
        /// </summary>
        public int Port { get; private set; } = DefaultPort;

        /// <summary>
        /// 加入组播时使用的本地 IPv4 网卡地址。
        /// </summary>
        public string LocalIp { get; private set; } = DefaultLocalIp;

        /// <summary>
        /// 解析命令行参数并生成配置对象。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        /// <returns>解析后的配置。</returns>
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
                        options.Port = ParsePort(ReadValue(args, ref i, "--port"));
                        break;
                    case "--local-ip":
                        options.LocalIp = ReadValue(args, ref i, "--local-ip");
                        break;
                    default:
                        throw new ArgumentException($"未知选项：{args[i]}");
                }
            }

            Validate(options);
            return options;
        }

        private static string ReadValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{option} 缺少参数值。");
            }

            index++;
            return args[index];
        }

        private static int ParsePort(string value)
        {
            if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
            {
                throw new ArgumentException($"无效端口：{value}");
            }

            return port;
        }

        private static void Validate(Options options)
        {
            if (!IPAddress.TryParse(options.Group, out var groupAddress) || groupAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException($"无效的组播 IPv4 地址：{options.Group}");
            }

            if (!IPAddress.TryParse(options.LocalIp, out var localAddress) || localAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException($"无效的本地 IPv4 地址：{options.LocalIp}");
            }
        }
    }

    /// <summary>
    /// AIS 报文解码器，负责 NMEA 解析、分片重组和字段解码。
    /// </summary>
    private sealed class AisDecoder
    {
        private readonly MultipartAssembler _assembler = new(TimeSpan.FromMinutes(2));

        /// <summary>
        /// 处理单条 AIS/NMEA 语句，并在可解码时返回结果。
        /// </summary>
        /// <param name="sentence">原始 NMEA 语句。</param>
        /// <returns>解码后的 AIS 消息集合。</returns>
        public IEnumerable<DecodedMessage> ProcessSentence(string sentence)
        {
            if (!NmeaSentence.TryParse(sentence, out var nmea))
            {
                yield break;
            }

            var assembled = _assembler.Add(nmea);
            if (assembled is null)
            {
                // 多片 AIS 报文只有在全部分片到齐后才会继续解码。
                yield break;
            }

            var bits = SixBitPayloadToBits(assembled.Payload, assembled.FillBits);
            yield return Decode(bits);
        }

        /// <summary>
        /// 按 AIS 位布局解析单条完整负载。
        /// </summary>
        /// <param name="bits">展开后的二进制位串。</param>
        /// <returns>解码结果。</returns>
        private static DecodedMessage Decode(string bits)
        {
            if (!HasBits(bits, 38))
            {
                return new DecodedMessage
                {
                    MessageName = "AIS 载荷被截断"
                };
            }

            var messageType = GetUInt(bits, 0, 6);
            var repeat = GetUInt(bits, 6, 2);
            var mmsi = GetUInt(bits, 8, 30);

            var result = new DecodedMessage
            {
                MessageType = messageType,
                RepeatIndicator = repeat,
                Mmsi = mmsi
            };

            // 优先解析常见 AIS 消息类型；未知类型仍保留基础信息，便于排查。
            switch (messageType)
            {
                case 1:
                case 2:
                case 3:
                    result.MessageName = "A 类位置报告";
                    if (HasBits(bits, 42))
                    {
                        result.NavigationStatus = NavigationStatusName(GetUInt(bits, 38, 4));
                    }

                    FillPosition(result, bits, 61, 89, 50, 116, 128);
                    break;
                case 5:
                    result.MessageName = "静态与航次相关数据";
                    if (HasBits(bits, 112))
                    {
                        result.CallSign = GetSixBitText(bits, 70, 42);
                    }

                    if (HasBits(bits, 232))
                    {
                        result.ShipName = GetSixBitText(bits, 112, 120);
                    }

                    if (HasBits(bits, 240))
                    {
                        var shipType5 = GetUInt(bits, 232, 8);
                        result.ShipType = ShipTypeName(shipType5);
                    }

                    if (HasBits(bits, 422))
                    {
                        result.Destination = GetSixBitText(bits, 302, 120);
                    }

                    break;
                case 18:
                    result.MessageName = "B 类位置报告";
                    FillPosition(result, bits, 57, 85, 46, 112, 124);
                    break;
                case 19:
                    result.MessageName = "扩展 B 类位置报告";
                    FillPosition(result, bits, 57, 85, 46, 112, 124);
                    if (HasBits(bits, 263))
                    {
                        result.ShipName = GetSixBitText(bits, 143, 120);
                    }

                    if (HasBits(bits, 271))
                    {
                        var shipType19 = GetUInt(bits, 263, 8);
                        result.ShipType = ShipTypeName(shipType19);
                    }

                    break;
                case 24:
                    result.MessageName = "B 类静态数据";
                    if (!HasBits(bits, 40))
                    {
                        break;
                    }

                    var partNumber = GetUInt(bits, 38, 2);
                    if (partNumber == 0)
                    {
                        if (HasBits(bits, 160))
                        {
                            result.ShipName = GetSixBitText(bits, 40, 120);
                        }
                    }
                    else if (partNumber == 1)
                    {
                        if (HasBits(bits, 48))
                        {
                            var shipType24 = GetUInt(bits, 40, 8);
                            result.ShipType = ShipTypeName(shipType24);
                        }

                        if (HasBits(bits, 132))
                        {
                            result.CallSign = GetSixBitText(bits, 90, 42);
                        }
                    }

                    break;
                default:
                    result.MessageName = $"AIS 消息类型 {messageType}";
                    break;
            }

            return result;
        }

        /// <summary>
        /// 根据给定的位偏移提取位置、航速、航向等公共导航字段。
        /// </summary>
        /// <param name="result">待填充的解码结果。</param>
        /// <param name="bits">完整位串。</param>
        /// <param name="lonStart">经度起始位。</param>
        /// <param name="latStart">纬度起始位。</param>
        /// <param name="sogStart">对地航速起始位。</param>
        /// <param name="cogStart">对地航向起始位。</param>
        /// <param name="hdgStart">船首向起始位。</param>
        private static void FillPosition(DecodedMessage result, string bits, int lonStart, int latStart, int sogStart, int cogStart, int hdgStart)
        {
            // 不同 AIS 消息类型里，经纬度、航速、航向等字段的位偏移并不相同。
            if (HasBits(bits, lonStart + 28))
            {
                result.Longitude = DecodeLongitude(GetInt(bits, lonStart, 28));
            }

            if (HasBits(bits, latStart + 27))
            {
                result.Latitude = DecodeLatitude(GetInt(bits, latStart, 27));
            }

            if (HasBits(bits, sogStart + 10))
            {
                result.SpeedKnots = DecodeSpeed(GetUInt(bits, sogStart, 10));
            }

            if (HasBits(bits, cogStart + 12))
            {
                result.CourseDegrees = DecodeCourse(GetUInt(bits, cogStart, 12));
            }

            if (HasBits(bits, hdgStart + 9))
            {
                result.HeadingDegrees = DecodeHeading(GetUInt(bits, hdgStart, 9));
            }
        }
    }

    /// <summary>
    /// 负责缓存并重组 AIS 多分片报文。
    /// </summary>
    private sealed class MultipartAssembler
    {
        private readonly TimeSpan _timeout;
        private readonly Dictionary<string, MultipartState> _pending = new();

        /// <summary>
        /// 初始化分片重组器。
        /// </summary>
        /// <param name="timeout">分片超时时间。</param>
        public MultipartAssembler(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        /// <summary>
        /// 添加一条 NMEA 语句；如果分片齐全，则返回组装后的负载。
        /// </summary>
        /// <param name="sentence">已解析的 NMEA 语句。</param>
        /// <returns>组装完成的 AIS 负载；若仍缺分片则返回空。</returns>
        public AssembledPayload? Add(NmeaSentence sentence)
        {
            PurgeExpired();

            if (sentence.FragmentCount == 1)
            {
                return new AssembledPayload(sentence.Payload, sentence.FillBits);
            }

            // SequenceId 并不一定全局唯一，所以把 talker、channel 和分片总数一起纳入键值。
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

        /// <summary>
        /// 清理已超时的分片缓存。
        /// </summary>
        private void PurgeExpired()
        {
            // 清理超时的残缺分片，避免长时间监听时缓存持续增长。
            var expired = _pending
                .Where(kvp => DateTime.UtcNow - kvp.Value.UpdatedAtUtc > _timeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
            {
                _pending.Remove(key);
            }
        }

        /// <summary>
        /// 多分片报文在缓存中的临时状态。
        /// </summary>
        private sealed class MultipartState
        {
            /// <summary>
            /// 已接收的分片内容，键为分片序号。
            /// </summary>
            public Dictionary<int, string> Parts { get; } = new();

            /// <summary>
            /// 最近一次更新该分片状态的 UTC 时间。
            /// </summary>
            public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 已完成组装的 AIS 负载及其填充位信息。
    /// </summary>
    private sealed record AssembledPayload(string Payload, int FillBits);

    /// <summary>
    /// AIS 消息的解码结果模型。
    /// </summary>
    private sealed class DecodedMessage
    {
        /// <summary>
        /// AIS 消息类型编号。
        /// </summary>
        public int MessageType { get; init; }

        /// <summary>
        /// 重发指示器。
        /// </summary>
        public int RepeatIndicator { get; init; }

        /// <summary>
        /// 船舶唯一识别号 MMSI。
        /// </summary>
        public int Mmsi { get; init; }

        /// <summary>
        /// 消息类型的可读名称。
        /// </summary>
        public string MessageName { get; set; } = string.Empty;

        /// <summary>
        /// 船名。
        /// </summary>
        public string ShipName { get; set; } = string.Empty;

        /// <summary>
        /// 船舶呼号。
        /// </summary>
        public string CallSign { get; set; } = string.Empty;

        /// <summary>
        /// 纬度，单位为度。
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// 经度，单位为度。
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// 对地航速，单位为节。
        /// </summary>
        public double? SpeedKnots { get; set; }

        /// <summary>
        /// 对地航向，单位为度。
        /// </summary>
        public double? CourseDegrees { get; set; }

        /// <summary>
        /// 船首向，单位为度。
        /// </summary>
        public int? HeadingDegrees { get; set; }

        /// <summary>
        /// 航行状态的可读文本。
        /// </summary>
        public string NavigationStatus { get; set; } = string.Empty;

        /// <summary>
        /// 船型的可读文本。
        /// </summary>
        public string ShipType { get; set; } = string.Empty;

        /// <summary>
        /// 目的地。
        /// </summary>
        public string Destination { get; set; } = string.Empty;
    }

    /// <summary>
    /// 解析后的 NMEA 语句结构。
    /// </summary>
    private sealed class NmeaSentence
    {
        /// <summary>
        /// 报文头标识，例如 AIVDM 或 AIVDO。
        /// </summary>
        public string Talker { get; init; } = string.Empty;

        /// <summary>
        /// 当前消息的总分片数。
        /// </summary>
        public int FragmentCount { get; init; }

        /// <summary>
        /// 当前分片序号，从 1 开始。
        /// </summary>
        public int FragmentNumber { get; init; }

        /// <summary>
        /// 用于关联多分片消息的序列号。
        /// </summary>
        public string SequenceId { get; init; } = string.Empty;

        /// <summary>
        /// AIS 信道标识。
        /// </summary>
        public string Channel { get; init; } = string.Empty;

        /// <summary>
        /// AIS 六位装甲载荷字符串。
        /// </summary>
        public string Payload { get; init; } = string.Empty;

        /// <summary>
        /// 载荷末尾的填充位数量。
        /// </summary>
        public int FillBits { get; init; }

        /// <summary>
        /// 尝试把原始文本解析成 NMEA 语句对象。
        /// </summary>
        /// <param name="sentence">原始语句文本。</param>
        /// <param name="result">解析结果。</param>
        /// <returns>解析成功返回 true，否则返回 false。</returns>
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

        /// <summary>
        /// 校验 NMEA 语句的异或校验和。
        /// </summary>
        /// <param name="sentence">原始语句文本。</param>
        /// <returns>校验通过返回 true。</returns>
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
                // NMEA 校验和是 '!' 与 '*' 之间所有字符按字节异或得到的值。
                checksum ^= ch;
            }

            return int.TryParse(checksumText[..2], System.Globalization.NumberStyles.HexNumber, null, out var expected)
                   && checksum == expected;
        }
    }

    /// <summary>
    /// 将 AIS 六位装甲载荷转换为连续位串，并去除尾部填充位。
    /// </summary>
    /// <param name="payload">AIS 装甲载荷。</param>
    /// <param name="fillBits">尾部填充位数量。</param>
    /// <returns>展开后的二进制位串。</returns>
    private static string SixBitPayloadToBits(string payload, int fillBits)
    {
        var builder = new StringBuilder(payload.Length * 6);
        foreach (var ch in payload)
        {
            // AIS 装甲编码会把可见 ASCII 映射成 6 位值，其中在 87 ('W') 之后有一个跳跃区间。
            var value = ch - 48;
            if (value > 40)
            {
                value -= 8;
            }

            builder.Append(Convert.ToString(value, 2).PadLeft(6, '0'));
        }

        if (fillBits > 0 && builder.Length >= fillBits)
        {
            builder.Length -= fillBits;
        }

        return builder.ToString();
    }

    private static bool HasBits(string bits, int requiredLength) => bits.Length >= requiredLength;

    /// <summary>
    /// 解析抓包文件路径，兼容源码目录和构建输出目录的常见运行位置。
    /// </summary>
    /// <param name="requestedPath">用户请求的路径。</param>
    /// <returns>找到时返回绝对路径，否则返回空。</returns>
    private static string? ResolvePcapPath(string requestedPath)
    {
        if (Path.IsPathRooted(requestedPath))
        {
            return File.Exists(requestedPath) ? requestedPath : null;
        }

        // 同时尝试当前工作目录以及常见的构建输出相对路径。
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, requestedPath),
            Path.Combine(AppContext.BaseDirectory, requestedPath),
            Path.Combine(AppContext.BaseDirectory, "..", requestedPath),
            Path.Combine(AppContext.BaseDirectory, "..", "..", requestedPath),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", requestedPath)
        };

        foreach (var candidate in candidates.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 从位串中读取无符号整数。
    /// </summary>
    private static int GetUInt(string bits, int start, int length) => Convert.ToInt32(bits.Substring(start, length), 2);

    /// <summary>
    /// 从位串中读取有符号整数。
    /// </summary>
    private static int GetInt(string bits, int start, int length)
    {
        var value = GetUInt(bits, start, length);
        var signBit = 1 << (length - 1);
        if ((value & signBit) != 0)
        {
            // AIS 的有符号整数使用二进制补码表示。
            value -= 1 << length;
        }

        return value;
    }

    /// <summary>
    /// 按 AIS 六位字符表从位串中读取文本字段。
    /// </summary>
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

    /// <summary>
    /// 解码 AIS 经度字段。
    /// </summary>
    private static double? DecodeLongitude(int raw) => raw == 0x6791AC0 ? null : Math.Round(raw / 600000.0, 6);

    /// <summary>
    /// 解码 AIS 纬度字段。
    /// </summary>
    private static double? DecodeLatitude(int raw) => raw == 0x3412140 ? null : Math.Round(raw / 600000.0, 6);

    /// <summary>
    /// 解码 AIS 对地航速字段；特殊上限值表示无可用数据。
    /// </summary>
    private static double? DecodeSpeed(int raw) => raw is 1022 or 1023 ? null : raw / 10.0;

    /// <summary>
    /// 解码 AIS 对地航向字段。
    /// </summary>
    private static double? DecodeCourse(int raw) => raw >= 3600 ? null : raw / 10.0;

    /// <summary>
    /// 解码 AIS 船首向字段。
    /// </summary>
    private static int? DecodeHeading(int raw) => raw >= 511 ? null : raw;

    /// <summary>
    /// 将船舶类型代码转换为可读文本。
    /// </summary>
    private static string ShipTypeName(int code) => code switch
    {
        30 => "渔船",
        31 => "拖船",
        32 => "大型拖带船",
        33 => "疏浚或水下作业",
        34 => "潜水作业",
        35 => "军用船舶",
        36 => "帆船",
        37 => "游艇",
        50 => "引航船",
        51 => "搜救船",
        52 => "拖轮",
        53 => "港作船",
        54 => "防污染船",
        55 => "执法船",
        58 => "医疗运输船",
        59 => "特种船",
        60 => "客船",
        70 => "货船",
        80 => "油船",
        90 => "其他",
        _ => $"类型 {code}"
    };

    /// <summary>
    /// 将航行状态代码转换为可读文本。
    /// </summary>
    private static string NavigationStatusName(int code) => code switch
    {
        0 => "机动航行中",
        1 => "锚泊中",
        2 => "失去控制",
        3 => "操纵受限",
        4 => "受吃水限制",
        5 => "系泊中",
        6 => "搁浅",
        7 => "作业捕鱼",
        8 => "扬帆航行中",
        15 => "未定义",
        _ => $"状态 {code}"
    };
}
