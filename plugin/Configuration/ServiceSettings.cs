using Newtonsoft.Json;

namespace revit_mcp_plugin.Configuration
{
    /// <summary>
    /// <para>서비스 설정 클래스</para>
    /// <para>Service settings.</para>
    /// </summary>
    public class ServiceSettings
    {
        /// <summary>
        /// <para>로그 수준</para>
        /// <para>Log level.</para>
        /// </summary>
        [JsonProperty("logLevel")]
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// <para>socket 서비스 포트</para>
        /// <para>Socket service port.</para>
        /// </summary>
        [JsonProperty("port")]
        public int Port { get; set; } = 8080;

    }
}
