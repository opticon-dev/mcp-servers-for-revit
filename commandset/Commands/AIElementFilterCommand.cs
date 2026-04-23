using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Commands
{
    public class AIElementFilterCommand : ExternalEventCommandBase
    {
        private AIElementFilterEventHandler _handler => (AIElementFilterEventHandler)Handler;

        /// <summary>
        /// 명령 이름
        /// </summary>
        public override string CommandName => "ai_element_filter";

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public AIElementFilterCommand(UIApplication uiApp)
            : base(new AIElementFilterEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                FilterSetting data = new FilterSetting();
                // 파라미터 파싱
                data = parameters["data"].ToObject<FilterSetting>();
                if (data == null)
                    throw new ArgumentNullException(nameof(data), "AI에서 전달된 데이터가 비어 있음");

                // AI 필터 파라미터 설정
                _handler.SetParameters(data);

                // 외부 이벤트를 트리거하고 완료 대기
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("엘리먼트 정보 가져오기 작업 시간 초과");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"엘리먼트 정보 가져오기 실패: {ex.Message}");
            }
        }
    }
}
