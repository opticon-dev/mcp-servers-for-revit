using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
///     3차원 점
/// </summary>
public class JZPoint
{
    /// <summary>
    ///     생성자
    /// </summary>
    public JZPoint()
    {
    }

    /// <summary>
    ///     생성자
    /// </summary>
    public JZPoint(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    ///     생성자
    /// </summary>
    public JZPoint(double x, double y)
    {
        X = x;
        Y = y;
        Z = 0;
    }

    [JsonProperty("x")] public double X { get; set; }

    [JsonProperty("y")] public double Y { get; set; }

    [JsonProperty("z")] public double Z { get; set; }

    /// <summary>
    ///     Revit의 XYZ 점으로 변환
    ///     단위 변환: mm -> ft
    /// </summary>
    public static XYZ ToXYZ(JZPoint jzPoint)
    {
        return new XYZ(jzPoint.X / 304.8, jzPoint.Y / 304.8, jzPoint.Z / 304.8);
    }
}