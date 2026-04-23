using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RevitMCPCommandSet.Services
{
    public class AIElementFilterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
        /// 생성 데이터 (입력 데이터)
        /// </summary>
        public FilterSetting FilterSetting { get; private set; }
        /// <summary>
        /// 실행 결과 (출력 데이터)
        /// </summary>
        public AIResult<List<object>> Result { get; private set; }

        /// <summary>
        /// 생성 파라미터 설정
        /// </summary>
        public void SetParameters(FilterSetting data)
        {
            FilterSetting = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementInfoList = new List<object>();
                // 필터 설정이 유효한지 확인
                if (!FilterSetting.Validate(out string errorMessage))
                    throw new Exception(errorMessage);
                // 지정된 조건에 맞는 엘리먼트의 ID 가져오기
                var elementList = GetFilteredElements(doc, FilterSetting);
                if (elementList == null || !elementList.Any())
                    // 프로젝트에서 지정된 엘리먼트를 찾을 수 없습니다. 필터 설정이 올바른지 확인하세요.
                    throw new Exception("프로젝트에서 지정된 엘리먼트를 찾을 수 없음, 필터 설정이 올바른지 확인하세요");
                // 필터 최대 개수 제한
                string message = "";
                if (FilterSetting.MaxElements > 0)
                {
                    if (elementList.Count > FilterSetting.MaxElements)
                    {
                        elementList = elementList.Take(FilterSetting.MaxElements).ToList();
                        // 또한 필터 조건을 만족하는 엘리먼트는 총 {elementList.Count}개이며, 앞의 {FilterSetting.MaxElements}개만 표시합니다.
                        message = $". 또한, 필터 조건에 부합하는 엘리먼트는 총 {elementList.Count} 개이며, 앞 {FilterSetting.MaxElements} 개만 표시됨";
                    }
                }

                // 지정된 ID의 엘리먼트 정보 가져오기
                elementInfoList = GetElementFullInfo(doc, elementList);

                Result = new AIResult<List<object>>
                {
                    Success = true,
                    // {elementInfoList.Count}개의 엘리먼트 정보를 성공적으로 가져왔으며, 상세 정보는 Response 속성에 저장됩니다.
                    Message = $"{elementInfoList.Count} 개의 엘리먼트 정보를 성공적으로 가져왔으며, 상세 정보는 Response 속성에 저장됨" + message,
                    Response = elementInfoList,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<object>>
                {
                    Success = false,
                    // 엘리먼트 정보 가져오기 중 오류: {ex.Message}
                    Message = $"엘리먼트 정보 가져오기 중 오류: {ex.Message}",
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
            // 엘리먼트 정보 가져오기
            return "엘리먼트 정보 가져오기";
        }

        /// <summary>
        /// 필터 설정에 따라 Revit 문서에서 조건에 맞는 엘리먼트를 가져오며, 다중 조건 조합 필터링을 지원
        /// </summary>
        /// <param name="doc">RevitRevit 문서</param>
        /// <param name="settings">필터 설정</param>
        /// <returns>모든 필터 조건을 만족하는 엘리먼트 집합</returns>
        public static IList<Element> GetFilteredElements(Document doc, FilterSetting settings)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            // 필터 설정 검증
            if (!settings.Validate(out string errorMessage))
            {
                // 필터 설정이 유효하지 않음: {errorMessage}
                System.Diagnostics.Trace.WriteLine($"필터 설정이 유효하지 않음: {errorMessage}");
                return new List<Element>();
            }
            // 적용된 필터 조건 기록
            List<string> appliedFilters = new List<string>();
            List<Element> result = new List<Element>();
            // 타입과 인스턴스를 모두 포함하면 각각 필터링한 뒤 결과를 병합해야 함
            if (settings.IncludeTypes && settings.IncludeInstances)
            {
                // 타입 엘리먼트 수집
                result.AddRange(GetElementsByKind(doc, settings, true, appliedFilters));

                // 인스턴스 엘리먼트 수집
                result.AddRange(GetElementsByKind(doc, settings, false, appliedFilters));
            }
            else if (settings.IncludeInstances)
            {
                // 인스턴스 엘리먼트만 수집
                result = GetElementsByKind(doc, settings, false, appliedFilters);
            }
            else if (settings.IncludeTypes)
            {
                // 타입 엘리먼트만 수집
                result = GetElementsByKind(doc, settings, true, appliedFilters);
            }

            // 적용된 필터 정보 출력
            if (appliedFilters.Count > 0)
            {
                // 적용된 필터 조건 {appliedFilters.Count}개: {string.Join(", ", appliedFilters)}
                System.Diagnostics.Trace.WriteLine($"적용된 필터 조건 {appliedFilters.Count}개: {string.Join(", ", appliedFilters)}");
                // 최종 필터링 결과: 총 {result.Count}개의 엘리먼트 발견
                System.Diagnostics.Trace.WriteLine($"최종 필터링 결과: 총 {result.Count}개의 엘리먼트 발견");
            }
            return result;

        }

        /// <summary>
        /// 엘리먼트 종류(타입 또는 인스턴스)에 따라 필터 조건을 만족하는 엘리먼트를 가져오기
        /// </summary>
        private static List<Element> GetElementsByKind(Document doc, FilterSetting settings, bool isElementType, List<string> appliedFilters)
        {
            // 기본 FilteredElementCollector 생성
            FilteredElementCollector collector;
            // 현재 뷰에서 보이는 엘리먼트를 필터링해야 하는지 확인 (인스턴스 엘리먼트에만 적용)
            if (!isElementType && settings.FilterVisibleInCurrentView && doc.ActiveView != null)
            {
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                // 현재 뷰에 보이는 엘리먼트
                appliedFilters.Add("현재 뷰에 보이는 엘리먼트");
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }
            // 엘리먼트 종류에 따라 필터링
            if (isElementType)
            {
                collector = collector.WhereElementIsElementType();
                // 엘리먼트 타입만
                appliedFilters.Add("엘리먼트 타입만");
            }
            else
            {
                collector = collector.WhereElementIsNotElementType();
                // 엘리먼트 인스턴스만
                appliedFilters.Add("엘리먼트 인스턴스만");
            }
            // 필터 목록 생성
            List<ElementFilter> filters = new List<ElementFilter>();
            // 1. 카테고리 필터
            if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
            {
                BuiltInCategory category;
                if (!Enum.TryParse(settings.FilterCategory, true, out category))
                {
                    // '{settings.FilterCategory}'을(를) 유효한 Revit 카테고리로 변환할 수 없습니다.
                    throw new ArgumentException($"'{settings.FilterCategory}'을(를) 유효한 Revit 카테고리로 변환할 수 없습니다.");
                }
                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(category);
                filters.Add(categoryFilter);
                // 카테고리: {settings.FilterCategory}
                appliedFilters.Add($"카테고리: {settings.FilterCategory}");
            }
            // 2. 엘리먼트 타입 필터
            if (!string.IsNullOrWhiteSpace(settings.FilterElementType))
            {

                Type elementType = null;
                // 타입 이름의 여러 가능한 형식을 해석 시도
                string[] possibleTypeNames = new string[]
                {
                    settings.FilterElementType,                                    // 원본 입력
                    $"Autodesk.Revit.DB.{settings.FilterElementType}, RevitAPI",  // Revit API 네임스페이스
                    $"{settings.FilterElementType}, RevitAPI"                      // 어셈블리를 포함한 완전 수식 이름
                };
                foreach (string typeName in possibleTypeNames)
                {
                    elementType = Type.GetType(typeName);
                    if (elementType != null)
                        break;
                }
                if (elementType != null)
                {
                    ElementClassFilter classFilter = new ElementClassFilter(elementType);
                    filters.Add(classFilter);
                    // 엘리먼트 타입: {elementType.Name}
                    appliedFilters.Add($"엘리먼트 타입: {elementType.Name}");
                }
                else
                {
                    // 경고: 타입 '{settings.FilterElementType}'을(를) 찾을 수 없음
                    throw new Exception($"경고: 타입 '{settings.FilterElementType}'을(를) 찾을 수 없음");
                }
            }
            // 3. 패밀리 심볼 필터 (엘리먼트 인스턴스에만 적용)
            if (!isElementType && settings.FilterFamilySymbolId > 0)
            {
                ElementId symbolId = new ElementId(settings.FilterFamilySymbolId);
                // 엘리먼트가 존재하고 패밀리 타입인지 확인
                Element symbolElement = doc.GetElement(symbolId);
                if (symbolElement != null && symbolElement is FamilySymbol)
                {
                    FamilyInstanceFilter familyFilter = new FamilyInstanceFilter(doc, symbolId);
                    filters.Add(familyFilter);
                    // 더 자세한 패밀리 정보 로그 추가
                    FamilySymbol symbol = symbolElement as FamilySymbol;
                    // 알 수 없는 패밀리
                    string familyName = symbol.Family?.Name ?? "알 수 없는 패밀리";
                    // 알 수 없는 타입
                    string symbolName = symbol.Name ?? "알 수 없는 타입";
                    // 패밀리 타입: {familyName} - {symbolName} (ID: {settings.FilterFamilySymbolId})
                    appliedFilters.Add($"패밀리 타입: {familyName} - {symbolName} (ID: {settings.FilterFamilySymbolId})");
                }
                else
                {
                    // 존재하지 않음
                    string elementType = symbolElement != null ? symbolElement.GetType().Name : "존재하지 않음";
                    // 경고: ID가 {settings.FilterFamilySymbolId}인 엘리먼트가 존재하지 않거나 유효한 FamilySymbol이 아닙니다. (실제 타입: {elementType})
                    System.Diagnostics.Trace.WriteLine($"경고: ID가 {settings.FilterFamilySymbolId}인 엘리먼트가 {(symbolElement == null ? "존재하지 않음" : "유효한 FamilySymbol이 아님")} (실제 타입: {elementType})");
                }
            }
            // 4. 공간 범위 필터
            if (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null)
            {
                // Revit XYZ 좌표로 변환 (밀리미터를 내부 단위로 변환)
                XYZ minXYZ = JZPoint.ToXYZ(settings.BoundingBoxMin);
                XYZ maxXYZ = JZPoint.ToXYZ(settings.BoundingBoxMax);
                // 공간 범위 Outline 객체 생성
                Outline outline = new Outline(minXYZ, maxXYZ);
                // 교차 필터 생성
                BoundingBoxIntersectsFilter boundingBoxFilter = new BoundingBoxIntersectsFilter(outline);
                filters.Add(boundingBoxFilter);
                // 공간 범위 필터: Min({settings.BoundingBoxMin.X:F2}, {settings.BoundingBoxMin.Y:F2}, {settings.BoundingBoxMin.Z:F2}), ...
                appliedFilters.Add($"공간 범위 필터: Min({settings.BoundingBoxMin.X:F2}, {settings.BoundingBoxMin.Y:F2}, {settings.BoundingBoxMin.Z:F2}), " +
                                  $"Max({settings.BoundingBoxMax.X:F2}, {settings.BoundingBoxMax.Y:F2}, {settings.BoundingBoxMax.Z:F2}) mm");
            }
            // 조합 필터 적용
            if (filters.Count > 0)
            {
                ElementFilter combinedFilter = filters.Count == 1
                    ? filters[0]
                    : new LogicalAndFilter(filters);
                collector = collector.WherePasses(combinedFilter);
                if (filters.Count > 1)
                {
                    // 필터 조건 {filters.Count}개를 결합한 필터를 적용함 (논리 AND 관계)
                    System.Diagnostics.Trace.WriteLine($"필터 조건 {filters.Count}개를 결합한 필터를 적용함 (논리 AND 관계)");
                }
            }
            return collector.ToElements().ToList();
        }

        /// <summary>
        /// 모델 엘리먼트 정보 가져오기
        /// </summary>
        public static List<object> GetElementFullInfo(Document doc, IList<Element> elementCollector)
        {
            List<object> infoList = new List<object>();

            // 엘리먼트를 가져와 처리
            foreach (var element in elementCollector)
            {
                // 실체 모델 엘리먼트인지 판단
                // 엘리먼트 인스턴스 정보 가져오기
                if (element?.Category?.HasMaterialQuantities ?? false)
                {
                    var info = CreateElementFullInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 엘리먼트 타입 정보 가져오기
                else if (element is ElementType elementType)
                {
                    var info = CreateTypeFullInfo(doc, elementType);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 3. 위치 기준 엘리먼트 (고빈도)
                else if (element is Level || element is Grid)
                {
                    var info = CreatePositioningElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 4. 공간 엘리먼트 (중고빈도)
                else if (element is SpatialElement) // Room, Area 등
                {
                    var info = CreateSpatialElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 5. 뷰 엘리먼트 (고빈도)
                else if (element is View)
                {
                    var info = CreateViewInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 6. 주석 엘리먼트 (중빈도)
                else if (element is TextNote || element is Dimension ||
                         element is IndependentTag || element is AnnotationSymbol ||
                         element is SpotDimension)
                {
                    var info = CreateAnnotationInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 7. 그룹 및 링크 처리
                else if (element is Group || element is RevitLinkInstance)
                {
                    var info = CreateGroupOrLinkInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 8. 엘리먼트 기본 정보 가져오기(기본 처리)
                else
                {
                    var info = CreateElementBasicInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
            }

            return infoList;
        }

        /// <summary>
        /// 단일 엘리먼트의 전체 ElementInfo 객체 생성
        /// </summary>
        public static ElementInstanceInfo CreateElementFullInfo(Document doc, Element element)
        {
            try
            {
                if (element?.Category == null)
                    return null;

                ElementInstanceInfo elementInfo = new ElementInstanceInfo();        // 엘리먼트 전체 정보를 저장하는 사용자 정의 클래스 생성
                // ID
                elementInfo.Id = element.Id.GetIntValue();
                // UniqueId
                elementInfo.UniqueId = element.UniqueId;
                // 타입 이름
                elementInfo.Name = element.Name;
                // 패밀리 이름
                elementInfo.FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString();
                // 카테고리
                elementInfo.Category = element.Category.Name;
                // 내장 카테고리
                elementInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue());
                // 타입 ID
                elementInfo.TypeId = element.GetTypeId().GetIntValue();
                // 소속 Room ID
                if (element is FamilyInstance instance)
                    elementInfo.RoomId = instance.Room?.Id.GetIntValue() ?? -1;
                // 레벨
                elementInfo.Level = GetElementLevel(doc, element);
                // 바운딩 박스
                BoundingBoxInfo boundingBoxInfo = new BoundingBoxInfo();
                elementInfo.BoundingBox = GetBoundingBoxInfo(element);
                // 파라미터
                //elementInfo.Parameters = GetDimensionParameters(element);
                ParameterInfo thicknessParam = GetThicknessInfo(element);      // 두께 파라미터
                if (thicknessParam != null)
                {
                    elementInfo.Parameters.Add(thicknessParam);
                }
                ParameterInfo heightParam = GetBoundingBoxHeight(elementInfo.BoundingBox);      // 높이 파라미터
                if (heightParam != null)
                {
                    elementInfo.Parameters.Add(heightParam);
                }

                return elementInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 단일 타입의 전체 TypeFullInfo 객체 생성
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public static ElementTypeInfo CreateTypeFullInfo(Document doc, ElementType elementType)
        {
            ElementTypeInfo typeInfo = new ElementTypeInfo();
            // Id
            typeInfo.Id = elementType.Id.GetIntValue();
            // UniqueId
            typeInfo.UniqueId = elementType.UniqueId;
            // 타입 이름
            typeInfo.Name = elementType.Name;
            // 패밀리 이름
            typeInfo.FamilyName = elementType.FamilyName;
            // 카테고리
            typeInfo.Category = elementType.Category.Name;
            // 내장 카테고리
            typeInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), elementType.Category.Id.GetIntValue());
            // 파라미터 사전
            typeInfo.Parameters = GetDimensionParameters(elementType);
            ParameterInfo thicknessParam = GetThicknessInfo(elementType);      // 두께 파라미터
            if (thicknessParam != null)
            {
                typeInfo.Parameters.Add(thicknessParam);
            }
            return typeInfo;
        }

        /// <summary>
        /// 위치 기준 엘리먼트 정보 생성
        /// </summary>
        public static PositioningElementInfo CreatePositioningElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                PositioningElementInfo info = new PositioningElementInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 레벨 처리
                if (element is Level level)
                {
                    // mm로 변환
                    info.Elevation = level.Elevation * 304.8;
                }
                // 축망 처리
                else if (element is Grid grid)
                {
                    Curve curve = grid.Curve;
                    if (curve != null)
                    {
                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        // JZLine 생성(mm로 변환)
                        info.GridLine = new JZLine(
                            start.X * 304.8, start.Y * 304.8, start.Z * 304.8,
                            end.X * 304.8, end.Y * 304.8, end.Z * 304.8);
                    }
                }

                // 레벨 정보 가져오기
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                // 위치 기준 엘리먼트 정보 생성 중 오류: {ex.Message}
                System.Diagnostics.Trace.WriteLine($"위치 기준 엘리먼트 정보 생성 중 오류: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// 공간 엘리먼트 정보 생성
        /// </summary>
        public static SpatialElementInfo CreateSpatialElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is SpatialElement))
                    return null;
                SpatialElement spatialElement = element as SpatialElement;
                SpatialElementInfo info = new SpatialElementInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // Room 또는 Area 번호 가져오기
                if (element is Room room)
                {
                    info.Number = room.Number;
                    // mm³로 변환
                    info.Volume = room.Volume * Math.Pow(304.8, 3);
                }
                else if (element is Area area)
                {
                    info.Number = area.Number;
                }

                // 면적 가져오기
                Parameter areaParam = element.get_Parameter(BuiltInParameter.ROOM_AREA);
                if (areaParam != null && areaParam.HasValue)
                {
                    // mm²로 변환
                    info.Area = areaParam.AsDouble() * Math.Pow(304.8, 2);
                }

                // 둘레 가져오기
                Parameter perimeterParam = element.get_Parameter(BuiltInParameter.ROOM_PERIMETER);
                if (perimeterParam != null && perimeterParam.HasValue)
                {
                    // mm로 변환
                    info.Perimeter = perimeterParam.AsDouble() * 304.8;
                }

                // 레벨 가져오기
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                // 공간 엘리먼트 정보 생성 중 오류: {ex.Message}
                System.Diagnostics.Trace.WriteLine($"공간 엘리먼트 정보 생성 중 오류: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// 뷰 엘리먼트 정보 생성
        /// </summary>
        public static ViewInfo CreateViewInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is View))
                    return null;
                View view = element as View;

                ViewInfo info = new ViewInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale,
                    IsTemplate = view.IsTemplate,
                    DetailLevel = view.DetailLevel.ToString(),
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 뷰와 연관된 레벨 가져오기
                if (view is ViewPlan viewPlan && viewPlan.GenLevel != null)
                {
                    Level level = viewPlan.GenLevel;
                    info.AssociatedLevel = new LevelInfo
                    {
                        Id = level.Id.GetIntValue(),
                        Name = level.Name,
                        Height = level.Elevation * 304.8 // mm로 변환
                    };
                }

                // 뷰가 열려 있고 활성 상태인지 판단
                UIDocument uidoc = new UIDocument(doc);

                // 열려 있는 모든 뷰 가져오기
                IList<UIView> openViews = uidoc.GetOpenUIViews();

                foreach (UIView uiView in openViews)
                {
                    // 뷰가 열려 있는지 확인
                    if (uiView.ViewId.GetValue() == view.Id.GetValue())
                    {
                        info.IsOpen = true;

                        // 뷰가 현재 활성 뷰인지 확인
                        if (uidoc.ActiveView.Id.GetValue() == view.Id.GetValue())
                        {
                            info.IsActive = true;
                        }
                        break;
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                // 뷰 엘리먼트 정보 생성 중 오류: {ex.Message}
                System.Diagnostics.Trace.WriteLine($"뷰 엘리먼트 정보 생성 중 오류: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// 주석 엘리먼트 정보 생성
        /// </summary>
        public static AnnotationInfo CreateAnnotationInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                AnnotationInfo info = new AnnotationInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 소속 뷰 가져오기
                Parameter viewParam = element.get_Parameter(BuiltInParameter.VIEW_NAME);
                if (viewParam != null && viewParam.HasValue)
                {
                    info.OwnerView = viewParam.AsString();
                }
                else if (element.OwnerViewId != ElementId.InvalidElementId)
                {
                    View ownerView = doc.GetElement(element.OwnerViewId) as View;
                    info.OwnerView = ownerView?.Name;
                }

                // 텍스트 주석 처리
                if (element is TextNote textNote)
                {
                    info.TextContent = textNote.Text;
                    XYZ position = textNote.Coord;
                    // mm로 변환
                    info.Position = new JZPoint(
                        position.X * 304.8,
                        position.Y * 304.8,
                        position.Z * 304.8);
                }
                // 치수 주석 처리
                else if (element is Dimension dimension)
                {
                    info.DimensionValue = dimension.Value.ToString();
                    XYZ origin = dimension.Origin;
                    // mm로 변환
                    info.Position = new JZPoint(
                        origin.X * 304.8,
                        origin.Y * 304.8,
                        origin.Z * 304.8);
                }
                // 기타 주석 엘리먼트 처리
                else if (element is AnnotationSymbol annotationSymbol)
                {
                    if (annotationSymbol.Location is LocationPoint locationPoint)
                    {
                        XYZ position = locationPoint.Point;
                        // mm로 변환
                        info.Position = new JZPoint(
                            position.X * 304.8,
                            position.Y * 304.8,
                            position.Z * 304.8);
                    }
                }
                return info;
            }
            catch (Exception ex)
            {
                // 주석 엘리먼트 정보 생성 중 오류: {ex.Message}
                System.Diagnostics.Trace.WriteLine($"주석 엘리먼트 정보 생성 중 오류: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// 그룹 또는 링크 정보 생성
        /// </summary>
        public static GroupOrLinkInfo CreateGroupOrLinkInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                GroupOrLinkInfo info = new GroupOrLinkInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 그룹 처리
                if (element is Group group)
                {
                    ICollection<ElementId> memberIds = group.GetMemberIds();
                    info.MemberCount = memberIds?.Count;
                    info.GroupType = group.GroupType?.Name;
                }
                // 링크 처리
                else if (element is RevitLinkInstance linkInstance)
                {
                    RevitLinkType linkType = doc.GetElement(linkInstance.GetTypeId()) as RevitLinkType;
                    if (linkType != null)
                    {
                        ExternalFileReference extFileRef = linkType.GetExternalFileReference();
                        // 절대 경로 가져오기
                        string absPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extFileRef.GetAbsolutePath());
                        info.LinkPath = absPath;

                        // GetLinkedFileStatus로 링크 상태 가져오기
                        LinkedFileStatus linkStatus = linkType.GetLinkedFileStatus();
                        info.LinkStatus = linkStatus.ToString();
                    }
                    else
                    {
                        info.LinkStatus = LinkedFileStatus.Invalid.ToString();
                    }

                    // 위치 가져오기
                    LocationPoint location = linkInstance.Location as LocationPoint;
                    if (location != null)
                    {
                        XYZ point = location.Point;
                        // mm로 변환
                        info.Position = new JZPoint(
                            point.X * 304.8,
                            point.Y * 304.8,
                            point.Z * 304.8);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                // 그룹 및 링크 정보 생성 중 오류: {ex.Message}
                System.Diagnostics.Trace.WriteLine($"그룹 및 링크 정보 생성 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 엘리먼트의 확장 기본 정보 생성
        /// </summary>
        public static ElementBasicInfo CreateElementBasicInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                ElementBasicInfo basicInfo = new ElementBasicInfo
                {
                    Id = element.Id.GetIntValue(),
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null ?
                        Enum.GetName(typeof(BuiltInCategory), element.Category.Id.GetIntValue()) : null,
                    BoundingBox = GetBoundingBoxInfo(element)
                };
                return basicInfo;
            }
            catch (Exception ex)
            {
                // 엘리먼트 기본 정보 생성 중 오류: {ex.Message}
                System.Diagnostics.Trace.WriteLine($"엘리먼트 기본 정보 생성 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 시스템 패밀리 구성요소의 두께 파라미터 정보 가져오기
        /// </summary>
        /// <param name="element">시스템 패밀리 구성요소(벽, 바닥, 문 등)</param>
        /// <returns>파라미터 정보 객체이며, 유효하지 않으면 null을 반환</returns>
        public static ParameterInfo GetThicknessInfo(Element element)
        {
            if (element == null)
            {
                return null;
            }

            // 구성요소 타입 가져오기
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            if (elementType == null)
            {
                return null;
            }

            // 구성요소 타입에 따라 해당 내장 두께 파라미터 가져오기
            Parameter thicknessParam = null;

            if (elementType is WallType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
            }
            else if (elementType is FloorType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            }
            else if (elementType is FamilySymbol familySymbol)
            {
                switch (familySymbol.Category?.Id.GetIntValue())
                {
                    case (int)BuiltInCategory.OST_Doors:
                    case (int)BuiltInCategory.OST_Windows:
                        thicknessParam = elementType.get_Parameter(BuiltInParameter.FAMILY_THICKNESS_PARAM);
                        break;
                }
            }
            else if (elementType is CeilingType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.CEILING_THICKNESS);
            }

            if (thicknessParam != null && thicknessParam.HasValue)
            {
                return new ParameterInfo
                {
                    // Name = "두께",
                    Name = "두께",
                    Value = $"{thicknessParam.AsDouble() * 304.8}"
                };
            }
            return null;
        }

        /// <summary>
        /// 엘리먼트의 소속 레벨 정보 가져오기
        /// </summary>
        public static LevelInfo GetElementLevel(Document doc, Element element)
        {
            try
            {
                Level level = null;

                // 서로 다른 타입의 엘리먼트에 대한 레벨 가져오기 처리
                if (element is Wall wall) // 벽
                {
                    level = doc.GetElement(wall.LevelId) as Level;
                }
                else if (element is Floor floor) // 바닥
                {
                    Parameter levelParam = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }
                else if (element is FamilyInstance familyInstance) // 패밀리 인스턴스(일반 모델 등 포함)
                {
                    // 패밀리 인스턴스의 레벨 파라미터 가져오기 시도
                    Parameter levelParam = familyInstance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                    // 위 방법으로 가져올 수 없으면 SCHEDULE_LEVEL_PARAM 사용 시도
                    if (level == null)
                    {
                        levelParam = familyInstance.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                        if (levelParam != null && levelParam.HasValue)
                        {
                            level = doc.GetElement(levelParam.AsElementId()) as Level;
                        }
                    }
                }
                else // 기타 엘리먼트
                {
                    // 공통 레벨 파라미터 가져오기 시도
                    Parameter levelParam = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }

                if (level != null)
                {
                    LevelInfo levelInfo = new LevelInfo
                    {
                        Id = level.Id.GetIntValue(),
                        Name = level.Name,
                        Height = level.Elevation * 304.8
                    };
                    return levelInfo;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 엘리먼트의 바운딩 박스 정보 가져오기
        /// </summary>
        public static BoundingBoxInfo GetBoundingBoxInfo(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null)
                    return null;
                return new BoundingBoxInfo
                {
                    Min = new JZPoint(
                        bbox.Min.X * 304.8,
                        bbox.Min.Y * 304.8,
                        bbox.Min.Z * 304.8),
                    Max = new JZPoint(
                        bbox.Max.X * 304.8,
                        bbox.Max.Y * 304.8,
                        bbox.Max.Z * 304.8)
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 바운딩 박스의 높이 파라미터 정보 가져오기
        /// </summary>
        /// <param name="boundingBoxInfo">바운딩 박스 정보</param>
        /// <returns>파라미터 정보 객체이며, 유효하지 않으면 null을 반환</returns>
        public static ParameterInfo GetBoundingBoxHeight(BoundingBoxInfo boundingBoxInfo)
        {
            try
            {
                // 파라미터 확인
                if (boundingBoxInfo?.Min == null || boundingBoxInfo?.Max == null)
                {
                    return null;
                }

                // Z축 방향의 차이가 곧 높이임
                double height = Math.Abs(boundingBoxInfo.Max.Z - boundingBoxInfo.Min.Z);

                return new ParameterInfo
                {
                    // Name = "높이",
                    Name = "높이",
                    Value = $"{height}"
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 엘리먼트의 모든 비어 있지 않은 파라미터 이름과 값 가져오기
        /// </summary>
        /// <param name="element">RevitRevit 엘리먼트</param>
        /// <returns>파라미터 정보 목록</returns>
        public static List<ParameterInfo> GetDimensionParameters(Element element)
        {
            // 엘리먼트가 null인지 확인
            if (element == null)
            {
                return new List<ParameterInfo>();
            }

            var parameters = new List<ParameterInfo>();

            // 엘리먼트의 모든 파라미터 가져오기
            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    // 유효하지 않은 파라미터 건너뛰기
                    if (!param.HasValue || param.IsReadOnly)
                    {
                        continue;
                    }

                    // 현재 파라미터가 치수 관련 파라미터이면
                    if (IsDimensionParameter(param))
                    {
                        // 파라미터 값의 문자열 표현 가져오기
                        string value = param.AsValueString();

                        // 값이 비어 있지 않으면 목록에 추가
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            parameters.Add(new ParameterInfo
                            {
                                Name = param.Definition.Name,
                                Value = value
                            });
                        }
                    }
                }
                catch
                {
                    // 특정 파라미터 값 가져오기 중 오류가 나면 다음 항목 계속 처리
                    continue;
                }
            }

            // 파라미터 이름순으로 정렬 후 반환
            return parameters.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// 파라미터가 기록 가능한 치수 파라미터인지 판단
        /// </summary>
        public static bool IsDimensionParameter(Parameter param)
        {

#if REVIT2023_OR_GREATER
            // Revit 2023에서는 Definition의 GetDataType() 메서드로 파라미터 타입을 가져옴
            ForgeTypeId paramTypeId = param.Definition.GetDataType();

            // 파라미터가 치수 관련 타입인지 판단
            bool isDimensionType = paramTypeId.Equals(SpecTypeId.Length) ||
                                   paramTypeId.Equals(SpecTypeId.Angle) ||
                                   paramTypeId.Equals(SpecTypeId.Area) ||
                                   paramTypeId.Equals(SpecTypeId.Volume);
            // 치수 타입 파라미터만 저장
            return isDimensionType;
#else
            // 파라미터가 치수 관련 타입인지 판단
            bool isDimensionType = param.Definition.ParameterType == ParameterType.Length ||
                                   param.Definition.ParameterType == ParameterType.Angle ||
                                   param.Definition.ParameterType == ParameterType.Area ||
                                   param.Definition.ParameterType == ParameterType.Volume;

            // 치수 타입 파라미터만 저장
            return isDimensionType;
#endif
        }

    }

    /// <summary>
    /// 엘리먼트 전체 정보를 저장하는 사용자 정의 클래스
    /// </summary>
    public class ElementInstanceInfo
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Id
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 타입 ID
        /// </summary>
        public int TypeId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 패밀리 이름
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 카테고리
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 내장 카테고리
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 소속 Room ID
        /// </summary>
        public int RoomId { get; set; }
        /// <summary>
        /// 소속 레벨
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// 위치 정보
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// 인스턴스 파라미터
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

    }

    /// <summary>
    /// 엘리먼트 타입 전체 정보를 저장하는 사용자 정의 클래스
    /// </summary>
    public class ElementTypeInfo
    {
        /// <summary>
        /// ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Id
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 패밀리 이름
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 카테고리 이름
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 내장 카테고리 ID
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 타입 파라미터
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new List<ParameterInfo>();

    }

    /// <summary>
    /// 공간 위치 기준 엘리먼트(레벨, 축망 등) 기본 정보 클래스
    /// </summary>
    public class PositioningElementInfo
    {
        /// <summary>
        /// 엘리먼트 ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 엘리먼트 고유 ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 패밀리 이름
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 카테고리 이름
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 내장 카테고리(선택 사항)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 엘리먼트의 .NET 클래스 이름
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// 고도 값 (레벨에 적용, 단위 mm)
        /// </summary>
        public double? Elevation { get; set; }
        /// <summary>
        /// 소속 레벨
        /// </summary>
        public LevelInfo Level { get; set; }
        /// <summary>
        /// 위치 정보
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// 축망 선(축망에 적용)
        /// </summary>
        public JZLine GridLine { get; set; }
    }
    /// <summary>
    /// 공간 엘리먼트(Room, Area 등) 기본 정보 클래스
    /// </summary>
    public class SpatialElementInfo
    {
        /// <summary>
        /// 엘리먼트 ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 엘리먼트 고유 ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 패밀리 이름
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 번호
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// 카테고리 이름
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 내장 카테고리(선택 사항)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 엘리먼트의 .NET 클래스 이름
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// 면적(단위 mm²)
        /// </summary>
        public double? Area { get; set; }
        /// <summary>
        /// 체적(단위 mm³)
        /// </summary>
        public double? Volume { get; set; }
        /// <summary>
        /// 둘레(단위 mm)
        /// </summary>
        public double? Perimeter { get; set; }
        /// <summary>
        /// 위치한 레벨
        /// </summary>
        public LevelInfo Level { get; set; }

        /// <summary>
        /// 위치 정보
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
    /// <summary>
    /// 뷰 엘리먼트 기본 정보 클래스
    /// </summary>
    public class ViewInfo
    {
        /// <summary>
        /// 엘리먼트 ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 엘리먼트 고유 ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 패밀리 이름
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 카테고리 이름
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 내장 카테고리(선택 사항)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 엘리먼트의 .NET 클래스 이름
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// 뷰 타입
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// 뷰 축척
        /// </summary>
        public int? Scale { get; set; }

        /// <summary>
        /// 템플릿 뷰 여부
        /// </summary>
        public bool IsTemplate { get; set; }

        /// <summary>
        /// 상세 수준
        /// </summary>
        public string DetailLevel { get; set; }

        /// <summary>
        /// 연관된 레벨
        /// </summary>
        public LevelInfo AssociatedLevel { get; set; }

        /// <summary>
        /// 위치 정보
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// 뷰가 열려 있는지 여부
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// 현재 활성 뷰인지 여부
        /// </summary>
        public bool IsActive { get; set; }
    }
    /// <summary>
    /// 주석 엘리먼트 기본 정보 클래스
    /// </summary>
    public class AnnotationInfo
    {
        /// <summary>
        /// 엘리먼트 ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 엘리먼트 고유 ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 패밀리 이름
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 카테고리 이름
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 내장 카테고리(선택 사항)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 엘리먼트의 .NET 클래스 이름
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// 소속 뷰
        /// </summary>
        public string OwnerView { get; set; }
        /// <summary>
        /// 텍스트 내용 (텍스트 주석에 적용)
        /// </summary>
        public string TextContent { get; set; }
        /// <summary>
        /// 위치 정보(단위 mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// 위치 정보
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
        /// <summary>
        /// 치수 값 (치수 주석에 적용)
        /// </summary>
        public string DimensionValue { get; set; }
    }
    /// <summary>
    /// 그룹 및 링크 기본 정보 클래스
    /// </summary>
    public class GroupOrLinkInfo
    {
        /// <summary>
        /// 엘리먼트 ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 엘리먼트 고유 ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 패밀리 이름
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 카테고리 이름
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 내장 카테고리(선택 사항)
        /// </summary>
        public string BuiltInCategory { get; set; }
        /// <summary>
        /// 엘리먼트의 .NET 클래스 이름
        /// </summary>
        public string ElementClass { get; set; }
        /// <summary>
        /// 구성원 수
        /// </summary>
        public int? MemberCount { get; set; }
        /// <summary>
        /// 그룹 타입
        /// </summary>
        public string GroupType { get; set; }
        /// <summary>
        /// 링크 상태
        /// </summary>
        public string LinkStatus { get; set; }
        /// <summary>
        /// 링크 경로
        /// </summary>
        public string LinkPath { get; set; }
        /// <summary>
        /// 위치 정보(단위 mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// 위치 정보
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }
    /// <summary>
    /// 엘리먼트 기본 정보 확장 클래스
    /// </summary>
    public class ElementBasicInfo
    {
        /// <summary>
        /// 엘리먼트 ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 엘리먼트 고유 ID
        /// </summary>
        public string UniqueId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 패밀리 이름
        /// </summary>
        public string FamilyName { get; set; }
        /// <summary>
        /// 카테고리 이름
        /// </summary>
        public string Category { get; set; }
        /// <summary>
        /// 내장 카테고리(선택 사항)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 위치 정보
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }



    /// <summary>
    /// 파라미터 전체 정보를 저장하는 사용자 정의 클래스
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// 바운딩 박스 정보를 저장하는 사용자 정의 클래스
    /// </summary>
    public class BoundingBoxInfo
    {
        public JZPoint Min { get; set; }
        public JZPoint Max { get; set; }
    }

    /// <summary>
    /// 레벨 정보를 저장하는 사용자 정의 클래스
    /// </summary>
    public class LevelInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Height { get; set; }
    }



}
