namespace AisConsoleReceiver;

/// <summary>
/// 解析后的 NMEA 语句结构。
/// </summary>
internal sealed class NmeaSentence
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
}
