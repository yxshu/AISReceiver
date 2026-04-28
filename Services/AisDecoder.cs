namespace AisConsoleReceiver;

/// <summary>
/// AIS 解码服务，负责串联 NMEA 解析、分片重组和 AIS 载荷解码。
/// </summary>
internal sealed class AisDecoder
{
    private readonly MultipartAssembler _assembler = new(TimeSpan.FromMinutes(2));

    /// <summary>
    /// 处理单条 AIS/NMEA 语句，并在可解码时返回结果。
    /// </summary>
    public IEnumerable<DecodedMessage> ProcessSentence(string sentence)
    {
        if (!NmeaSentenceParser.TryParse(sentence, out var nmea))
        {
            yield break;
        }

        var assembled = _assembler.Add(nmea);
        if (assembled is null)
        {
            yield break;
        }

        yield return AisPayloadDecoder.Decode(assembled.Payload, assembled.FillBits);
    }
}
