using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
/// 선형 구성요소
/// </summary>
public class LineElement
{
    public LineElement()
    {
        Parameters = new Dictionary<string, double>();
    }

    /// <summary>
    ///     구성요소 타입
    /// </summary>
    [JsonProperty("category")]
    public string Category { get; set; } = "INVALID";

    /// <summary>
    ///     타입 ID
    /// </summary>
    [JsonProperty("typeId")]
    public int TypeId { get; set; } = -1;

    /// <summary>
    ///     경로 곡선
    /// </summary>
    [JsonProperty("locationLine")]
    public JZLine LocationLine { get; set; }

    /// <summary>
    ///     두께
    /// </summary>
    [JsonProperty("thickness")]
    public double Thickness { get; set; }

    /// <summary>
    ///     높이
    /// </summary>
    [JsonProperty("height")]
    public double Height { get; set; }

    /// <summary>
    ///     하단 레벨
    /// </summary>
    [JsonProperty("baseLevel")]
    public double BaseLevel { get; set; }

    /// <summary>
    ///     하단 오프셋 / 면 기반 오프셋
    /// </summary>
    [JsonProperty("baseOffset")]
    public double BaseOffset { get; set; }

    /// <summary>
    ///     매개변수 속성
    /// </summary>
    [JsonProperty("parameters")]
    public Dictionary<string, double> Parameters { get; set; }
}