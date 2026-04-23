using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// 필터 설정 - 조합 조건 필터링 지원
    /// </summary>
    public class FilterSetting
    {
        /// <summary>
        /// 필터링할 Revit 내장 카테고리 이름을 가져오거나 설정 (예: "OST_Walls").
        /// null이거나 비어 있으면 카테고리 필터링을 수행하지 않음.
        /// </summary>
        [JsonProperty("filterCategory")]
        public string FilterCategory { get; set; } = null;
        /// <summary>
        /// 필터링할 Revit 엘리먼트 타입 이름을 가져오거나 설정 (예: "Wall" 또는 "Autodesk.Revit.DB.Wall").
        /// null이거나 비어 있으면 타입 필터링을 수행하지 않음.
        /// </summary>
        [JsonProperty("filterElementType")]
        public string FilterElementType { get; set; } = null;
        /// <summary>
        /// 필터링할 패밀리 타입의 ElementId 값(FamilySymbol)을 가져오거나 설정.
        /// 0이거나 음수이면 패밀리 필터링을 수행하지 않음.
        /// 주의: 이 필터는 엘리먼트 인스턴스에만 적용되며, 타입 엘리먼트에는 적용되지 않음.
        /// </summary>
        [JsonProperty("filterFamilySymbolId")]
        public int FilterFamilySymbolId { get; set; } = -1;
        /// <summary>
        /// 엘리먼트 타입(예: 벽 타입, 문 타입 등)을 포함할지 여부를 가져오거나 설정
        /// </summary>
        [JsonProperty("includeTypes")]
        public bool IncludeTypes { get; set; } = false;
        /// <summary>
        /// 엘리먼트 인스턴스(예: 배치된 벽, 문 등)를 포함할지 여부를 가져오거나 설정
        /// </summary>
        [JsonProperty("includeInstances")]
        public bool IncludeInstances { get; set; } = true;
        /// <summary>
        /// 현재 뷰에서 보이는 엘리먼트만 반환할지 여부를 가져오거나 설정.
        /// 주의: 이 필터는 엘리먼트 인스턴스에만 적용되며, 타입 엘리먼트에는 적용되지 않음.
        /// </summary>
        [JsonProperty("filterVisibleInCurrentView")]
        public bool FilterVisibleInCurrentView { get; set; }
        /// <summary>
        /// 공간 범위 필터링의 최소점 좌표를 가져오거나 설정 (단위: mm)
        /// 이 값과 BoundingBoxMax가 설정되면, 이 경계 상자와 교차하는 엘리먼트를 필터링
        /// </summary>
        [JsonProperty("boundingBoxMin")]
        public JZPoint BoundingBoxMin { get; set; } = null;
        /// <summary>
        /// 공간 범위 필터링의 최대점 좌표를 가져오거나 설정 (단위: mm)
        /// 이 값과 BoundingBoxMin이 설정되면, 이 경계 상자와 교차하는 엘리먼트를 필터링
        /// </summary>
        [JsonProperty("boundingBoxMax")]
        public JZPoint BoundingBoxMax { get; set; } = null;
        /// <summary>
        /// 최대 엘리먼트 수 제한
        /// </summary>
        [JsonProperty("maxElements")]
        public int MaxElements { get; set; } = 50;
        /// <summary>
        /// 필터 설정의 유효성을 검증하고, 잠재적인 충돌을 확인
        /// </summary>
        /// <returns>설정이 유효하면 true 반환, 그렇지 않으면 false 반환</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = null;

            // 최소 하나의 엘리먼트 종류가 선택되었는지 확인
            if (!IncludeTypes && !IncludeInstances)
            {
                errorMessage = "필터 설정이 유효하지 않음: 엘리먼트 타입 또는 엘리먼트 인스턴스 중 최소 하나를 포함해야 함";
                return false;
            }

            // 최소 하나의 필터 조건이 지정되었는지 확인
            if (string.IsNullOrWhiteSpace(FilterCategory) &&
                string.IsNullOrWhiteSpace(FilterElementType) &&
                FilterFamilySymbolId <= 0)
            {
                errorMessage = "필터 설정이 유효하지 않음: 최소 하나의 필터 조건(카테고리, 엘리먼트 타입 또는 패밀리 타입)을 지정해야 함";
                return false;
            }

            // 타입 엘리먼트와 특정 필터 간의 충돌 확인
            if (IncludeTypes && !IncludeInstances)
            {
                List<string> invalidFilters = new List<string>();
                if (FilterFamilySymbolId > 0)
                    invalidFilters.Add("패밀리 인스턴스 필터");
                if (FilterVisibleInCurrentView)
                    invalidFilters.Add("뷰 가시성 필터");
                if (invalidFilters.Count > 0)
                {
                    errorMessage = $"타입 엘리먼트만 필터링할 때는 다음 필터를 사용할 수 없음: {string.Join(", ", invalidFilters)}";
                    return false;
                }
            }
            // 공간 범위 필터의 유효성 확인
            if (BoundingBoxMin != null && BoundingBoxMax != null)
            {
                // 최소점이 최대점보다 작거나 같은지 확인
                if (BoundingBoxMin.X > BoundingBoxMax.X ||
                    BoundingBoxMin.Y > BoundingBoxMax.Y ||
                    BoundingBoxMin.Z > BoundingBoxMax.Z)
                {
                    errorMessage = "공간 범위 필터 설정이 유효하지 않음: 최소점 좌표가 최대점 좌표보다 작거나 같아야 함";
                    return false;
                }
            }
            else if (BoundingBoxMin != null || BoundingBoxMax != null)
            {
                errorMessage = "공간 범위 필터 설정이 유효하지 않음: 최소점과 최대점 좌표를 동시에 설정해야 함";
                return false;
            }
            return true;
        }
    }
}
