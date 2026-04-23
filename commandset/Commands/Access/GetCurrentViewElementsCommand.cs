using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Access
{
    public class GetCurrentViewElementsCommand : ExternalEventCommandBase
    {
        private GetCurrentViewElementsEventHandler _handler => (GetCurrentViewElementsEventHandler)Handler;

        public override string CommandName => "get_current_view_elements";

        public GetCurrentViewElementsCommand(UIApplication uiApp)
            : base(new GetCurrentViewElementsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // 파라미터 파싱
                List<string> modelCategoryList = parameters?["modelCategoryList"]?.ToObject<List<string>>() ?? new List<string>();
                List<string> annotationCategoryList = parameters?["annotationCategoryList"]?.ToObject<List<string>>() ?? new List<string>();
                bool includeHidden = parameters?["includeHidden"]?.Value<bool>() ?? false;
                int limit = parameters?["limit"]?.Value<int>() ?? 100;

                // 조회 파라미터 설정
                _handler.SetQueryParameters(modelCategoryList, annotationCategoryList, includeHidden, limit);

                // 외부 이벤트를 트리거하고 완료 대기
                if (RaiseAndWaitForCompletion(60000)) // 60초 타임아웃
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("뷰 엘리먼트 가져오기 시간 초과");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"뷰 엘리먼트 가져오기 실패: {ex.Message}");
            }
        }
    }
}
