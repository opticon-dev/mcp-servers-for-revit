using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class TagWallsCommand : ExternalEventCommandBase
    {
        private TagWallsEventHandler _handler => (TagWallsEventHandler)Handler;

        /// <summary>
        /// 명령 이름
        /// </summary>
        public override string CommandName => "tag_walls";

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public TagWallsCommand(UIApplication uiApp)
            : base(new TagWallsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // 파라미터 파싱
                bool useLeader = false;
                if (parameters["useLeader"] != null)
                {
                    useLeader = parameters["useLeader"].ToObject<bool>();
                }

                string tagTypeId = null;
                if (parameters["tagTypeId"] != null)
                {
                    tagTypeId = parameters["tagTypeId"].ToString();
                }

                // 태깅 파라미터 설정
                _handler.SetParameters(useLeader, tagTypeId);

                // 외부 이벤트를 트리거하고 완료 대기
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.TaggingResults;
                }
                else
                {
                    throw new TimeoutException("벽 태깅 작업 시간 초과");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"벽 태깅 실패: {ex.Message}");
            }
        }
    }
}