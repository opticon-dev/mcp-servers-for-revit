using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Access
{
    public class GetAvailableFamilyTypesCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private GetAvailableFamilyTypesEventHandler _handler => (GetAvailableFamilyTypesEventHandler)Handler;

        public override string CommandName => "get_available_family_types";

        public GetAvailableFamilyTypesCommand(UIApplication uiApp)
            : base(new GetAvailableFamilyTypesEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    // 파라미터 파싱
                    List<string> categoryList = parameters?["categoryList"]?.ToObject<List<string>>() ?? new List<string>();
                    string familyNameFilter = parameters?["familyNameFilter"]?.Value<string>();
                    int? limit = parameters?["limit"]?.Value<int>();

                    // 조회 파라미터 설정
                    _handler.CategoryList = categoryList;
                    _handler.FamilyNameFilter = familyNameFilter;
                    _handler.Limit = limit;

                    // 외부 이벤트를 트리거하고 완료 대기, 최대 15초까지 대기
                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return _handler.ResultFamilyTypes;
                    }
                    else
                    {
                        throw new TimeoutException("사용 가능한 패밀리 타입 가져오기 시간 초과");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"사용 가능한 패밀리 타입 가져오기 실패: {ex.Message}");
                }
            }
        }
    }
}
