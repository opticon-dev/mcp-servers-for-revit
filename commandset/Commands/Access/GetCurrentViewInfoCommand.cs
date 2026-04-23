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
    public class GetCurrentViewInfoCommand : ExternalEventCommandBase
    {
        private GetCurrentViewInfoEventHandler _handler => (GetCurrentViewInfoEventHandler)Handler;

        public override string CommandName => "get_current_view_info";

        public GetCurrentViewInfoCommand(UIApplication uiApp)
            : base(new GetCurrentViewInfoEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            // 외부 이벤트를 트리거하고 완료 대기
            if (RaiseAndWaitForCompletion(10000)) // 10초 타임아웃
            {
                return _handler.ResultInfo;
            }
            else
            {
                throw new TimeoutException("정보 가져오기 시간 초과");
            }
        }
    }
}
