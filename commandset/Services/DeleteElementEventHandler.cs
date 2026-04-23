using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class DeleteElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // 실행 결과
        public bool IsSuccess { get; private set; }

        // 성공적으로 삭제된 엘리먼트 수
        public int DeletedCount { get; private set; }
        // 상태 동기화 객체
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        // 삭제할 엘리먼트 ID 배열
        public string[] ElementIds { get; set; }
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
                var doc = app.ActiveUIDocument.Document;
                DeletedCount = 0;
                if (ElementIds == null || ElementIds.Length == 0)
                {
                    IsSuccess = false;
                    return;
                }
                // 삭제 대상 엘리먼트 ID 컬렉션 생성
                List<ElementId> elementIdsToDelete = new List<ElementId>();
                List<string> invalidIds = new List<string>();
                foreach (var idStr in ElementIds)
                {
                    if (int.TryParse(idStr, out int elementIdValue))
                    {
                        var elementId = new ElementId(elementIdValue);
                        // 엘리먼트 존재 여부 확인
                        if (doc.GetElement(elementId) != null)
                        {
                            elementIdsToDelete.Add(elementId);
                        }
                    }
                    else
                    {
                        invalidIds.Add(idStr);
                    }
                }
                if (invalidIds.Count > 0)
                {
                    TaskDialog.Show("경고", $"다음 ID가 유효하지 않거나 엘리먼트가 존재하지 않음: {string.Join(", ", invalidIds)}");
                }
                // 삭제 가능한 엘리먼트가 있으면, 삭제 실행
                if (elementIdsToDelete.Count > 0)
                {
                    using (var transaction = new Transaction(doc, "Delete Elements"))
                    {
                        transaction.Start();

                        // 엘리먼트 일괄 삭제
                        ICollection<ElementId> deletedIds = doc.Delete(elementIdsToDelete);
                        DeletedCount = deletedIds.Count;

                        transaction.Commit();
                    }
                    IsSuccess = true;
                }
                else
                {
                    TaskDialog.Show("오류", "삭제할 수 있는 유효한 엘리먼트가 없음");
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("오류", "엘리먼트 삭제 실패: " + ex.Message);
                IsSuccess = false;
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }
        public string GetName()
        {
            return "엘리먼트 삭제";
        }
    }
}
