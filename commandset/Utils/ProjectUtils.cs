using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Commands;
using RevitMCPCommandSet.Models.Common;
using System.IO;
using System.Reflection;

namespace RevitMCPCommandSet.Utils
{
    public static class ProjectUtils
    {
        /// <summary>
        /// 패밀리 인스턴스를 생성하는 공통 메서드
        /// </summary>
        /// <param name="doc">현재 문서</param>
        /// <param name="familySymbol">패밀리 타입</param>
        /// <param name="locationPoint">위치 점</param>
        /// <param name="locationLine">기준선</param>
        /// <param name="baseLevel">하단 레벨</param>
        /// <param name="topLevel">두 번째 레벨(TwoLevelsBased용)</param>
        /// <param name="baseOffset">하단 오프셋(ft)</param>
        /// <param name="topOffset">상단 오프셋(ft)</param>
        /// <param name="faceDirection">기준 방향</param>
        /// <param name="handDirection">기준 방향</param>
        /// <param name="view">뷰</param>
        /// <returns>생성된 패밀리 인스턴스, 실패 시 null 반환</returns>
        public static FamilyInstance CreateInstance(
            this Document doc,
            FamilySymbol familySymbol,
            XYZ locationPoint = null,
            Line locationLine = null,
            Level baseLevel = null,
            Level topLevel = null,
            double baseOffset = -1,
            double topOffset = -1,
            XYZ faceDirection = null,
            XYZ handDirection = null,
            View view = null,
            Element explicitHost = null,
            bool snapToHostCenter = true)
        {
            // 기본 파라미터 검사
            if (doc == null)
                throw new ArgumentNullException($"필수 파라미터 {typeof(Document)} {nameof(doc)} 이(가) 누락되었습니다!");
            if (familySymbol == null)
                throw new ArgumentNullException($"필수 파라미터 {typeof(FamilySymbol)} {nameof(familySymbol)} 이(가) 누락되었습니다!");

            // 패밀리 모델 활성화
            if (!familySymbol.IsActive)
                familySymbol.Activate();

            FamilyInstance instance = null;

            // 패밀리 배치 유형에 따라 생성 방법 선택
            switch (familySymbol.Family.FamilyPlacementType)
            {
                // 단일 레벨 기반 패밀리(예: 미터법 일반 모델)
                case FamilyPlacementType.OneLevelBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(XYZ)} {nameof(locationPoint)} 이(가) 누락되었습니다!");
                    // 레벨 정보 포함
                    if (baseLevel != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationPoint,                  // 인스턴스가 배치될 물리적 위치
                            familySymbol,                   // 삽입할 인스턴스 타입을 나타내는 FamilySymbol 객체
                            baseLevel,                      // 객체의 기준 레벨로 사용할 Level 객체
                            StructuralType.NonStructural);  // 구조 부재인 경우 부재의 타입을 지정
                    }
                    // 레벨 정보 미포함
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationPoint,                  // 인스턴스가 배치될 물리적 위치
                            familySymbol,                   // 삽입할 인스턴스 타입을 나타내는 FamilySymbol 객체
                            StructuralType.NonStructural);  // 구조 부재인 경우 부재의 타입을 지정
                    }
                    break;

                // 단일 레벨과 호스트 기반 패밀리(예: 문, 창)
                case FamilyPlacementType.OneLevelBasedHosted:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(XYZ)} {nameof(locationPoint)} 이(가) 누락되었습니다!");

                    Element host = explicitHost;
                    XYZ placementPoint = locationPoint;

                    // If explicit host provided and it's a wall, snap to its centerline
                    if (host != null && snapToHostCenter && host is Wall explicitWall)
                    {
                        LocationCurve eLoc = explicitWall.Location as LocationCurve;
                        if (eLoc != null)
                        {
                            IntersectionResult eIr = eLoc.Curve.Project(locationPoint);
                            if (eIr != null)
                                placementPoint = new XYZ(eIr.XYZPoint.X, eIr.XYZPoint.Y, locationPoint.Z);
                        }
                    }

                    // Auto-detect host wall if not explicitly provided
                    if (host == null)
                    {
                        // Try geometric wall-centerline proximity first
                        var wallResult = doc.GetNearestWallByLocationLine(locationPoint, baseLevel);
                        if (wallResult.HasValue)
                        {
                            host = wallResult.Value.wall;
                            if (snapToHostCenter)
                                placementPoint = wallResult.Value.projectedPoint;
                        }
                        else
                        {
                            // Fall back to original ray-casting method
                            host = doc.GetNearestHostElement(locationPoint, familySymbol);
                        }
                    }

                    if (host == null)
                        throw new ArgumentNullException($"적합한 호스트 정보를 찾을 수 없습니다!");

                    if (baseLevel != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            placementPoint,
                            familySymbol,
                            host,
                            baseLevel,
                            StructuralType.NonStructural);
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            placementPoint,
                            familySymbol,
                            host,
                            StructuralType.NonStructural);
                    }

                    // Set sill height for windows (baseOffset maps to sill height for hosted elements)
                    if (instance != null && baseOffset != -1)
                    {
                        Parameter sillParam = instance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                        if (sillParam != null && !sillParam.IsReadOnly)
                        {
                            sillParam.Set(baseOffset);
                        }
                    }
                    break;

                // 두 개의 레벨 기반 패밀리(예: 기둥)
                case FamilyPlacementType.TwoLevelsBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(XYZ)} {nameof(locationPoint)} 이(가) 누락되었습니다!");
                    if (baseLevel == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(Level)} {nameof(baseLevel)} 이(가) 누락되었습니다!");
                    // 구조 기둥인지 건축 기둥인지 판별
                    StructuralType structuralType = StructuralType.NonStructural;
                    if (familySymbol.Category.Id.GetIntValue() == (int)BuiltInCategory.OST_StructuralColumns)
                        structuralType = StructuralType.Column;
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,              // 인스턴스가 배치될 물리적 위치
                        familySymbol,               // 삽입할 인스턴스 타입을 나타내는 FamilySymbol 객체
                        baseLevel,                  // 객체의 기준 레벨로 사용할 Level 객체
                        structuralType);            // 하단 레벨, 상단 레벨, 하단 오프셋, 상단 오프셋 설정
                    if (instance != null)
                    {
                        // 기둥의 기준 레벨과 상단 레벨 설정
                        if (baseLevel != null)
                        {
                            Parameter baseLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                            if (baseLevelParam != null)
                                baseLevelParam.Set(baseLevel.Id);
                        }
                        if (topLevel != null)
                        {
                            Parameter topLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (topLevelParam != null)
                                topLevelParam.Set(topLevel.Id);
                        }
                        // 하단 오프셋 파라미터 가져오기
                        if (baseOffset != -1)
                        {
                            Parameter baseOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                            if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                            {
                                // 밀리미터를 Revit 내부 단위로 변환
                                double baseOffsetInternal = baseOffset;
                                baseOffsetParam.Set(baseOffsetInternal);
                            }
                        }
                        // 상단 오프셋 파라미터 가져오기
                        if (topOffset != -1)
                        {
                            Parameter topOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            if (topOffsetParam != null && topOffsetParam.StorageType == StorageType.Double)
                            {
                                // 밀리미터를 Revit 내부 단위로 변환
                                double topOffsetInternal = topOffset;
                                topOffsetParam.Set(topOffsetInternal);
                            }
                        }
                    }
                    break;

                // 뷰 전용 패밀리(예: 상세 주석)
                case FamilyPlacementType.ViewBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(XYZ)} {nameof(locationPoint)} 이(가) 누락되었습니다!");
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,  // 패밀리 인스턴스의 원점입니다. 평면 뷰(ViewPlan)에 생성하면 이 원점이 평면 뷰에 투영됩니다
                        familySymbol,   // 삽입할 인스턴스 타입을 나타내는 패밀리 심볼 객체
                        view);          // 패밀리 인스턴스를 배치할 2D 뷰
                    break;

                // 작업 평면 기반 패밀리(예: 면 기반/벽 기반 일반 모델)
                case FamilyPlacementType.WorkPlaneBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(XYZ)} {nameof(locationPoint)} 이(가) 누락되었습니다!");
                    // 가장 가까운 호스트 면 가져오기
                    Reference hostFace = doc.GetNearestFaceReference(locationPoint, 1000 / 304.8);
                    if (hostFace == null)
                        throw new ArgumentNullException($"적합한 호스트 정보를 찾을 수 없습니다!");
                    if (faceDirection == null || faceDirection == XYZ.Zero)
                    {
                        var result = doc.GenerateDefaultOrientation(hostFace);
                        faceDirection = result.FacingOrientation;
                    }
                    // 점과 방향을 사용해 면 위에 패밀리 인스턴스 생성
                    instance = doc.Create.NewFamilyInstance(
                        hostFace,               // 면에 대한 레퍼런스  
                        locationPoint,          // 인스턴스가 배치될 면 위의 점
                        faceDirection,          // 패밀리 인스턴스 방향을 정의하는 벡터입니다. 이 방향은 면 위에서의 회전을 정의하므로 면 법선과 평행할 수 없습니다
                        familySymbol);          // 삽입할 인스턴스 타입을 나타내는 FamilySymbol 객체입니다. 이 FamilySymbol은 FamilyPlacementType이 WorkPlaneBased인 패밀리여야 합니다
                    break;

                // 작업 평면 위의 선 기반 패밀리(예: 선 기반 일반 모델)
                case FamilyPlacementType.CurveBased:
                    if (locationLine == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(Line)} {nameof(locationLine)} 이(가) 누락되었습니다!");

                    // 가장 가까운 호스트 면 가져오기(오차 허용 안 함)
                    Reference lineHostFace = doc.GetNearestFaceReference(locationLine.Evaluate(0.5, true), 1e-5);
                    if (lineHostFace != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            lineHostFace,   // 면에 대한 레퍼런스 
                            locationLine,   // 패밀리 인스턴스가 기준으로 삼는 곡선
                            familySymbol);  // 삽입할 인스턴스의 타입을 나타내는 FamilySymbol 객체입니다. 이 Symbol은 FamilyPlacementType이 WorkPlaneBased 또는 CurveBased인 패밀리여야 합니다
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationLine,                   // 패밀리 인스턴스가 기준으로 삼는 곡선
                            familySymbol,                   // 삽입할 인스턴스의 타입을 나타내는 FamilySymbol 객체입니다. 이 Symbol은 FamilyPlacementType이 WorkPlaneBased 또는 CurveBased인 패밀리여야 합니다
                            baseLevel,                      // 객체의 기준 레벨로 사용할 Level 객체
                            StructuralType.NonStructural);  // 구조 부재인 경우 부재의 타입을 지정
                    }
                    if (instance != null)
                    {
                        // 하단 오프셋 파라미터 가져오기
                        if (baseOffset != -1)
                        {
                            Parameter baseOffsetParam = instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                            if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                            {
                                // 밀리미터를 Revit 내부 단위로 변환
                                double baseOffsetInternal = baseOffset;
                                baseOffsetParam.Set(baseOffsetInternal);
                            }
                        }
                    }
                    break;

                // 특정 뷰의 선 기반 패밀리(예: 상세 컴포넌트)
                case FamilyPlacementType.CurveBasedDetail:
                    if (locationLine == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(Line)} {nameof(locationLine)} 이(가) 누락되었습니다!");
                    if (view == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(View)} {nameof(view)} 이(가) 누락되었습니다!");
                    instance = doc.Create.NewFamilyInstance(
                        locationLine,   // 패밀리 인스턴스의 선 위치입니다. 이 선은 뷰 평면 안에 있어야 합니다
                        familySymbol,   // 삽입할 인스턴스 타입을 나타내는 패밀리 심볼 객체
                        view);          // 패밀리 인스턴스를 배치할 2D 뷰
                    break;

                // 구조 곡선 구동 패밀리(예: 보, 지지대, 경사 기둥)
                case FamilyPlacementType.CurveDrivenStructural:
                    if (locationLine == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(Line)} {nameof(locationLine)} 이(가) 누락되었습니다!");
                    if (baseLevel == null)
                        throw new ArgumentNullException($"필수 파라미터 {typeof(Level)} {nameof(baseLevel)} 이(가) 누락되었습니다!");
                    instance = doc.Create.NewFamilyInstance(
                        locationLine,                   // 패밀리 인스턴스가 기준으로 삼는 곡선
                        familySymbol,                   // 삽입할 인스턴스의 타입을 나타내는 FamilySymbol 객체입니다. 이 Symbol은 FamilyPlacementType이 WorkPlaneBased 또는 CurveBased인 패밀리여야 합니다
                        baseLevel,                      // 객체의 기준 레벨로 사용할 Level 객체
                        StructuralType.Beam);           // 구조 부재인 경우 부재의 타입을 지정
                    break;

                // 적응형 패밀리(예: 적응형 일반 모델, 커튼월 패널)
                case FamilyPlacementType.Adaptive:
                    throw new NotImplementedException("FamilyPlacementType.Adaptive 생성 메서드는 아직 구현되지 않았습니다!");

                default:
                    break;
            }
            return instance;
        }

        /// <summary>
        /// 기본 방향과 손 방향 생성(긴 변은 HandOrientation, 짧은 변은 FacingOrientation)
        /// </summary>
        /// <param name="hostFace"></param>
        /// <returns></returns>
        public static (XYZ FacingOrientation, XYZ HandOrientation) GenerateDefaultOrientation(this Document doc, Reference hostFace)
        {
            var facingOrientation = new XYZ();  // 정면 방향: 패밀리 내부 Y축 양의 방향이 로드된 후 향하는 방향
            var handOrientation = new XYZ();    // 손 방향: 패밀리 내부 X축 양의 방향이 로드된 후 향하는 방향

            // Step1 Reference에서 면 객체 가져오기
            Face face = doc.GetElement(hostFace.ElementId).GetGeometryObjectFromReference(hostFace) as Face;

            // Step2 면 윤곽 가져오기
            List<Curve> profile = null;
            // 윤곽선 컬렉션으로, 각 하위 리스트는 하나의 완전한 폐합 윤곽을 나타내며 첫 번째는 보통 외곽 윤곽입니다
            List<List<Curve>> profiles = new List<List<Curve>>();
            // 모든 윤곽 루프 가져오기(외곽 윤곽과 있을 수 있는 내부 구멍 포함)
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            // 각 윤곽 루프 순회
            foreach (EdgeArray loop in edgeLoops)
            {
                List<Curve> currentLoop = new List<Curve>();
                // 루프 안의 각 모서리 가져오기
                foreach (Edge edge in loop)
                {
                    Curve curve = edge.AsCurve();
                    currentLoop.Add(curve);
                }
                // 현재 루프에 모서리가 있으면 결과 컬렉션에 추가
                if (currentLoop.Count > 0)
                {
                    profiles.Add(currentLoop);
                }
            }
            // 첫 번째는 보통 외곽 윤곽
            if (profiles != null && profiles.Any())
                profile = profiles.FirstOrDefault();

            // Step3 면 법선 벡터 가져오기
            XYZ faceNormal = null;
            // 평면이면 법선 벡터 속성을 직접 가져올 수 있음
            if (face is PlanarFace planarFace)
                faceNormal = planarFace.FaceNormal;

            // Step4 면의 두 개의 유효한 주 방향 가져오기(오른손 법칙 충족)
            var result = face.GetMainDirections();
            var primaryDirection = result.PrimaryDirection;
            var secondaryDirection = result.SecondaryDirection;

            // 기본적으로 긴 변 방향은 HandOrientation, 짧은 변 방향은 FacingOrientation
            facingOrientation = primaryDirection;
            handOrientation = secondaryDirection;

            // 오른손 법칙 충족 여부 판단(엄지: HandOrientation, 검지: FacingOrientation, 중지: FaceNormal)
            if (!facingOrientation.IsRightHandRuleCompliant(handOrientation, faceNormal))
            {
                var newHandOrientation = facingOrientation.GenerateIndexFinger(faceNormal);
                if (newHandOrientation != null)
                {
                    handOrientation = newHandOrientation;
                }
            }

            return (facingOrientation, handOrientation);
        }

        /// <summary>
        /// 점에서 가장 가까운 면 Reference 가져오기
        /// </summary>
        /// <param name="doc">현재 문서</param>
        /// <param name="location">대상 점 위치</param>
        /// <param name="radius">검색 반경(내부 단위)</param>
        /// <returns>가장 가까운 면 Reference, 찾지 못하면 null 반환</returns>
        public static Reference GetNearestFaceReference(this Document doc, XYZ location, double radius = 1000 / 304.8)
        {
            try
            {
                // 오차 처리
                location = new XYZ(location.X, location.Y, location.Z + 0.1 / 304.8);

                // 3D 뷰 생성 또는 가져오기
                View3D view3D = null;
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D));

                foreach (View3D v in collector)
                {
                    if (!v.IsTemplate)
                    {
                        view3D = v;
                        break;
                    }
                }

                if (view3D == null)
                {
                    using (Transaction trans = new Transaction(doc, "Create 3D View"))
                    {
                        trans.Start();
                        ViewFamilyType vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        if (vft != null)
                        {
                            view3D = View3D.CreateIsometric(doc, vft.Id);
                        }
                        trans.Commit();
                    }
                }

                if (view3D == null)
                {
                    TaskDialog.Show("오류", "3D 뷰를 생성하거나 가져올 수 없습니다");
                    return null;
                }

                // 6개 방향의 레이 설정
                XYZ[] directions = new XYZ[]
                {
                  XYZ.BasisX,    // X 양의 방향
                  -XYZ.BasisX,   // X 음의 방향
                  XYZ.BasisY,    // Y 양의 방향
                  -XYZ.BasisY,   // Y 음의 방향
                  XYZ.BasisZ,    // Z 양의 방향
                  -XYZ.BasisZ    // Z 음의 방향
                };

                // 필터 생성
                ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
                ElementClassFilter floorFilter = new ElementClassFilter(typeof(Floor));
                ElementClassFilter ceilingFilter = new ElementClassFilter(typeof(Ceiling));
                ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));

                // 필터 결합
                LogicalOrFilter categoryFilter = new LogicalOrFilter(
                    new ElementFilter[] { wallFilter, floorFilter, ceilingFilter, instanceFilter });


                // 1. 가장 단순함: 모든 인스턴스 엘리먼트용 필터
                //ElementFilter filter = new ElementIsElementTypeFilter(true);

                // 레이 추적기 생성
                ReferenceIntersector refIntersector = new ReferenceIntersector(categoryFilter,
                    FindReferenceTarget.Face, view3D);
                refIntersector.FindReferencesInRevitLinks = true; // 링크 파일의 면까지 찾아야 하는 경우

                double minDistance = double.MaxValue;
                Reference nearestFace = null;

                foreach (XYZ direction in directions)
                {
                    // 현재 위치에서 레이 발사
                    IList<ReferenceWithContext> references = refIntersector.Find(location, direction);

                    foreach (ReferenceWithContext rwc in references)
                    {
                        double distance = rwc.Proximity; // 면까지의 거리 가져오기

                        // 검색 범위 안에 있고 거리가 더 가까우면
                        if (distance <= radius && distance < minDistance)
                        {
                            minDistance = distance;
                            nearestFace = rwc.GetReference();
                        }
                    }
                }

                return nearestFace;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("오류", $"가장 가까운 면을 가져오는 중 오류 발생: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 점에서 가장 가까운 호스트 가능 엘리먼트 가져오기
        /// </summary>
        /// <param name="doc">현재 문서</param>
        /// <param name="location">대상 점 위치</param>
        /// <param name="familySymbol">패밀리 타입, 호스트 타입 판별용</param>
        /// <param name="radius">검색 반경(내부 단위)</param>
        /// <returns>가장 가까운 호스트 엘리먼트, 찾지 못하면 null 반환</returns>
        public static Element GetNearestHostElement(this Document doc, XYZ location, FamilySymbol familySymbol, double radius = 5.0)
        {
            try
            {
                // 기본 파라미터 검사
                if (doc == null || location == null || familySymbol == null)
                    return null;

                // 패밀리의 호스트 동작 파라미터 가져오기
                Parameter hostParam = familySymbol.Family.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
                int hostingBehavior = hostParam?.AsInteger() ?? 0;

                // 3D 뷰 생성 또는 가져오기
                View3D view3D = null;
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D));
                foreach (View3D v in viewCollector)
                {
                    if (!v.IsTemplate)
                    {
                        view3D = v;
                        break;
                    }
                }

                if (view3D == null)
                {
                    using (Transaction trans = new Transaction(doc, "Create 3D View"))
                    {
                        trans.Start();
                        ViewFamilyType vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        if (vft != null)
                        {
                            view3D = View3D.CreateIsometric(doc, vft.Id);
                        }
                        trans.Commit();
                    }
                }

                if (view3D == null)
                {
                    TaskDialog.Show("오류", "3D 뷰를 생성하거나 가져올 수 없습니다");
                    return null;
                }

                // 호스트 동작에 따라 타입 필터 생성
                ElementFilter classFilter;
                switch (hostingBehavior)
                {
                    case 1: // Wall based
                        classFilter = new ElementClassFilter(typeof(Wall));
                        break;
                    case 2: // Floor based
                        classFilter = new ElementClassFilter(typeof(Floor));
                        break;
                    case 3: // Ceiling based
                        classFilter = new ElementClassFilter(typeof(Ceiling));
                        break;
                    case 4: // Roof based
                        classFilter = new ElementClassFilter(typeof(RoofBase));
                        break;
                    default:
                        return null; // 지원하지 않는 호스트 타입
                }

                // 6개 방향의 레이 설정
                XYZ[] directions = new XYZ[]
                {
                    XYZ.BasisX,    // X 양의 방향
                    -XYZ.BasisX,   // X 음의 방향
                    XYZ.BasisY,    // Y 양의 방향
                    -XYZ.BasisY,   // Y 음의 방향
                    XYZ.BasisZ,    // Z 양의 방향
                    -XYZ.BasisZ    // Z 음의 방향
                };

                // 레이 추적기 생성
                ReferenceIntersector refIntersector = new ReferenceIntersector(classFilter,
                    FindReferenceTarget.Element, view3D);
                refIntersector.FindReferencesInRevitLinks = true; // 링크 파일의 엘리먼트까지 찾아야 하는 경우

                double minDistance = double.MaxValue;
                Element nearestHost = null;

                foreach (XYZ direction in directions)
                {
                    // 현재 위치에서 레이 발사
                    IList<ReferenceWithContext> references = refIntersector.Find(location, direction);

                    foreach (ReferenceWithContext rwc in references)
                    {
                        double distance = rwc.Proximity; // 엘리먼트까지의 거리 가져오기

                        // 검색 범위 안에 있고 거리가 더 가까우면
                        if (distance <= radius && distance < minDistance)
                        {
                            minDistance = distance;
                            nearestHost = doc.GetElement(rwc.GetReference().ElementId);
                        }
                    }
                }

                return nearestHost;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("오류", $"가장 가까운 호스트 엘리먼트를 가져오는 중 오류 발생: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds the nearest wall to a point using wall location-line distance calculation.
        /// More reliable than ray-casting for door/window placement.
        /// </summary>
        /// <param name="doc">Current Revit document</param>
        /// <param name="point">Target point (internal units, feet)</param>
        /// <param name="level">Level to filter walls on</param>
        /// <param name="tolerance">Extra tolerance beyond half wall width (feet). Default ~5mm.</param>
        /// <returns>Tuple of (wall, projectedPoint, wallDirection, distance) or null</returns>
        public static (Wall wall, XYZ projectedPoint, XYZ wallDirection, double distance)?
            GetNearestWallByLocationLine(
                this Document doc,
                XYZ point,
                Level level,
                double tolerance = 5.0 / 304.8)
        {
            if (doc == null || point == null || level == null)
                return null;

            // Collect all walls on the given level
            var walls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w =>
                {
                    Parameter baseLevelParam = w.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    return baseLevelParam != null && baseLevelParam.AsElementId() == level.Id;
                })
                .ToList();

            Wall bestWall = null;
            XYZ bestProjection = null;
            XYZ bestDirection = null;
            double bestDistance = double.MaxValue;

            foreach (Wall wall in walls)
            {
                LocationCurve locCurve = wall.Location as LocationCurve;
                if (locCurve == null) continue;

                Curve curve = locCurve.Curve;
                if (curve == null) continue;

                // Use Curve.Project() which handles both lines and arcs
                IntersectionResult ir = curve.Project(new XYZ(point.X, point.Y, curve.GetEndPoint(0).Z));
                if (ir == null) continue;

                XYZ projectedPt = ir.XYZPoint;
                double distance = new XYZ(point.X - projectedPt.X, point.Y - projectedPt.Y, 0).GetLength();

                // Check if point is within half the wall width + tolerance
                double halfWidth = wall.Width / 2.0;
                if (distance <= halfWidth + tolerance && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestWall = wall;
                    bestProjection = new XYZ(projectedPt.X, projectedPt.Y, point.Z);

                    // Compute wall direction from curve tangent at projected parameter
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);
                    bestDirection = new XYZ(p1.X - p0.X, p1.Y - p0.Y, 0).Normalize();
                }
            }

            if (bestWall == null)
                return null;

            return (bestWall, bestProjection, bestDirection, bestDistance);
        }

        /// <summary>
        /// 지정한 면 하이라이트 표시
        /// </summary>
        /// <param name="doc">현재 문서</param>
        /// <param name="faceRef">하이라이트할 면 Reference</param>
        /// <param name="duration">하이라이트 지속 시간(밀리초, 기본값 3000밀리초)</param>
        public static void HighlightFace(this Document doc, Reference faceRef)
        {
            if (faceRef == null) return;

            // 솔리드 채우기 패턴 가져오기
            FillPatternElement solidFill = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(x => x.GetFillPattern().IsSolidFill);

            if (solidFill == null)
            {
                TaskDialog.Show("오류", "솔리드 채우기 패턴을 찾지 못했습니다");
                return;
            }

            // 하이라이트 표시 설정 생성
            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternColor(new Color(255, 0, 0)); // 빨간색
            ogs.SetSurfaceForegroundPatternId(solidFill.Id);
            ogs.SetSurfaceTransparency(0); // 불투명

            // 하이라이트 표시
            doc.ActiveView.SetElementOverrides(faceRef.ElementId, ogs);
        }

        /// <summary>
        /// 면의 두 주요 방향 벡터 추출
        /// </summary>
        /// <param name="face">입력 면</param>
        /// <returns>주 방향과 보조 방향을 포함하는 튜플</returns>
        /// <exception cref="ArgumentNullException">면이 null일 때 발생</exception>
        /// <exception cref="ArgumentException">면의 윤곽이 유효한 형상을 만들기에 부족할 때 발생</exception>
        /// <exception cref="InvalidOperationException">유효한 방향을 추출할 수 없을 때 발생</exception>
        public static (XYZ PrimaryDirection, XYZ SecondaryDirection) GetMainDirections(this Face face)
        {
            // 1. 파라미터 검증
            if (face == null)
                throw new ArgumentNullException(nameof(face), "면은 비워 둘 수 없습니다");

            // 2. 면의 법선 벡터 가져오기, 이후 필요할 수 있는 수직 벡터 계산에 사용
            XYZ faceNormal = face.ComputeNormal(new UV(0.5, 0.5));

            // 3. 면의 외곽 윤곽 가져오기
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            if (edgeLoops.Size == 0)
                throw new ArgumentException("면에 유효한 에지 루프가 없습니다", nameof(face));

            // 보통 첫 번째 루프가 외곽 윤곽임
            EdgeArray outerLoop = edgeLoops.get_Item(0);

            // 4. 각 모서리의 방향 벡터와 길이 계산
            List<XYZ> edgeDirections = new List<XYZ>();  // 각 모서리의 단위 방향 벡터 저장
            List<double> edgeLengths = new List<double>(); // 각 모서리의 길이 저장

            foreach (Edge edge in outerLoop)
            {
                Curve curve = edge.AsCurve();
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);

                // 시작점에서 끝점까지의 벡터 계산
                XYZ direction = endPoint - startPoint;
                double length = direction.GetLength();

                // 너무 짧은 모서리는 무시(정점 중복 또는 수치 정밀도 문제 가능)
                if (length > 1e-10)
                {
                    edgeDirections.Add(direction.Normalize());  // 정규화된 방향 벡터 저장
                    edgeLengths.Add(length);                    // 모서리 길이 저장
                }
            }

            if (edgeDirections.Count < 4) // 최소 4개의 모서리가 있는지 확인
            {
                throw new ArgumentException("제공된 면에는 유효한 형상을 만들 만큼 충분한 변이 없습니다", nameof(face));
            }

            // 5. 유사한 방향의 모서리끼리 그룹화
            List<List<int>> directionGroups = new List<List<int>>();  // 방향 그룹 저장, 각 그룹은 모서리 인덱스를 포함

            for (int i = 0; i < edgeDirections.Count; i++)
            {
                bool foundGroup = false;
                XYZ currentDirection = edgeDirections[i];

                // 현재 모서리를 기존 방향 그룹에 추가 시도
                for (int j = 0; j < directionGroups.Count; j++)
                {
                    var group = directionGroups[j];
                    // 현재 그룹의 가중 평균 방향 계산
                    XYZ groupAvgDir = CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths);

                    // 현재 방향이 그룹 평균 방향과 유사한지 확인(정반대 방향 포함)
                    double dotProduct = Math.Abs(groupAvgDir.DotProduct(currentDirection));
                    if (dotProduct > 0.8) // 약 30도 이내 편차는 유사 방향으로 간주
                    {
                        group.Add(i);  // 현재 모서리 인덱스를 해당 방향 그룹에 추가
                        foundGroup = true;
                        break;
                    }
                }

                // 현재 모서리가 어떤 기존 그룹과도 유사하지 않으면 새 그룹 생성
                if (!foundGroup)
                {
                    List<int> newGroup = new List<int> { i };
                    directionGroups.Add(newGroup);
                }
            }

            // 6. 각 방향 그룹의 총 가중치(모서리 길이 합)와 평균 방향 계산
            List<double> groupWeights = new List<double>();
            List<XYZ> groupDirections = new List<XYZ>();

            foreach (var group in directionGroups)
            {
                // 해당 그룹의 모든 모서리 길이 합 계산
                double totalLength = 0;
                foreach (int edgeIndex in group)
                {
                    totalLength += edgeLengths[edgeIndex];
                }
                groupWeights.Add(totalLength);

                // 해당 그룹의 가중 평균 방향 계산
                groupDirections.Add(CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths));
            }

            // 7. 가중치 기준으로 정렬해 주요 방향 추출
            int[] sortedIndices = Enumerable.Range(0, groupDirections.Count)
                .OrderByDescending(i => groupWeights[i])
                .ToArray();

            // 8. 결과 구성
            if (groupDirections.Count >= 2)
            {
                // 방향 그룹이 최소 두 개면 가중치가 가장 큰 두 그룹을 주 방향과 부 방향으로 사용
                int primaryIndex = sortedIndices[0];
                int secondaryIndex = sortedIndices[1];

                return (
                    PrimaryDirection: groupDirections[primaryIndex],      // 주 방향
                    SecondaryDirection: groupDirections[secondaryIndex]   // 부 방향
                );
            }
            else if (groupDirections.Count == 1)
            {
                // 방향 그룹이 하나뿐이면 주 방향에 수직인 부 방향을 수동으로 생성
                XYZ primaryDirection = groupDirections[0];
                // 면 법선 벡터와 주 방향의 외적으로 수직 벡터 생성
                XYZ secondaryDirection = faceNormal.CrossProduct(primaryDirection).Normalize();

                return (
                    PrimaryDirection: primaryDirection,         // 주 방향 
                    SecondaryDirection: secondaryDirection      // 수동으로 구성한 수직 부 방향
                );
            }
            else
            {
                // 유효한 방향을 추출할 수 없음(드물게 발생)
                throw new InvalidOperationException("면에서 유효한 방향을 추출할 수 없습니다");
            }
        }

        /// <summary>
        /// 변 길이를 기준으로 한 그룹의 가중 평균 방향 계산
        /// </summary>
        /// <param name="edgeIndices">변 인덱스 목록</param>
        /// <param name="directions">모든 변의 방향 벡터</param>
        /// <param name="lengths">모든 변의 길이</param>
        /// <returns>정규화된 가중 평균 방향 벡터</returns>
        public static XYZ CalculateWeightedAverageDirection(List<int> edgeIndices, List<XYZ> directions, List<double> lengths)
        {
            if (edgeIndices.Count == 0)
                return null;

            double sumX = 0, sumY = 0, sumZ = 0;
            XYZ referenceDir = directions[edgeIndices[0]];  // 그룹 내 첫 번째 방향을 기준으로 사용

            foreach (int i in edgeIndices)
            {
                XYZ currentDir = directions[i];

                // 현재 방향과 기준 방향의 내적을 계산해 반전이 필요한지 판단
                double dot = referenceDir.DotProduct(currentDir);

                // 방향이 반대면(내적이 음수) 벡터를 반전한 뒤 기여도를 계산
                // 이렇게 하면 같은 그룹 내 벡터 방향이 일관되어 서로 상쇄되는 것을 방지
                double factor = (dot >= 0) ? lengths[i] : -lengths[i];

                // 벡터 성분 누적(가중치 포함)
                sumX += currentDir.X * factor;
                sumY += currentDir.Y * factor;
                sumZ += currentDir.Z * factor;
            }

            // 합성 벡터 생성 후 정규화
            XYZ avgDir = new XYZ(sumX, sumY, sumZ);
            double magnitude = avgDir.GetLength();

            // 영벡터 방지
            if (magnitude < 1e-10)
                return referenceDir;  // 기준 방향으로 폴백

            return avgDir.Normalize();  // 정규화된 방향 벡터 반환
        }

        /// <summary>
        /// 세 벡터가 동시에 오른손 법칙을 만족하고 서로 엄격히 수직인지 판단
        /// </summary>
        /// <param name="thumb">엄지 방향 벡터</param>
        /// <param name="indexFinger">검지 방향 벡터</param>
        /// <param name="middleFinger">중지 방향 벡터</param>
        /// <param name="tolerance">판단 허용오차, 기본값 1e-6</param>
        /// <returns>세 벡터가 오른손 법칙과 수직 조건을 만족하면 true, 아니면 false</returns>
        public static bool IsRightHandRuleCompliant(this XYZ thumb, XYZ indexFinger, XYZ middleFinger, double tolerance = 1e-6)
        {
            // 세 벡터가 서로 수직인지 확인(모든 내적이 0에 가까움)
            double dotThumbIndex = Math.Abs(thumb.DotProduct(indexFinger));
            double dotThumbMiddle = Math.Abs(thumb.DotProduct(middleFinger));
            double dotIndexMiddle = Math.Abs(indexFinger.DotProduct(middleFinger));

            bool areOrthogonal = (dotThumbIndex <= tolerance) &&
                                  (dotThumbMiddle <= tolerance) &&
                                  (dotIndexMiddle <= tolerance);

            // 세 벡터가 서로 수직일 때만 오른손 법칙을 검사
            if (!areOrthogonal)
                return false;

            // 외적 벡터와 엄지 방향의 내적을 계산해 오른손 법칙 충족 여부 판단
            XYZ crossProduct = indexFinger.CrossProduct(middleFinger);
            double rightHandTest = crossProduct.DotProduct(thumb);

            // 내적이 양수이면 오른손 법칙을 충족
            return rightHandTest > tolerance;
        }

        /// <summary>
        /// 엄지와 중지 방향을 기반으로 오른손 법칙을 만족하는 검지 방향 생성
        /// </summary>
        /// <param name="thumb">엄지 방향 벡터</param>
        /// <param name="middleFinger">중지 방향 벡터</param>
        /// <param name="tolerance">수직 판정 허용오차, 기본값 1e-6</param>
        /// <returns>생성된 검지 방향 벡터, 입력 벡터가 수직이 아니면 null 반환</returns>
        public static XYZ GenerateIndexFinger(this XYZ thumb, XYZ middleFinger, double tolerance = 1e-6)
        {
            // 먼저 입력 벡터를 정규화
            XYZ normalizedThumb = thumb.Normalize();
            XYZ normalizedMiddleFinger = middleFinger.Normalize();

            // 두 벡터가 수직인지 확인(내적이 0에 가까움)
            double dotProduct = normalizedThumb.DotProduct(normalizedMiddleFinger);

            // 내적의 절대값이 허용오차보다 크면 벡터는 수직이 아님
            if (Math.Abs(dotProduct) > tolerance)
            {
                return null;
            }

            // 외적으로 검지 방향을 계산하고 반전
            XYZ indexFinger = normalizedMiddleFinger.CrossProduct(normalizedThumb).Negate();

            // 정규화된 검지 방향 벡터 반환
            return indexFinger.Normalize();
        }

        /// <summary>
        /// 지정한 높이의 레벨을 생성하거나 가져오기
        /// </summary>
        /// <param name="doc">revitRevit 문서</param>
        /// <param name="elevation">레벨 높이(ft)</param>
        /// <param name="levelName">레벨 이름</param>
        /// <returns></returns>
        public static Level CreateOrGetLevel(this Document doc, double elevation, string levelName)
        {
            // 먼저 지정한 높이의 레벨이 있는지 확인
            Level existingLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => Math.Abs(l.Elevation - elevation) < 0.1 / 304.8);

            if (existingLevel != null)
                return existingLevel;

            // 새 레벨 생성
            Level newLevel = Level.Create(doc, elevation);
            // 레벨 이름 설정
            Level namesakeLevel = new FilteredElementCollector(doc)
                 .OfClass(typeof(Level))
                 .Cast<Level>()
                 .FirstOrDefault(l => l.Name == levelName);
            if (namesakeLevel != null)
            {
                levelName = $"{levelName}_{newLevel.Id.GetValue()}";
            }
            newLevel.Name = levelName;

            return newLevel;
        }

        /// <summary>
        /// 주어진 높이에 가장 가까운 레벨 찾기
        /// </summary>
        /// <param name="doc">현재 Revit 문서</param>
        /// <param name="height">대상 높이(Revit 내부 단위)</param>
        /// <returns>대상 높이에 가장 가까운 레벨, 문서에 레벨이 없으면 null 반환</returns>
        public static Level FindNearestLevel(this Document doc, double height)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "문서는 비워 둘 수 없습니다");

            // LINQ 쿼리로 가장 가까운 레벨을 직접 가져오기
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => Math.Abs(level.Elevation - height))
                .FirstOrDefault();
        }

        ///// <summary>
        ///// 뷰를 새로 고치고 지연 추가
        ///// </summary>
        //public static void Refresh(this Document doc, int waitingTime = 0, bool allowOperation = true)
        //{
        //    UIApplication uiApp = new UIApplication(doc.Application);
        //    UIDocument uiDoc = uiApp.ActiveUIDocument;

        //    // if (uiDoc.Document.IsModifiable)
        //    {
        //        // uiDoc.Document.Regenerate();
        //    }
        //    // uiDoc.RefreshActiveView();

        //    // if (waitingTime != 0)
        //    {
        //        System.Threading.Thread.Sleep(waitingTime);
        //    }

        //    // if (allowOperation)
        //    {
        //        System.Windows.Forms.Application.DoEvents();
        //    }
        //}

        /// <summary>
        /// 지정한 메시지를 바탕화면의 지정 파일에 저장(기본값은 덮어쓰기)
        /// </summary>
        /// <param name="message">저장할 메시지 내용</param>
        /// <param name="fileName">대상 파일명</param>
        public static void SaveToDesktop(this string message, string fileName = "temp.json", bool isAppend = false)
        {
            // logName에 확장자가 포함되었는지 확인
            if (!Path.HasExtension(fileName))
            {
                fileName += ".txt"; // 기본적으로 .txt 확장자 추가
            }

            // 바탕 화면 경로 가져오기
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            // 전체 파일 경로 조합
            string filePath = Path.Combine(desktopPath, fileName);

            // 파일 쓰기(덮어쓰기 모드)
            using (StreamWriter sw = new StreamWriter(filePath, isAppend))
            {
                sw.WriteLine($"{message}");
            }
        }

    }
}
