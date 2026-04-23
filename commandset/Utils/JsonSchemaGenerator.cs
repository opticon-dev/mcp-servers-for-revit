using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Utils
{
    public static class JsonSchemaGenerator
    {
        /// <summary>
        /// 지정된 타입의 JSON Schema를 생성하고 변환합니다.
        /// </summary>
        /// <typeparam name="T">생성할 Schema의 타입</typeparam>
        /// <param name="mainPropertyName">변환 후 Schema의 주요 속성 이름</param>
        /// <returns>변환된 JSON Schema 문자열</returns>
        public static string GenerateTransformedSchema<T>(string mainPropertyName)
        {
            return GenerateTransformedSchema<T>(mainPropertyName, false);
        }

        /// <summary>
        /// 지정된 타입의 JSON Schema를 생성하고 변환하며, ThinkingProcess 속성을 지원합니다.
        /// </summary>
        /// <typeparam name="T">생성할 Schema의 타입</typeparam>
        /// <param name="mainPropertyName">변환 후 Schema의 주요 속성 이름</param>
        /// <param name="includeThinkingProcess">ThinkingProcess 속성을 추가할지 여부</param>
        /// <returns>변환된 JSON Schema 문자열</returns>
        public static string GenerateTransformedSchema<T>(string mainPropertyName, bool includeThinkingProcess)
        {
            if (string.IsNullOrWhiteSpace(mainPropertyName))
                throw new ArgumentException("Main property name cannot be null or empty.", nameof(mainPropertyName));

            // 루트 Schema 생성
            JObject rootSchema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject(),
                ["required"] = new JArray(),
                ["additionalProperties"] = false
            };

            // ThinkingProcess 속성을 추가해야 하는 경우
            if (includeThinkingProcess)
            {
                AddProperty(rootSchema, "ThinkingProcess", new JObject { ["type"] = "string" }, true);
            }

            // 대상 속성의 Schema 생성
            JObject mainPropertySchema = GenerateSchema(typeof(T));
            AddProperty(rootSchema, mainPropertyName, mainPropertySchema, true);

            // 모든 객체에 재귀적으로 "additionalProperties": false 추가
            AddAdditionalPropertiesFalse(rootSchema);

            // 포맷된 JSON Schema 반환
            return JsonConvert.SerializeObject(rootSchema, Formatting.Indented);
        }

        /// <summary>
        /// 지정된 타입의 JSON Schema를 재귀적으로 생성합니다.
        /// </summary>
        private static JObject GenerateSchema(Type type)
        {
            if (type == typeof(string)) return new JObject { ["type"] = "string" };
            if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return new JObject { ["type"] = "integer" };
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return new JObject { ["type"] = "number" };
            if (type == typeof(bool)) return new JObject { ["type"] = "boolean" };

            // Dictionary 타입을 우선 처리
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return HandleDictionary(type);

            // 배열 또는 컬렉션 타입 처리
            if (type.IsArray || (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType))
            {
                Type itemType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                return new JObject
                {
                    ["type"] = "array",
                    ["items"] = GenerateSchema(itemType)
                };
            }

            // 클래스 타입 처리
            if (type.IsClass)
            {
                var schema = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["required"] = new JArray(),
                    ["additionalProperties"] = false
                };

                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    AddProperty(schema, prop.Name, GenerateSchema(prop.PropertyType), isRequired: true);
                }
                return schema;
            }

            // 기본적으로 문자열로 처리
            return new JObject { ["type"] = "string" };
        }

        /// <summary>
        /// Dictionary<string, TValue> 타입을 전용 처리하여 키가 string 타입인지 보장하고 값 타입을 올바르게 처리합니다.
        /// </summary>
        private static JObject HandleDictionary(Type type)
        {
            Type keyType = type.GetGenericArguments()[0];
            Type valueType = type.GetGenericArguments()[1];

            if (keyType != typeof(string))
            {
                throw new NotSupportedException("JSON Schema only supports dictionaries with string keys.");
            }

            return new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = GenerateSchema(valueType)
            };
        }

        /// <summary>
        /// Schema에 속성을 추가합니다.
        /// </summary>
        private static void AddProperty(JObject schema, string propertyName, JToken propertySchema, bool isRequired)
        {
            ((JObject)schema["properties"]).Add(propertyName, propertySchema);

            if (isRequired)
            {
                ((JArray)schema["required"]).Add(propertyName);
            }
        }

        /// <summary>
        /// "required" 속성을 포함한 객체에 재귀적으로 "additionalProperties": false를 추가합니다.
        /// </summary>
        private static void AddAdditionalPropertiesFalse(JToken token)
        {
            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                if (obj["required"] != null && obj["additionalProperties"] == null)
                {
                    obj["additionalProperties"] = false;
                }

                foreach (var property in obj.Properties())
                {
                    AddAdditionalPropertiesFalse(property.Value);
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in (JArray)token)
                {
                    AddAdditionalPropertiesFalse(item);
                }
            }
        }
    }
}
