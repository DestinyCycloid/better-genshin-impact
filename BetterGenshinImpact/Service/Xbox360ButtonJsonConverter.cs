using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BetterGenshinImpact.Service;

/// <summary>
/// Xbox360Button 枚举的 JSON 转换器
/// 将枚举序列化为整数值，而不是复杂对象
/// </summary>
public class Xbox360ButtonJsonConverter : JsonConverter<Xbox360Button>
{
    public override Xbox360Button Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 如果是对象格式（旧格式），尝试读取 value 字段
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return default;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();
                    
                    if (propertyName == "value" && reader.TokenType == JsonTokenType.Number)
                    {
                        var intValue = reader.GetInt32();
                        return (Xbox360Button)Enum.ToObject(typeof(Xbox360Button), intValue);
                    }
                }
            }
            return default;
        }
        
        // 如果是数字格式（新格式）
        if (reader.TokenType == JsonTokenType.Number)
        {
            var intValue = reader.GetInt32();
            return (Xbox360Button)Enum.ToObject(typeof(Xbox360Button), intValue);
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, Xbox360Button value, JsonSerializerOptions options)
    {
        // 序列化为整数值
        writer.WriteNumberValue(Convert.ToInt32(value));
    }
}
