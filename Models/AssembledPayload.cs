namespace AisConsoleReceiver;

/// <summary>
/// 已完成组装的 AIS 负载及其填充位信息。
/// </summary>
internal sealed record AssembledPayload(
    /// <summary>
    /// 完整的 AIS 六位装甲载荷。
    /// </summary>
    string Payload,
    /// <summary>
    /// 载荷尾部的填充位数量。
    /// </summary>
    int FillBits);
