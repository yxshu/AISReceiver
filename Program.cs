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
    private static void RunPcap(AisDecoder decoder, string pcapPath)
    {
        var resolvedPcapPath = ResolvePcapPath(pcapPath);
        if (resolvedPcapPath is null)
        {
            throw new FileNotFoundException($"找不到 pcap 文件：{pcapPath}");
        }

        Console.WriteLine($"正在读取抓包文件：{resolvedPcapPath}");
        var bytes = File.ReadAllBytes(resolvedPcapPath);
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
    private static void RunLive(AisDecoder decoder, Options options)
    {
        Console.WriteLine($"正在监听组播 {options.Group}:{options.Port}，使用本地网卡 {options.LocalIp}");
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

    /// <summary>
    /// 将解码后的 AIS 消息格式化为单行文本，便于控制台输出。
    /// </summary>
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
        var defaultPcap = ResolvePcapPath(Options.DefaultPcapFileName) ?? Options.DefaultPcapFileName;

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
    /// 解析抓包文件路径，兼容源码目录和构建输出目录的常见运行位置。
    /// </summary>
    private static string? ResolvePcapPath(string requestedPath)
    {
        if (Path.IsPathRooted(requestedPath))
        {
            return File.Exists(requestedPath) ? requestedPath : null;
        }

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
}
