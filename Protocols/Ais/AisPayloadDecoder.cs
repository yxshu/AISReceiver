namespace AisConsoleReceiver;

/// <summary>
/// AIS 载荷解析器，负责将完整 AIS 负载解码为业务模型。
/// </summary>
internal static class AisPayloadDecoder
{
    /// <summary>
    /// 解码一条完整 AIS 负载。
    /// </summary>
    public static DecodedMessage Decode(string payload, int fillBits)
    {
        var bits = AisBitDecoder.SixBitPayloadToBits(payload, fillBits);
        if (!AisBitDecoder.HasBits(bits, 38))
        {
            return new DecodedMessage
            {
                MessageName = "AIS 载荷被截断"
            };
        }

        var messageType = AisBitDecoder.GetUInt(bits, 0, 6);
        var repeat = AisBitDecoder.GetUInt(bits, 6, 2);
        var mmsi = AisBitDecoder.GetUInt(bits, 8, 30);

        var result = new DecodedMessage
        {
            MessageType = messageType,
            RepeatIndicator = repeat,
            Mmsi = mmsi
        };

        switch (messageType)
        {
            case 1:
            case 2:
            case 3:
                result.MessageName = "A 类位置报告";
                if (AisBitDecoder.HasBits(bits, 42))
                {
                    result.NavigationStatus = AisCodeTables.NavigationStatusName(AisBitDecoder.GetUInt(bits, 38, 4));
                }

                FillPosition(result, bits, 61, 89, 50, 116, 128);
                break;
            case 5:
                result.MessageName = "静态与航次相关数据";
                if (AisBitDecoder.HasBits(bits, 112))
                {
                    result.CallSign = AisBitDecoder.GetSixBitText(bits, 70, 42);
                }

                if (AisBitDecoder.HasBits(bits, 232))
                {
                    result.ShipName = AisBitDecoder.GetSixBitText(bits, 112, 120);
                }

                if (AisBitDecoder.HasBits(bits, 240))
                {
                    result.ShipType = AisCodeTables.ShipTypeName(AisBitDecoder.GetUInt(bits, 232, 8));
                }

                if (AisBitDecoder.HasBits(bits, 422))
                {
                    result.Destination = AisBitDecoder.GetSixBitText(bits, 302, 120);
                }

                break;
            case 18:
                result.MessageName = "B 类位置报告";
                FillPosition(result, bits, 57, 85, 46, 112, 124);
                break;
            case 19:
                result.MessageName = "扩展 B 类位置报告";
                FillPosition(result, bits, 57, 85, 46, 112, 124);
                if (AisBitDecoder.HasBits(bits, 263))
                {
                    result.ShipName = AisBitDecoder.GetSixBitText(bits, 143, 120);
                }

                if (AisBitDecoder.HasBits(bits, 271))
                {
                    result.ShipType = AisCodeTables.ShipTypeName(AisBitDecoder.GetUInt(bits, 263, 8));
                }

                break;
            case 24:
                result.MessageName = "B 类静态数据";
                DecodeType24(result, bits);
                break;
            default:
                result.MessageName = $"AIS 消息类型 {messageType}";
                break;
        }

        return result;
    }

    private static void DecodeType24(DecodedMessage result, string bits)
    {
        if (!AisBitDecoder.HasBits(bits, 40))
        {
            return;
        }

        var partNumber = AisBitDecoder.GetUInt(bits, 38, 2);
        if (partNumber == 0)
        {
            if (AisBitDecoder.HasBits(bits, 160))
            {
                result.ShipName = AisBitDecoder.GetSixBitText(bits, 40, 120);
            }

            return;
        }

        if (AisBitDecoder.HasBits(bits, 48))
        {
            result.ShipType = AisCodeTables.ShipTypeName(AisBitDecoder.GetUInt(bits, 40, 8));
        }

        if (AisBitDecoder.HasBits(bits, 132))
        {
            result.CallSign = AisBitDecoder.GetSixBitText(bits, 90, 42);
        }
    }

    private static void FillPosition(DecodedMessage result, string bits, int lonStart, int latStart, int sogStart, int cogStart, int hdgStart)
    {
        if (AisBitDecoder.HasBits(bits, lonStart + 28))
        {
            result.Longitude = AisBitDecoder.DecodeLongitude(AisBitDecoder.GetInt(bits, lonStart, 28));
        }

        if (AisBitDecoder.HasBits(bits, latStart + 27))
        {
            result.Latitude = AisBitDecoder.DecodeLatitude(AisBitDecoder.GetInt(bits, latStart, 27));
        }

        if (AisBitDecoder.HasBits(bits, sogStart + 10))
        {
            result.SpeedKnots = AisBitDecoder.DecodeSpeed(AisBitDecoder.GetUInt(bits, sogStart, 10));
        }

        if (AisBitDecoder.HasBits(bits, cogStart + 12))
        {
            result.CourseDegrees = AisBitDecoder.DecodeCourse(AisBitDecoder.GetUInt(bits, cogStart, 12));
        }

        if (AisBitDecoder.HasBits(bits, hdgStart + 9))
        {
            result.HeadingDegrees = AisBitDecoder.DecodeHeading(AisBitDecoder.GetUInt(bits, hdgStart, 9));
        }
    }
}
