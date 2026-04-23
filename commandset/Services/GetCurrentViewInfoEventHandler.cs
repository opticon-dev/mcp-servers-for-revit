using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetCurrentViewInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // 실행 결과
        public CurrentViewInfo ResultInfo { get; private set; }

        // 상태 동기화 객체
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // IWaitableExternalEventHandler 인터페이스 구현
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;
                var activeView = doc.ActiveView;

                ResultInfo = new CurrentViewInfo
                {
#if REVIT2024_OR_GREATER
                    Id = (int)activeView.Id.Value,
#else
                    Id = activeView.Id.IntegerValue,
#endif
                    UniqueId = activeView.UniqueId,
                    Name = activeView.Name,
                    ViewType = activeView.ViewType.ToString(),
                    IsTemplate = activeView.IsTemplate,
                    Scale = activeView.Scale,
                    DetailLevel = activeView.DetailLevel.ToString(),
                };
            }
            catch (Exception ex)
            {
                TaskDialog.Show("error", "정보 가져오기 실패");
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "현재 뷰 정보 가져오기";
        }
    }
}
