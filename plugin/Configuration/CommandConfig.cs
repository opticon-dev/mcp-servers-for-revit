using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;

namespace revit_mcp_plugin.Configuration
{
    /// <summary>
    /// <para>명령 구성 클래스</para>
    /// <para>Command configuration class.</para>
    /// </summary>
    public class CommandConfig
    {
        /// <summary>
        /// <para>명령 이름 - IRevitCommand.CommandName에 대응</para>
        /// <para>Name of the command. Corresponds to <see cref="IRevitCommand.CommandName"/></para>
        /// </summary>
        [JsonProperty("commandName")]
        public string CommandName { get; set; }

        /// <summary>
        /// <para>어셈블리 경로 - 이 명령을 포함하는 DLL</para>
        /// <para>Assembly path - DLL containing this command.</para>
        /// </summary>
        [JsonProperty("assemblyPath")]
        public string AssemblyPath { get; set; }

        /// <summary>
        /// <para>이 명령 사용 여부</para>
        /// <para>Enable this command.</para>
        /// </summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// <para>지원되는 Revit 버전</para>
        /// <para>Supported Revit versions.</para>
        /// </summary>
        [JsonProperty("supportedRevitVersions")]
        public string[] SupportedRevitVersions { get; set; } = new string[0];

        /// <summary>
        /// <para>개발자 정보</para>
        /// <para>Developer information.</para>
        /// </summary>
        [JsonProperty("developer")]
        public DeveloperInfo Developer { get; set; } = new DeveloperInfo();

        /// <summary>
        /// <para>명령 설명</para>
        /// <para>Command description.</para>
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; } = "";
    }
}
