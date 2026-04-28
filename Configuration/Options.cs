using System.Net;
using System.Net.Sockets;

namespace AisConsoleReceiver;

/// <summary>
/// 命令行参数对象。
/// </summary>
internal sealed class Options
{
    internal const string DefaultPcapFileName = "20260408AIS-DATA-STREAM.pcapng";
    internal const string DefaultGroup = "239.192.0.4";
    internal const int DefaultPort = 60004;
    internal const string DefaultLocalIp = "192.168.1.100";

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
