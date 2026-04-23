using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Utils;
using System;
using System.IO;

namespace revit_mcp_plugin.Configuration
{
    public class ConfigurationManager
    {
        private readonly ILogger _logger;
        private readonly string _configPath;

        public FrameworkConfig Config { get; private set; }

        public ConfigurationManager(ILogger logger)
        {
            _logger = logger;

            // 구성 파일 경로
            // Configuration file path.
            _configPath = PathManager.GetCommandRegistryFilePath();
        }

        /// <summary>
        /// <para>구성 로드</para>
        /// <para>Load configuration from a JSON file.</para>
        /// </summary>
        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    Config = JsonConvert.DeserializeObject<FrameworkConfig>(json);
                    _logger.Info("구성 파일을 로드함: {0}\nConfiguration file loaded: {0}", _configPath);
                }
                else
                {
                    _logger.Error("구성 파일을 찾을 수 없음\nNo configuration file found.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("구성 파일 로드 실패: {0}\nFailed to load configuration file: {0}", ex.Message);
            }

            // 로드 시간 기록
            // Register load time.
            _lastConfigLoadTime = DateTime.Now;
        }

        ///// <summary>
        ///// <para>구성 다시 로드</para>
        ///  <para>Reload configuration.</para>
        ///// </summary>
        //public void RefreshConfiguration()
        //{
        //    LoadConfiguration();
        //// _logger.Info("구성이 다시 로드됨\nConfiguration has been reloaded.");
        //}

        //public bool HasConfigChanged()
        //{
        //    if (!File.Exists(_configPath))
        //        return false;

        //    DateTime lastWrite = File.GetLastWriteTime(_configPath);
        //    return lastWrite > _lastConfigLoadTime;
        //}

        private DateTime _lastConfigLoadTime;
    }
}
