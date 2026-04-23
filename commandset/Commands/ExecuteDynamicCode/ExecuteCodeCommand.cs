using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.ExecuteDynamicCode
{
    /// <summary>
    /// 코드 실행을 처리하는 명령 클래스
    /// </summary>
    public class ExecuteCodeCommand : ExternalEventCommandBase
    {
        private ExecuteCodeEventHandler _handler => (ExecuteCodeEventHandler)Handler;

        public override string CommandName => "send_code_to_revit";

        public ExecuteCodeCommand(UIApplication uiApp)
            : base(new ExecuteCodeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // 파라미터 검증
                if (!parameters.ContainsKey("code"))
                {
                    throw new ArgumentException("Missing required parameter: 'code'");
                }

                // 코드와 파라미터 파싱
                string code = parameters["code"].Value<string>();
                JArray parametersArray = parameters["parameters"] as JArray;
                object[] executionParameters = parametersArray?.ToObject<object[]>() ?? Array.Empty<object>();
                string transactionMode = parameters["transactionMode"]?.Value<string>() ?? ExecuteCodeEventHandler.TransactionModeAuto;

                // 실행 파라미터 설정
                _handler.SetExecutionParameters(code, executionParameters, transactionMode);

                // 외부 이벤트를 트리거하고 완료 대기
                if (RaiseAndWaitForCompletion(60000)) // 1분 타임아웃
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("코드 실행 시간 초과");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"코드 실행 실패: {ex.Message}", ex);
            }
        }
    }
}
