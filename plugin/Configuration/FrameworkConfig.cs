using Newtonsoft.Json;
using System.Collections.Generic;

namespace revit_mcp_plugin.Configuration
{
    /// <summary>
    /// <para>프레임워크 구성 클래스</para>
    /// <para>Framework configuration class.</para>
    /// </summary>
    public class FrameworkConfig
    {
        /// <summary>
        /// <para>명령 구성 목록</para>
        /// <para>Command configuration list.</para>
        /// </summary>
        [JsonProperty("commands")]
        public List<CommandConfig> Commands { get; set; } = new List<CommandConfig>();

        /// <summary>
        /// <para>전역 설정</para>
        /// <para>Global settings.</para>
        /// </summary>
        [JsonProperty("settings")]
        public ServiceSettings Settings { get; set; } = new ServiceSettings();
    }
}
