using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class TagWallsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;

        /// <summary>
        /// 이벤트 대기 객체
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// 태깅 결과 데이터
        /// </summary>
        public object TaggingResults { get; private set; }

        private bool _useLeader;
        private string _tagTypeId;

        /// <summary>
        /// 생성 파라미터 설정
        /// </summary>
        public void SetParameters(bool useLeader, string tagTypeId)
        {
            _useLeader = useLeader;
            _tagTypeId = tagTypeId;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                View activeView = doc.ActiveView;

                // Get all walls in the current view
                FilteredElementCollector wallCollector = new FilteredElementCollector(doc, activeView.Id);
                ICollection<Element> walls = wallCollector.OfCategory(BuiltInCategory.OST_Walls)
                                                         .WhereElementIsNotElementType()
                                                         .ToElements();

                // Create wall tags
                List<object> createdTags = new List<object>();
                List<string> errors = new List<string>();

                using (Transaction tran = new Transaction(doc, "벽체 태깅"))
                {
                    tran.Start();

                    // Find the wall tag type
                    FamilySymbol wallTagType = FindWallTagType(doc);

                    if (wallTagType == null)
                    {
                        TaggingResults = new
                        {
                            success = false,
                            message = "벽 태그 패밀리 타입을 찾을 수 없음"
                        };
                        tran.RollBack();
                        return;
                    }

                    // Ensure tag type is active
                    if (!wallTagType.IsActive)
                    {
                        wallTagType.Activate();
                        doc.Regenerate();
                    }

                    // Create tags for each wall
                    foreach (Element wall in walls)
                    {
#if REVIT2024_OR_GREATER
                        try
                        {
                            // Get the wall's location curve
                            LocationCurve locationCurve = wall.Location as LocationCurve;
                            if (locationCurve != null)
                            {
                                // Get the middle point of the wall
                                Curve curve = locationCurve.Curve;
                                XYZ midpoint = curve.Evaluate(0.5, true);

                                // Create tag at midpoint
                                IndependentTag tag = IndependentTag.Create(
                                    doc,
                                    wallTagType.Id,
                                    activeView.Id,
                                    new Reference(wall),
                                    _useLeader, // Use leader based on parameter
                                    TagOrientation.Horizontal,
                                    midpoint);

                                if (tag != null)
                                {
                                    createdTags.Add(new
                                    {
                                        id = tag.Id.Value.ToString(),
                                        wallId = wall.Id.Value.ToString(),

                                        wallName = wall.Name,
                                        location = new
                                        {
                                            x = midpoint.X,
                                            y = midpoint.Y,
                                            z = midpoint.Z
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"벽체 {wall.Id.Value} 태깅 오류: {ex.Message}");
                        }
#else
try
                        {
                            // Get the wall's location curve
                            LocationCurve locationCurve = wall.Location as LocationCurve;
                            if (locationCurve != null)
                            {
                                // Get the middle point of the wall
                                Curve curve = locationCurve.Curve;
                                XYZ midpoint = curve.Evaluate(0.5, true);

                                // Create tag at midpoint
                                IndependentTag tag = IndependentTag.Create(
                                    doc,
                                    wallTagType.Id,
                                    activeView.Id,
                                    new Reference(wall),
                                    _useLeader, // Use leader based on parameter
                                    TagOrientation.Horizontal,
                                    midpoint);

                                if (tag != null)
                                {
                                    createdTags.Add(new
                                    {
                                        id = tag.Id.IntegerValue.ToString(),
                                        wallId = wall.Id.IntegerValue.ToString(),

                                        wallName = wall.Name,
                                        location = new
                                        {
                                            x = midpoint.X,
                                            y = midpoint.Y,
                                            z = midpoint.Z
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"벽체 {wall.Id.IntegerValue} 태깅 오류: {ex.Message}");
                        }
#endif
                    }

                    tran.Commit();

                    TaggingResults = new
                    {
                        success = true,
                        totalWalls = walls.Count,
                        taggedWalls = createdTags.Count,
                        tags = createdTags,
                        errors = errors.Count > 0 ? errors : null
                    };
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("오류", $"벽체 태깅 중 오류 발생: {ex.Message}");
                TaggingResults = new
                {
                    success = false,
                    message = $"오류 발생: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set(); // 대기 스레드에게 작업 완료 알림
            }
        }

        /// <summary>
        /// 생성 완료 대기
        /// </summary>
        /// <param name="timeoutMilliseconds">타임아웃 시간 (밀리초)</param>
        /// <returns>작업이 타임아웃 이전에 완료되었는지 여부</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName 구현
        /// </summary>
        public string GetName()
        {
            return "벽 태깅";
        }

        /// <summary>
        /// Find the wall tag type in the document
        /// </summary>
        private FamilySymbol FindWallTagType(Document doc)
        {
#if REVIT2024_OR_GREATER
            // If specific tag type ID was specified, try to use it
            if (!string.IsNullOrEmpty(_tagTypeId))
            {
                if (int.TryParse(_tagTypeId, out int id))
                {
                    ElementId elementId = new ElementId(id);
                    Element element = doc.GetElement(elementId);

                    if (element != null && element is FamilySymbol symbol &&
                        (symbol.Category.Id.Value == (int)BuiltInCategory.OST_WallTags ||
                         symbol.Category.Id.Value == (int)BuiltInCategory.OST_MultiCategoryTags))
                    {
                        return symbol;
                    }
                }
            }

            // First try to find a tag specifically for walls
            FilteredElementCollector tagCollector = new FilteredElementCollector(doc);
            FamilySymbol wallTagType = tagCollector.OfClass(typeof(FamilySymbol))
                                                  .WhereElementIsElementType()
                                                  .Where(e => e.Category != null &&
                                                         e.Category.Id.Value == (int)BuiltInCategory.OST_WallTags)
                                                  .Cast<FamilySymbol>()
                                                  .FirstOrDefault();

            // If no wall tag found, try to find a multi-category tag that can tag walls
            if (wallTagType == null)
            {
                wallTagType = tagCollector.OfClass(typeof(FamilySymbol))
                                         .WhereElementIsElementType()
                                         .Where(e => e.Category != null &&
                                                e.Category.Id.Value == (int)BuiltInCategory.OST_MultiCategoryTags)
                                         .Cast<FamilySymbol>()
                                         .FirstOrDefault();
            }

            return wallTagType;
#else
            // If specific tag type ID was specified, try to use it
            if (!string.IsNullOrEmpty(_tagTypeId))
            {
                if (int.TryParse(_tagTypeId, out int id))
                {
                    ElementId elementId = new ElementId(id);
                    Element element = doc.GetElement(elementId);

                    if (element != null && element is FamilySymbol symbol &&
                        (symbol.Category.Id.IntegerValue == (int)BuiltInCategory.OST_WallTags ||
                         symbol.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MultiCategoryTags))
                    {
                        return symbol;
                    }
                }
            }

            // First try to find a tag specifically for walls
            FilteredElementCollector tagCollector = new FilteredElementCollector(doc);
            FamilySymbol wallTagType = tagCollector.OfClass(typeof(FamilySymbol))
                                                  .WhereElementIsElementType()
                                                  .Where(e => e.Category != null &&
                                                         e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_WallTags)
                                                  .Cast<FamilySymbol>()
                                                  .FirstOrDefault();

            // If no wall tag found, try to find a multi-category tag that can tag walls
            if (wallTagType == null)
            {
                wallTagType = tagCollector.OfClass(typeof(FamilySymbol))
                                         .WhereElementIsElementType()
                                         .Where(e => e.Category != null &&
                                                e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MultiCategoryTags)
                                         .Cast<FamilySymbol>()
                                         .FirstOrDefault();
            }

            return wallTagType;
#endif

        }
    }
}