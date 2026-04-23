using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Commands.Access
{
    public class GetSelectedElementsCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private GetSelectedElementsEventHandler _handler => (GetSelectedElementsEventHandler)Handler;

        public override string CommandName => "get_selected_elements";

        public GetSelectedElementsCommand(UIApplication uiApp)
            : base(new GetSelectedElementsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    // 파라미터 파싱
                    int? limit = parameters?["limit"]?.Value<int>();

                    // 수량 제한 설정
                    _handler.Limit = limit;

                    // 외부 이벤트를 트리거하고 완료 대기
                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.ResultElements;
                    }
                    else
                    {
                        throw new TimeoutException("선택된 엘리먼트 가져오기 시간 초과");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"선택된 엘리먼트 가져오기 실패: {ex.Message}");
                }
            }
        }
    }
}
