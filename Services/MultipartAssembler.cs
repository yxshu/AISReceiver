using System.Text;

namespace AisConsoleReceiver;

/// <summary>
/// 负责缓存并重组 AIS 多分片报文。
/// </summary>
internal sealed class MultipartAssembler
{
    private readonly TimeSpan _timeout;
    private readonly Dictionary<string, MultipartState> _pending = new();

    /// <summary>
    /// 初始化分片重组器。
    /// </summary>
    public MultipartAssembler(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    /// <summary>
    /// 添加一条 NMEA 语句；如果分片齐全，则返回组装后的负载。
    /// </summary>
    public AssembledPayload? Add(NmeaSentence sentence)
    {
        PurgeExpired();

        if (sentence.FragmentCount == 1)
        {
            return new AssembledPayload(sentence.Payload, sentence.FillBits);
        }

        var key = $"{sentence.Talker}|{sentence.Channel}|{sentence.SequenceId}|{sentence.FragmentCount}";
        if (!_pending.TryGetValue(key, out var state))
        {
            state = new MultipartState();
            _pending[key] = state;
        }

        state.UpdatedAtUtc = DateTime.UtcNow;
        state.Parts[sentence.FragmentNumber] = sentence.Payload;

        if (state.Parts.Count != sentence.FragmentCount)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var i = 1; i <= sentence.FragmentCount; i++)
        {
            if (!state.Parts.TryGetValue(i, out var part))
            {
                return null;
            }

            builder.Append(part);
        }

        _pending.Remove(key);
        return new AssembledPayload(builder.ToString(), sentence.FillBits);
    }

    /// <summary>
    /// 清理已超时的分片缓存。
    /// </summary>
    private void PurgeExpired()
    {
        var expired = _pending
            .Where(kvp => DateTime.UtcNow - kvp.Value.UpdatedAtUtc > _timeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expired)
        {
            _pending.Remove(key);
        }
    }

    /// <summary>
    /// 多分片报文在缓存中的临时状态。
    /// </summary>
    private sealed class MultipartState
    {
        /// <summary>
        /// 已接收的分片内容，键为分片序号。
        /// </summary>
        public Dictionary<int, string> Parts { get; } = new();

        /// <summary>
        /// 最近一次更新该分片状态的 UTC 时间。
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
