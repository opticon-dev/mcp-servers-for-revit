using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Services
{
    public class OperateElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
        public OperationSetting OperationData { get; private set; }
        /// <summary>
        /// 실행 결과 (출력 데이터)
        /// </summary>
        public AIResult<string> Result { get; private set; }

        /// <summary>
        /// 생성 파라미터 설정
        /// </summary>
        public void SetParameters(OperationSetting data)
        {
            OperationData = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                bool result = ExecuteElementOperation(uiDoc, OperationData);

                Result = new AIResult<string>
                {
                    Success = true,
                    Message = $"작업이 성공적으로 실행됨",
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<string>
                {
                    Success = false,
                    Message = $"엘리먼트 조작 중 오류: {ex.Message}",
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
            return "엘리먼트 조작";
        }

        /// <summary>
        /// 작업 설정에 따라 해당 엘리먼트 작업을 수행
        /// </summary>
        /// <param name="uidoc">현재 UI 문서</param>
        /// <param name="setting">작업 설정</param>
        /// <returns>작업 성공 여부</returns>
        public static bool ExecuteElementOperation(UIDocument uidoc, OperationSetting setting)
        {
            // 파라미터 유효성 확인
            if (uidoc == null || uidoc.Document == null || setting == null || setting.ElementIds == null ||
                (setting.ElementIds.Count == 0 && setting.Action.ToLower() != "resetisolate"))
                throw new Exception("파라미터가 유효하지 않음: 문서가 비어 있거나 조작할 엘리먼트가 지정되지 않음");

            Document doc = uidoc.Document;

            // int 타입의 엘리먼트 ID를 ElementId 타입으로 변환
            ICollection<ElementId> elementIds = setting.ElementIds.Select(id => new ElementId(id)).ToList();

            // 작업 타입 파싱
            ElementOperationType action;
            if (!Enum.TryParse(setting.Action, true, out action))
            {
                throw new Exception($"지원되지 않는 작업 타입: {setting.Action}");
            }

            // 작업 타입에 따라 다른 작업 수행
            switch (action)
            {
                case ElementOperationType.Select:
                    // 엘리먼트 선택
                    uidoc.Selection.SetElementIds(elementIds);
                    return true;

                case ElementOperationType.SelectionBox:
                    // 3D 뷰에서 섹션 박스 생성

                    // 현재 뷰가 3D 뷰인지 확인
                    View3D targetView;

                    if (doc.ActiveView is View3D)
                    {
                        // 현재 뷰가 3D 뷰이면, 현재 뷰에서 섹션 박스 생성
                        targetView = doc.ActiveView as View3D;
                    }
                    else
                    {
                        // 현재 뷰가 3D 뷰가 아니면, 기본 3D 뷰를 찾음
                        FilteredElementCollector collector = new FilteredElementCollector(doc);
                        collector.OfClass(typeof(View3D));

                        // 기본 3D 뷰 또는 사용 가능한 다른 3D 뷰를 찾음
                        targetView = collector
                            .Cast<View3D>()
                            .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked && (v.Name.Contains("{3D}") || v.Name.Contains("Default 3D")));

                        if (targetView == null)
                        {
                            // 적합한 3D 뷰를 찾지 못한 경우, 예외 발생
                            throw new Exception("섹션 박스를 생성할 적합한 3D 뷰를 찾을 수 없음");
                        }

                        // 해당 3D 뷰 활성화
                        uidoc.ActiveView = targetView;
                    }

                    // 선택된 엘리먼트의 경계 상자 계산
                    BoundingBoxXYZ boundingBox = null;

                    foreach (ElementId id in elementIds)
                    {
                        Element elem = doc.GetElement(id);
                        BoundingBoxXYZ elemBox = elem.get_BoundingBox(null);

                        if (elemBox != null)
                        {
                            if (boundingBox == null)
                            {
                                boundingBox = new BoundingBoxXYZ
                                {
                                    Min = new XYZ(elemBox.Min.X, elemBox.Min.Y, elemBox.Min.Z),
                                    Max = new XYZ(elemBox.Max.X, elemBox.Max.Y, elemBox.Max.Z)
                                };
                            }
                            else
                            {
                                // 현재 엘리먼트를 포함하도록 경계 상자 확장
                                boundingBox.Min = new XYZ(
                                    Math.Min(boundingBox.Min.X, elemBox.Min.X),
                                    Math.Min(boundingBox.Min.Y, elemBox.Min.Y),
                                    Math.Min(boundingBox.Min.Z, elemBox.Min.Z));

                                boundingBox.Max = new XYZ(
                                    Math.Max(boundingBox.Max.X, elemBox.Max.X),
                                    Math.Max(boundingBox.Max.Y, elemBox.Max.Y),
                                    Math.Max(boundingBox.Max.Z, elemBox.Max.Z));
                            }
                        }
                    }

                    if (boundingBox == null)
                    {
                        throw new Exception("선택된 엘리먼트에 대한 경계 상자를 생성할 수 없음");
                    }

                    // 경계 상자 크기를 키워 엘리먼트보다 약간 크게 만듦
                    double offset = 1.0; // 1피트 오프셋
                    boundingBox.Min = new XYZ(boundingBox.Min.X - offset, boundingBox.Min.Y - offset, boundingBox.Min.Z - offset);
                    boundingBox.Max = new XYZ(boundingBox.Max.X + offset, boundingBox.Max.Y + offset, boundingBox.Max.Z + offset);

                    // 3D 뷰에서 섹션 박스 활성화 및 설정
                    using (Transaction trans = new Transaction(doc, "섹션 박스 생성"))
                    {
                        trans.Start();
                        targetView.IsSectionBoxActive = true;
                        targetView.SetSectionBox(boundingBox);
                        trans.Commit();
                    }

                    // 뷰 중심으로 이동
                    uidoc.ShowElements(elementIds);
                    return true;

                case ElementOperationType.SetColor:
                    // 엘리먼트를 지정된 색상으로 설정
                    using (Transaction trans = new Transaction(doc, "엘리먼트 색상 설정"))
                    {
                        trans.Start();
                        SetElementsColor(doc, elementIds, setting.ColorValue);
                        trans.Commit();
                    }
                    // 이 엘리먼트들이 보이도록 스크롤
                    uidoc.ShowElements(elementIds);
                    return true;


                case ElementOperationType.SetTransparency:
                    // 현재 뷰에서 엘리먼트의 투명도 설정
                    using (Transaction trans = new Transaction(doc, "엘리먼트 투명도 설정"))
                    {
                        trans.Start();

                        // 그래픽 오버라이드 설정 객체 생성
                        OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();

                        // 투명도 설정 (값이 0-100 범위 내에 있음을 보장)
                        int transparencyValue = Math.Max(0, Math.Min(100, setting.TransparencyValue));

                        // 표면 투명도 설정
                        overrideSettings.SetSurfaceTransparency(transparencyValue);

                        // 각 엘리먼트에 투명도 설정 적용
                        foreach (ElementId id in elementIds)
                        {
                            doc.ActiveView.SetElementOverrides(id, overrideSettings);
                        }

                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Delete:
                    // 엘리먼트 삭제 (트랜잭션 필요)
                    using (Transaction trans = new Transaction(doc, "엘리먼트 삭제"))
                    {
                        trans.Start();
                        doc.Delete(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Hide:
                    // 엘리먼트 숨기기 (활성 뷰와 트랜잭션 필요)
                    using (Transaction trans = new Transaction(doc, "엘리먼트 숨기기"))
                    {
                        trans.Start();
                        doc.ActiveView.HideElements(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.TempHide:
                    // 엘리먼트 임시 숨기기 (활성 뷰와 트랜잭션 필요)
                    using (Transaction trans = new Transaction(doc, "엘리먼트 임시 숨기기"))
                    {
                        trans.Start();
                        doc.ActiveView.HideElementsTemporary(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Isolate:
                    // 엘리먼트 분리 (활성 뷰와 트랜잭션 필요)
                    using (Transaction trans = new Transaction(doc, "엘리먼트 분리"))
                    {
                        trans.Start();
                        doc.ActiveView.IsolateElementsTemporary(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.Unhide:
                    // 엘리먼트 숨기기 해제 (활성 뷰와 트랜잭션 필요)
                    using (Transaction trans = new Transaction(doc, "엘리먼트 숨기기 해제"))
                    {
                        trans.Start();
                        doc.ActiveView.UnhideElements(elementIds);
                        trans.Commit();
                    }
                    return true;

                case ElementOperationType.ResetIsolate:
                    // 분리 재설정 (활성 뷰와 트랜잭션 필요)
                    using (Transaction trans = new Transaction(doc, "분리 재설정"))
                    {
                        trans.Start();
                        doc.ActiveView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        trans.Commit();
                    }
                    return true;

                default:
                    throw new Exception($"지원되지 않는 작업 타입: {setting.Action}");
            }
        }

        /// <summary>
        /// 뷰에서 지정된 엘리먼트를 지정된 색상으로 설정
        /// </summary>
        /// <param name="doc">문서</param>
        /// <param name="elementIds">색상을 설정할 엘리먼트 ID 컬렉션</param>
        /// <param name="elementColor">색상 값 (RGB 형식)</param>
        private static void SetElementsColor(Document doc, ICollection<ElementId> elementIds, int[] elementColor)
        {
            // 색상 배열이 유효한지 확인
            if (elementColor == null || elementColor.Length < 3)
            {
                elementColor = new int[] { 255, 0, 0 }; // 기본 빨간색
            }
            // RGB 값이 0-255 범위 내에 있음을 보장
            int r = Math.Max(0, Math.Min(255, elementColor[0]));
            int g = Math.Max(0, Math.Min(255, elementColor[1]));
            int b = Math.Max(0, Math.Min(255, elementColor[2]));
            // Revit 색상 객체 생성 - byte 타입 변환 사용
            Color color = new Color((byte)r, (byte)g, (byte)b);
            // 그래픽 오버라이드 설정 생성
            OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
            // 지정된 색상 설정
            overrideSettings.SetProjectionLineColor(color);
            overrideSettings.SetCutLineColor(color);
            overrideSettings.SetSurfaceForegroundPatternColor(color);
            overrideSettings.SetSurfaceBackgroundPatternColor(color);

            // 채우기 패턴 설정 시도
            try
            {
                // 기본 채우기 패턴 가져오기 시도
                FilteredElementCollector patternCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement));

                // 먼저 솔리드 채우기 패턴을 찾음
                FillPatternElement solidPattern = patternCollector
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                if (solidPattern != null)
                {
                    overrideSettings.SetSurfaceForegroundPatternId(solidPattern.Id);
                    overrideSettings.SetSurfaceForegroundPatternVisible(true);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"채우기 패턴 설정 실패: {ex.Message}");
            }

            // 각 엘리먼트에 오버라이드 설정 적용
            foreach (ElementId id in elementIds)
            {
                doc.ActiveView.SetElementOverrides(id, overrideSettings);
            }
        }

    }
}
