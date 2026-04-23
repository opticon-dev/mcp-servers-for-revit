using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
///     3차원 면
/// </summary>
public class JZFace
{
    /// <summary>
    ///     생성자
    /// </summary>
    public JZFace()
    {
        InnerLoops = new List<List<JZLine>>();
        OuterLoop = new List<JZLine>();
    }

    /// <summary>
    ///     외부 루프 (List<List<JZLine>> 타입)
    /// </summary>
    [JsonProperty("outerLoop")]
    public List<JZLine> OuterLoop { get; set; }

    /// <summary>
    ///     내부 루프 (List<JZLine> 타입, 하나 또는 여러 개의 내부 루프를 의미)
    /// </summary>
    [JsonProperty("innerLoops")]
    public List<List<JZLine>> InnerLoops { get; set; }
}