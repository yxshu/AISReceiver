namespace AisConsoleReceiver;

/// <summary>
/// AIS 标准代码表。
/// </summary>
internal static class AisCodeTables
{
    /// <summary>
    /// 将船舶类型代码转换为可读文本。
    /// </summary>
    public static string ShipTypeName(int code) => code switch
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
    public static string NavigationStatusName(int code) => code switch
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
