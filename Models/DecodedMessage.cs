namespace AisConsoleReceiver;

/// <summary>
/// AIS 消息的解码结果模型。
/// </summary>
internal sealed class DecodedMessage
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
