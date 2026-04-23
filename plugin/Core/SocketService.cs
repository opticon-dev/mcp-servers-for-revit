using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Models.JsonRPC;
using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;

namespace revit_mcp_plugin.Core
{
    public class SocketService
    {
        private static SocketService _instance;
        private TcpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private int _port = 8080;
        private UIApplication _uiApp;
        private ICommandRegistry _commandRegistry;
        private ILogger _logger;
        private CommandExecutor _commandExecutor;

        public static SocketService Instance
        {
            get
            {
                if(_instance == null)
                    _instance = new SocketService();
                return _instance;
            }
        }

        private SocketService()
        {
            _commandRegistry = new RevitCommandRegistry();
            _logger = new Logger();
        }

        public bool IsRunning => _isRunning;

        public int Port
        {
            get => _port;
            set => _port = value;
        }

        // 초기화
        // Initialization.
        public void Initialize(UIApplication uiApp)
        {
            _uiApp = uiApp;

            // 이벤트 관리자 초기화
            // Initialize ExternalEventManager
            ExternalEventManager.Instance.Initialize(uiApp, _logger);

            // 현재 Revit 버전 기록
            // Get the current Revit version.
            var versionAdapter = new RevitMCPSDK.API.Utils.RevitVersionAdapter(_uiApp.Application);
            string currentVersion = versionAdapter.GetRevitVersion();
            _logger.Info("현재 Revit 버전: {0}\nCurrent Revit version: {0}", currentVersion);



            // 명령 실행기 생성
            // Create CommandExecutor
            _commandExecutor = new CommandExecutor(_commandRegistry, _logger);

            // 설정을 로드하고 명령을 등록
            // Load configuration and register commands.
            ConfigurationManager configManager = new ConfigurationManager(_logger);
            configManager.LoadConfiguration();
            

            //// 설정에서 서비스 포트를 읽기
            //// Read the service port from the configuration.
            //if (configManager.Config.Settings.Port > 0)
            //{
            //    _port = configManager.Config.Settings.Port;
            //}
            _port = 8080; // 고정 포트 번호

            // 명령 로드
            // Load command.
            CommandManager commandManager = new CommandManager(
                _commandRegistry, _logger, configManager, _uiApp);
            commandManager.LoadCommands();

            _logger.Info($"Socket service initialized on port {_port}");
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _isRunning = true;
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();

                _listenerThread = new Thread(ListenForClients)
                {
                    IsBackground = true
                };
                _listenerThread.Start();              
            }
            catch (Exception)
            {
                _isRunning = false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                _isRunning = false;

                _listener?.Stop();
                _listener = null;

                if(_listenerThread!=null && _listenerThread.IsAlive)
                {
                    _listenerThread.Join(1000);
                }
            }
            catch (Exception)
            {
                // log error
            }
        }

        private void ListenForClients()
        {
            try
            {
                while (_isRunning)
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    Thread clientThread = new Thread(HandleClientCommunication)
                    {
                        IsBackground = true
                    };
                    clientThread.Start(client);
                }
            }
            catch (SocketException)
            {
                
            }
            catch(Exception)
            {
                // log
            }
        }

        private void HandleClientCommunication(object clientObj)
        {
            TcpClient tcpClient = (TcpClient)clientObj;
            NetworkStream stream = tcpClient.GetStream();

            try
            {
                byte[] buffer = new byte[8192];

                while (_isRunning && tcpClient.Connected)
                {
                    // 클라이언트 메시지 읽기
                    // Read client messages.
                    int bytesRead = 0;

                    try
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (IOException)
                    {
                        // 클라이언트 연결 끊김
                        // Client disconnected.
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        // 클라이언트 연결 끊김
                        // Client disconnected.
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    System.Diagnostics.Trace.WriteLine($"메시지 수신: {message}\nReceived message: {message}");

                    string response = ProcessJsonRPCRequest(message);

                    // 응답 전송
                    // Send response.
                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                }
            }
            catch(Exception)
            {
                // log
            }
            finally
            {
                tcpClient.Close();
            }
        }

        private string ProcessJsonRPCRequest(string requestJson)
        {
            JsonRPCRequest request;

            try
            {
                // JSON-RPC 요청 파싱
                // Parse JSON-RPC requests.
                request = JsonConvert.DeserializeObject<JsonRPCRequest>(requestJson);

                // 요청 형식이 유효한지 검증
                // Verify that the request format is valid.
                if (request == null || !request.IsValid())
                {
                    return CreateErrorResponse(
                        null,
                        JsonRPCErrorCodes.InvalidRequest,
                        "Invalid JSON-RPC request"
                    );
                }

                // 명령 찾기
                // Search for the command in the registry.
                if (!_commandRegistry.TryGetCommand(request.Method, out var command))
                {
                    return CreateErrorResponse(request.Id, JsonRPCErrorCodes.MethodNotFound,
                        $"Method '{request.Method}' not found");
                }

                // 명령 실행
                // Execute command.
                try
                {                
                    object result = command.Execute(request.GetParamsObject(), request.Id);

                    return CreateSuccessResponse(request.Id, result);
                }
                catch (Exception ex)
                {
                    return CreateErrorResponse(request.Id, JsonRPCErrorCodes.InternalError, ex.Message);
                }
            }
            catch (JsonException)
            {
                // JSON 파싱 오류
                // JSON parsing error.
                return CreateErrorResponse(
                    null,
                    JsonRPCErrorCodes.ParseError,
                    "Invalid JSON"
                );
            }
            catch (Exception ex)
            {
                // 요청 처리 중 발생한 기타 오류
                // Catch other errors produced when processing requests.
                return CreateErrorResponse(
                    null,
                    JsonRPCErrorCodes.InternalError,
                    $"Internal error: {ex.Message}"
                );
            }
        }

        private string CreateSuccessResponse(string id, object result)
        {
            var response = new JsonRPCSuccessResponse
            {
                Id = id,
                Result = result is JToken jToken ? jToken : JToken.FromObject(result)
            };

            return response.ToJson();
        }

        private string CreateErrorResponse(string id, int code, string message, object data = null)
        {
            var response = new JsonRPCErrorResponse
            {
                Id = id,
                Error = new JsonRPCError
                {
                    Code = code,
                    Message = message,
                    Data = data != null ? JToken.FromObject(data) : null
                }
            };

            return response.ToJson();
        }
    }
}
