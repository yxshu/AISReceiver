using System.Text;

namespace AisConsoleReceiver;

/// <summary>
/// AIS 位串转换与字段解码工具。
/// </summary>
internal static class AisBitDecoder
{
    private const string SixBitAscii = "@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_ !\"#$%&'()*+,-./0123456789:;<=>?";

    /// <summary>
    /// 将 AIS 六位装甲载荷转换为连续位串，并去除尾部填充位。
    /// </summary>
    public static string SixBitPayloadToBits(string payload, int fillBits)
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

        if (fillBits > 0 && builder.Length >= fillBits)
        {
            builder.Length -= fillBits;
        }

        return builder.ToString();
    }

    public static bool HasBits(string bits, int requiredLength) => bits.Length >= requiredLength;

    public static int GetUInt(string bits, int start, int length) => Convert.ToInt32(bits.Substring(start, length), 2);

    public static int GetInt(string bits, int start, int length)
    {
        var value = GetUInt(bits, start, length);
        var signBit = 1 << (length - 1);
        if ((value & signBit) != 0)
        {
            value -= 1 << length;
        }

        return value;
    }

    public static string GetSixBitText(string bits, int start, int length)
    {
        var builder = new StringBuilder();
        for (var offset = start; offset < start + length; offset += 6)
        {
            var idx = GetUInt(bits, offset, 6);
            builder.Append(SixBitAscii[idx]);
        }

        return builder.ToString().Replace('@', ' ').Trim();
    }

    public static double? DecodeLongitude(int raw) => raw == 0x6791AC0 ? null : Math.Round(raw / 600000.0, 6);

    public static double? DecodeLatitude(int raw) => raw == 0x3412140 ? null : Math.Round(raw / 600000.0, 6);

    public static double? DecodeSpeed(int raw) => raw is 1022 or 1023 ? null : raw / 10.0;

    public static double? DecodeCourse(int raw) => raw >= 3600 ? null : raw / 10.0;

    public static int? DecodeHeading(int raw) => raw >= 511 ? null : raw;
}
