using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
///     3차원 선분
/// </summary>
public class JZLine
{
    /// <summary>
    ///     생성자
    /// </summary>
    public JZLine()
    {
    }

    /// <summary>
    ///     생성자
    /// </summary>
    public JZLine(JZPoint p0, JZPoint p1)
    {
        P0 = p0;
        P1 = p1;
    }

    /// <summary>
    ///     4개의 double을 매개변수로 받는 생성자
    /// </summary>
    /// <param name="x0">시작점 X 좌표</param>
    /// <param name="y0">시작점 Y 좌표</param>
    /// <param name="z0">시작점 Z 좌표</param>
    /// <param name="x1">끝점 X 좌표</param>
    /// <param name="y1">끝점 Y 좌표</param>
    /// <param name="z1">끝점 Z 좌표</param>
    public JZLine(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        P0 = new JZPoint(x0, y0, z0);
        P1 = new JZPoint(x1, y1, z1);
    }

    /// <summary>
    ///     4개의 double을 매개변수로 받는 생성자
    /// </summary>
    /// <param name="x0">시작점 X 좌표</param>
    /// <param name="y0">시작점 Y 좌표</param>
    /// <param name="z0">시작점 Z 좌표</param>
    /// <param name="x1">끝점 X 좌표</param>
    /// <param name="y1">끝점 Y 좌표</param>
    /// <param name="z1">끝점 Z 좌표</param>
    public JZLine(double x0, double y0, double x1, double y1)
    {
        P0 = new JZPoint(x0, y0, 0);
        P1 = new JZPoint(x1, y1, 0);
    }

    /// <summary>
    ///     시작점
    /// </summary>
    [JsonProperty("p0")]
    public JZPoint P0 { get; set; }

    /// <summary>
    ///     끝점
    /// </summary>
    [JsonProperty("p1")]
    public JZPoint P1 { get; set; }

    /// <summary>
    ///     선분의 길이를 가져오기
    /// </summary>
    public double GetLength()
    {
        if (P0 == null || P1 == null)
            throw new InvalidOperationException("JZLine must have both P0 and P1 defined to calculate length.");

        // 3차원 점 사이의 거리 계산
        var dx = P1.X - P0.X;
        var dy = P1.Y - P0.Y;
        var dz = P1.Z - P0.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    ///     선분의 방향을 가져오기
    ///     정규화된 JZPoint를 방향 벡터로 반환
    /// </summary>
    public JZPoint GetDirection()
    {
        if (P0 == null || P1 == null)
            throw new InvalidOperationException("JZLine must have both P0 and P1 defined to calculate direction.");

        // 방향 벡터 계산
        var dx = P1.X - P0.X;
        var dy = P1.Y - P0.Y;
        var dz = P1.Z - P0.Z;

        // 벡터의 크기 계산
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        if (length == 0)
            throw new InvalidOperationException("Cannot determine direction for a line with zero length.");

        // 정규화된 벡터 반환
        return new JZPoint(dx / length, dy / length, dz / length);
    }

    /// <summary>
    ///     Revit의 Line으로 변환
    ///     단위 변환: mm -> ft
    /// </summary>
    public static Line ToLine(JZLine jzLine)
    {
        if (jzLine.P0 == null || jzLine.P1 == null) return null;

        return Line.CreateBound(JZPoint.ToXYZ(jzLine.P0), JZPoint.ToXYZ(jzLine.P1));
    }
}