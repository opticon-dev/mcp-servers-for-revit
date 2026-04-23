using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
///     면형 구성요소
/// </summary>
public class SurfaceElement
{
    public SurfaceElement()
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
    ///     쉘형 윤곽 경계
    /// </summary>
    [JsonProperty("boundary")]
    public JZFace Boundary { get; set; }

    /// <summary>
    ///     두께
    /// </summary>
    [JsonProperty("thickness")]
    public double Thickness { get; set; }

    /// <summary>
    ///     하단 레벨
    /// </summary>
    [JsonProperty("baseLevel")]
    public double BaseLevel { get; set; }

    /// <summary>
    ///     하단 오프셋
    /// </summary>
    [JsonProperty("baseOffset")]
    public double BaseOffset { get; set; }

    /// <summary>
    ///     매개변수 속성
    /// </summary>
    public Dictionary<string, double> Parameters { get; set; }
}