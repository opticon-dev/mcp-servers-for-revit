namespace RevitMCPCommandSet.Models.Common;

public class AIResult<T>
{
    /// <summary>
    ///     성공 여부
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     메시지
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    ///     반환 데이터
    /// </summary>
    public T Response { get; set; }
}