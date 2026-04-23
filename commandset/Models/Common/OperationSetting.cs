using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// 엘리먼트에 대해 수행할 수 있는 작업 유형 정의
    /// </summary>
    public enum ElementOperationType
    {
        /// <summary>
        /// 엘리먼트 선택
        /// </summary>
        Select,

        /// <summary>
        /// 선택 상자
        /// </summary>
        SelectionBox,

        /// <summary>
        /// 엘리먼트 색상 및 채우기 설정
        /// </summary>
        SetColor,

        /// <summary>
        /// 엘리먼트 투명도 설정
        /// </summary>
        SetTransparency,

        /// <summary>
        /// 엘리먼트 삭제
        /// </summary>
        Delete,

        /// <summary>
        /// 엘리먼트 숨기기
        /// </summary>
        Hide,

        /// <summary>
        /// 엘리먼트 임시 숨기기
        /// </summary>
        TempHide,

        /// <summary>
        /// 엘리먼트 분리 (단독 표시)
        /// </summary>
        Isolate,

        /// <summary>
        /// 엘리먼트 숨기기 해제
        /// </summary>
        Unhide,

        /// <summary>
        /// 분리 재설정 (모든 엘리먼트 표시)
        /// </summary>
        ResetIsolate,
    }


    /// <summary>
    /// 엘리먼트 조작 설정
    /// </summary>
    public class OperationSetting
    {
        /// <summary>
        /// 조작할 엘리먼트 ID 목록
        /// </summary>
        [JsonProperty("elementIds")]
        public List<int> ElementIds = new List<int>();

        /// <summary>
        /// 수행할 작업, ElementOperationType 열거형의 string 타입 값을 저장
        /// </summary>
        [JsonProperty("action")]
        public string Action { get; set; } = "Select";

        /// <summary>
        /// 투명도 값 (0-100), 값이 클수록 투명도가 높음
        /// </summary>
        [JsonProperty("transparencyValue")]
        public int TransparencyValue { get; set; } = 50;

        /// <summary>
        /// 엘리먼트 색상 설정 (RGB 형식), 기본값은 빨간색
        /// </summary>
        [JsonProperty("colorValue")]
        public int[] ColorValue { get; set; } = new int[] { 255, 0, 0 }; // 기본 빨간색
    }
}
