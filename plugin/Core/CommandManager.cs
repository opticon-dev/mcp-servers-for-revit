using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPSDK.API.Utils;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;
using System;
using System.IO;
using System.Reflection;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// <para>명령을 로드하고 관리하는 명령 관리자</para>
    /// <para>Command Manager</para>
    /// </summary>
    public class CommandManager
    {
        private readonly ICommandRegistry _commandRegistry;
        private readonly ILogger _logger;
        private readonly ConfigurationManager _configManager;
        private readonly UIApplication _uiApplication;
        private readonly RevitVersionAdapter _versionAdapter;

        /// <summary>
        /// Manager in charge of loading and managing commands.
        /// </summary>
        /// <param name="commandRegistry"></param>
        /// <param name="logger"></param>
        /// <param name="configManager"></param>
        /// <param name="uiApplication"></param>
        public CommandManager(
            ICommandRegistry commandRegistry,
            ILogger logger,
            ConfigurationManager configManager,
            UIApplication uiApplication)
        {
            _commandRegistry = commandRegistry;
            _logger = logger;
            _configManager = configManager;
            _uiApplication = uiApplication;
            _versionAdapter = new RevitVersionAdapter(_uiApplication.Application);
        }

        /// <summary>
        /// <para>구성 파일에 지정된 모든 명령을 로드합니다.</para>
        /// <para>Load all commands specified in the configuration file.</para>
        /// </summary>
        public void LoadCommands()
        {
            _logger.Info("명령 로드 시작\nStart loading command.");
            string currentVersion = _versionAdapter.GetRevitVersion();
            _logger.Info("현재 Revit 버전: {0}\nCurrent Revit version: {0}", currentVersion);

            // 구성에서 외부 명령 로드
            // Load external commands from the configuration file.
            foreach (var commandConfig in _configManager.Config.Commands)
            {
                try
                {
                    if (!commandConfig.Enabled)
                    {
                        _logger.Info("비활성화된 명령 건너뜀: {0}\nSkipping disabled command: {0}", commandConfig.CommandName);
                        continue;
                    }

                    // 버전 호환성 확인
                    // Check Revit version compatibility.
                    if (commandConfig.SupportedRevitVersions != null &&
                        commandConfig.SupportedRevitVersions.Length > 0 &&
                        !_versionAdapter.IsVersionSupported(commandConfig.SupportedRevitVersions))
                    {
                        _logger.Warning("명령 {0}은 현재 Revit 버전 {1}을(를) 지원하지 않아 건너뜀\nThe command {0} is not supported by the current Revit version ({1}} and it has been skipped.",
                            commandConfig.CommandName, currentVersion);
                        continue;
                    }

                    // 경로의 버전 플레이스홀더 치환
                    // Replace version placeholder strings in paths.
                    commandConfig.AssemblyPath = commandConfig.AssemblyPath.Contains("{VERSION}")
                        ? commandConfig.AssemblyPath.Replace("{VERSION}", currentVersion)
                        : commandConfig.AssemblyPath;

                    // 외부 명령 어셈블리 로드
                    // Load external command assembly.
                    LoadCommandFromAssembly(commandConfig);
                }
                catch (Exception ex)
                {
                    _logger.Error("명령 {0} 로드 실패: {1}\nFailed to load command {0}: {1}", commandConfig.CommandName, ex.Message);
                }
            }

            _logger.Info("명령 로드 완료\nCommand loading complete.");
        }

        /// <summary>
        /// 특정 어셈블리의 특정 명령 로드
        /// Loads specific commands in specific assemblies.
        /// </summary>
        /// <param name="config">Configuration class describing the command.</param>
        private void LoadCommandFromAssembly(CommandConfig config)
        {
            try
            {
                // 어셈블리 경로 확인
                // Determine the assembly path.
                string assemblyPath = config.AssemblyPath;
                if (!Path.IsPathRooted(assemblyPath))
                {
                    // 절대 경로가 아니면 Commands 디렉터리 기준 상대 경로로 처리
                    // If it is not an absolute path, then it is relative to the Command's directory.
                    string baseDir = PathManager.GetCommandsDirectoryPath();
                    assemblyPath = Path.Combine(baseDir, assemblyPath);
                }

                if (!File.Exists(assemblyPath))
                {
                    _logger.Error("명령 어셈블리가 존재하지 않음: {0}\nCommand assembly does not exist: {0}", assemblyPath);
                    return;
                }

                // 어셈블리 로드
                // Load assembly.
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                // IRevitCommand 인터페이스를 구현하는 타입 찾기
                // Find types that implement the IRevitCommand interface.
                foreach (Type type in assembly.GetTypes())
                {
                    if (typeof(RevitMCPSDK.API.Interfaces.IRevitCommand).IsAssignableFrom(type) &&
                        !type.IsInterface &&
                        !type.IsAbstract)
                    {
                        try
                        {
                            // 명령 인스턴스 생성
                            // Create a command instance.
                            RevitMCPSDK.API.Interfaces.IRevitCommand command;

                            // 명령이 초기화 가능 인터페이스를 구현했는지 확인
                            // Check whether the command implements the initializable interface.
                            if (typeof(IRevitCommandInitializable).IsAssignableFrom(type))
                            {
                                // 인스턴스를 생성하고 초기화
                                // Create instance and initialize.
                                command = (IRevitCommand)Activator.CreateInstance(type);
                                ((IRevitCommandInitializable)command).Initialize(_uiApplication);
                            }
                            else
                            {
                                // UIApplication을 받는 생성자를 찾아 시도
                                // Try searching for constructors that accept UIApplication.
                                var constructor = type.GetConstructor(new[] { typeof(UIApplication) });
                                if (constructor != null)
                                {
                                    command = (IRevitCommand)constructor.Invoke(new object[] { _uiApplication });
                                }
                                else
                                {
                                    // 매개변수 없는 생성자 사용
                                    // Use a parameterless constructor.
                                    command = (IRevitCommand)Activator.CreateInstance(type);
                                }
                            }

                            // 명령 이름이 구성과 일치하는지 확인
                            // Check whether the command name matches the configuration.
                            if (command.CommandName == config.CommandName)
                            {
                                _commandRegistry.RegisterCommand(command);
                                _logger.Info("명령 인스턴스 생성 실패 [{0}]: {1}\nFailed to create command instance [{0}]: {1}",
                                    command.CommandName, Path.GetFileName(assemblyPath));
                                break; // 일치하는 명령을 찾으면 루프 종료 - Exit the loop after finding a matching command.
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("명령 인스턴스 생성 실패 [{0}]: {1}\nFailed to create command instance [{0}]: {1}", type.FullName, ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("명령 어셈블리 로드 실패: {0}\nFailed to load command assembly: {0}", ex.Message);
            }
        }
    }
}
