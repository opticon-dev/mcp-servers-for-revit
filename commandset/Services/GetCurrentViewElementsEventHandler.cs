using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetCurrentViewElementsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        // 기본 모델 카테고리 목록
        private readonly List<string> _defaultModelCategories = new List<string>
        {
            "OST_Walls",
            "OST_Doors",
            "OST_Windows",
            "OST_Furniture",
            "OST_Columns",
            "OST_Floors",
            "OST_Roofs",
            "OST_Stairs",
            "OST_StructuralFraming",
            "OST_Ceilings",
            "OST_MEPSpaces",
            "OST_Rooms"
        };
        // 기본 주석 카테고리 목록
        private readonly List<string> _defaultAnnotationCategories = new List<string>
        {
            "OST_Dimensions",
            "OST_TextNotes",
            "OST_GenericAnnotation",
            "OST_WallTags",
            "OST_DoorTags",
            "OST_WindowTags",
            "OST_RoomTags",
            "OST_AreaTags",
            "OST_SpaceTags",
            "OST_ViewportLabels",
            "OST_TitleBlocks"
        };

        // 조회 파라미터
        private List<string> _modelCategoryList;
        private List<string> _annotationCategoryList;
        private bool _includeHidden;
        private int _limit;

        // 실행 결과
        public ViewElementsResult ResultInfo { get; private set; }

        // 상태 동기화 객체
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // 조회 파라미터 설정
        public void SetQueryParameters(List<string> modelCategoryList, List<string> annotationCategoryList, bool includeHidden, int limit)
        {
            _modelCategoryList = modelCategoryList;
            _annotationCategoryList = annotationCategoryList;
            _includeHidden = includeHidden;
            _limit = limit;
            TaskCompleted = false;
            _resetEvent.Reset();
        }

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


                // 모든 카테고리 병합
                List<string> allCategories = new List<string>();
                if (_modelCategoryList == null && _annotationCategoryList == null)
                {
                    allCategories.AddRange(_defaultModelCategories);
                    allCategories.AddRange(_defaultAnnotationCategories);
                }
                else
                {
                    allCategories.AddRange(_modelCategoryList ?? new List<string>());
                    allCategories.AddRange(_annotationCategoryList ?? new List<string>());
                }

                // 현재 뷰의 모든 엘리먼트 가져오기
                var collector = new FilteredElementCollector(doc, activeView.Id)
                    .WhereElementIsNotElementType();

                // 모든 엘리먼트 가져오기
                IList<Element> elements = collector.ToElements();

                // 카테고리별 필터링
                if (allCategories.Count > 0)
                {
                    // 문자열 카테고리를 열거형으로 변환
                    List<BuiltInCategory> builtInCategories = new List<BuiltInCategory>();
                    foreach (string categoryName in allCategories)
                    {
                        if (Enum.TryParse(categoryName, out BuiltInCategory category))
                        {
                            builtInCategories.Add(category);
                        }
                    }
                    // 카테고리 파싱이 성공하면 카테고리 필터 사용
                    if (builtInCategories.Count > 0)
                    {
                        ElementMulticategoryFilter categoryFilter = new ElementMulticategoryFilter(builtInCategories);
                        elements = new FilteredElementCollector(doc, activeView.Id)
                            .WhereElementIsNotElementType()
                            .WherePasses(categoryFilter)
                            .ToElements();
                    }
                }

                // 숨겨진 엘리먼트 필터링
                if (!_includeHidden)
                {
                    elements = elements.Where(e => !e.IsHidden(activeView)).ToList();
                }

                // 반환 수량 제한
                if (_limit > 0 && elements.Count > _limit)
                {
                    elements = elements.Take(_limit).ToList();
                }

                // 결과 생성
                var elementInfos = elements.Select(e => new ElementInfo
                {
#if REVIT2024_OR_GREATER
                    Id = e.Id.Value,
#else
                    Id = e.Id.IntegerValue,
#endif
                    UniqueId = e.UniqueId,
                    Name = e.Name,
                    Category = e.Category?.Name ?? "unknow",
                    Properties = GetElementProperties(e)
                }).ToList();

                ResultInfo = new ViewElementsResult
                {
#if REVIT2024_OR_GREATER
                    ViewId = activeView.Id.Value,
#else
                    ViewId = activeView.Id.IntegerValue,
#endif
                    ViewName = activeView.Name,
                    TotalElementsInView = new FilteredElementCollector(doc, activeView.Id).GetElementCount(),
                    FilteredElementCount = elementInfos.Count,
                    Elements = elementInfos
                };
            }
            catch (Exception ex)
            {
                TaskDialog.Show("error", ex.Message);
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private Dictionary<string, string> GetElementProperties(Element element)
        {
            var properties = new Dictionary<string, string>();

            // 공통 속성 추가
#if REVIT2024_OR_GREATER
            properties.Add("ElementId", element.Id.Value.ToString());
#else
            properties.Add("ElementId", element.Id.IntegerValue.ToString());
#endif
            if (element.Location != null)
            {
                if (element.Location is LocationPoint locationPoint)
                {
                    var point = locationPoint.Point;
                    properties.Add("LocationX", point.X.ToString("F2"));
                    properties.Add("LocationY", point.Y.ToString("F2"));
                    properties.Add("LocationZ", point.Z.ToString("F2"));
                }
                else if (element.Location is LocationCurve locationCurve)
                {
                    var curve = locationCurve.Curve;
                    properties.Add("Start", $"{curve.GetEndPoint(0).X:F2}, {curve.GetEndPoint(0).Y:F2}, {curve.GetEndPoint(0).Z:F2}");
                    properties.Add("End", $"{curve.GetEndPoint(1).X:F2}, {curve.GetEndPoint(1).Y:F2}, {curve.GetEndPoint(1).Z:F2}");
                    properties.Add("Length", curve.Length.ToString("F2"));
                }
            }

            // 자주 사용되는 파라미터 값 가져오기
            var commonParams = new[] { "Comments", "Mark", "Level", "Family", "Type" };
            foreach (var paramName in commonParams)
            {
                Parameter param = element.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly)
                {
                    if (param.StorageType == StorageType.String)
                        properties.Add(paramName, param.AsString() ?? "");
                    else if (param.StorageType == StorageType.Double)
                        properties.Add(paramName, param.AsDouble().ToString("F2"));
                    else if (param.StorageType == StorageType.Integer)
                        properties.Add(paramName, param.AsInteger().ToString());
                    else if (param.StorageType == StorageType.ElementId)
#if REVIT2024_OR_GREATER
                        properties.Add(paramName, param.AsElementId().Value.ToString());
#else
                        properties.Add(paramName, param.AsElementId().IntegerValue.ToString());
#endif
                }
            }

            return properties;
        }

        public string GetName()
        {
            return "현재 뷰 엘리먼트 가져오기";
        }
    }
}
