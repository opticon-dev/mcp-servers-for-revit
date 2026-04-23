using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class CreateLineElementCommand : ExternalEventCommandBase
    {
        private CreateLineElementEventHandler _handler => (CreateLineElementEventHandler)Handler;

        /// <summary>
        /// 명령 이름
        /// </summary>
        public override string CommandName => "create_line_based_element";

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public CreateLineElementCommand(UIApplication uiApp)
            : base(new CreateLineElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                List<LineElement> data = new List<LineElement>();
                // 파라미터 파싱
                data = parameters["data"].ToObject<List<LineElement>>();
                if (data == null)
                    throw new ArgumentNullException(nameof(data), "AI에서 전달된 데이터가 비어 있음");

                // 선형 구성요소 파라미터 설정
                _handler.SetParameters(data);

                // 외부 이벤트를 트리거하고 완료 대기
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("선형 구성요소 생성 작업 시간 초과");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"선형 구성요소 생성 실패: {ex.Message}");
            }
        }
    }
}
