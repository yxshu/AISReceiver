namespace AisConsoleReceiver;

/// <summary>
/// NMEA 语句解析器。
/// </summary>
internal static class NmeaSentenceParser
{
    /// <summary>
    /// 尝试把原始文本解析成 NMEA 语句对象。
    /// </summary>
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
