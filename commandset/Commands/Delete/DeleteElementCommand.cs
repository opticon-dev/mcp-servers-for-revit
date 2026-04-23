using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Delete
{
    public class DeleteElementCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private DeleteElementEventHandler _handler => (DeleteElementEventHandler)Handler;

        public override string CommandName => "delete_element";

        public DeleteElementCommand(UIApplication uiApp)
            : base(new DeleteElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    // 배열 파라미터 파싱
                    var elementIds = parameters?["elementIds"]?.ToObject<string[]>();
                    if (elementIds == null || elementIds.Length == 0)
                    {
                        throw new ArgumentException("엘리먼트 ID 목록은 비어 있을 수 없음");
                    }

                    // 삭제할 엘리먼트 ID 배열 설정
                    _handler.ElementIds = elementIds;

                    // 외부 이벤트를 트리거하고 완료 대기
                    if (RaiseAndWaitForCompletion(15000))
                    {
                        if (_handler.IsSuccess)
                        {
                            return new { deleted = true, count = _handler.DeletedCount };
                        }
                        else
                        {
                            throw new Exception("엘리먼트 삭제 실패");
                        }
                    }
                    else
                    {
                        throw new TimeoutException("엘리먼트 삭제 작업 시간 초과");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"엘리먼트 삭제 실패: {ex.Message}");
                }
            }
        }
    }
}
