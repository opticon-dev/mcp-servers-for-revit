using Newtonsoft.Json;

namespace revit_mcp_plugin.Configuration
{
    /// <summary>
    /// <para>개발자 정보</para>
    /// <para>Developer information.</para>
    /// </summary>
    public class DeveloperInfo
    {
        /// <summary>
        /// <para>개발자 이름</para>
        /// <para>Developer name.</para>
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// <para>개발자 이메일</para>
        /// <para>Developer e-mail address.</para>
        /// </summary>
        [JsonProperty("email")]
        public string Email { get; set; } = "";

        /// <summary>
        /// <para>개발자 웹사이트</para>
        /// <para>Developer website.</para>
        /// </summary>
        [JsonProperty("website")]
        public string Website { get; set; } = "";

        /// <summary>
        /// <para>개발자 조직</para>
        /// <para>Developer Organization.</para>
        /// </summary>
        [JsonProperty("organization")]
        public string Organization { get; set; } = "";
    }
}
