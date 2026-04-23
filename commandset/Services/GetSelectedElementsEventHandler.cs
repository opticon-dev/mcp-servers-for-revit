using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Services
{
    public class GetSelectedElementsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // 실행 결과
        public List<Models.Common.ElementInfo> ResultElements { get; private set; }

        // 상태 동기화 객체
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // 반환되는 엘리먼트 수량 제한
        public int? Limit { get; set; }

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

                // 현재 선택된 엘리먼트 가져오기
                var selectedIds = uiDoc.Selection.GetElementIds();
                var selectedElements = selectedIds.Select(id => doc.GetElement(id)).ToList();

                // 수량 제한 적용
                if (Limit.HasValue && Limit.Value > 0)
                {
                    selectedElements = selectedElements.Take(Limit.Value).ToList();
                }

                // ElementInfo 목록으로 변환
                ResultElements = selectedElements.Select(element => new ElementInfo
                {
#if REVIT2024_OR_GREATER
                    Id = element.Id.Value,
#else
                    Id = element.Id.IntegerValue,
#endif
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    Category = element.Category?.Name
                }).ToList();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "선택된 엘리먼트 가져오기 실패: " + ex.Message);
                ResultElements = new List<Models.Common.ElementInfo>();
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "선택된 엘리먼트 가져오기";
        }
    }
}
