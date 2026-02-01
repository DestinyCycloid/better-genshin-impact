using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BetterGenshinImpact.Core.Config;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BetterGenshinImpact.Service;

/// <summary>
/// GamepadButtonMapping 的 JSON 转换器
/// 确保 Xbox360Button 枚举被正确序列化为整数值
/// </summary>
public class GamepadButtonMappingJsonConverter : JsonConverter<GamepadButtonMapping>
{
    public override GamepadButtonMapping Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var mapping = new GamepadButtonMapping();
        
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return mapping;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName?.ToLowerInvariant())
            {
                case "button":
                    // 处理旧格式（对象）和新格式（整数）
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        // 旧格式：{"value": 16384, "name": "X", "id": 13}
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.EndObject)
                            {
                                break;
                            }

                            if (reader.TokenType == JsonTokenType.PropertyName)
                            {
                                var objPropertyName = reader.GetString();
                                reader.Read();
                                
                                if (objPropertyName == "value" && reader.TokenType == JsonTokenType.Number)
                                {
                                    var intValue = reader.GetInt32();
                                    mapping.Button = (Xbox360Button)Enum.ToObject(typeof(Xbox360Button), intValue);
                                }
                            }
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.Number)
                    {
                        // 新格式：整数值
                        var intValue = reader.GetInt32();
                        mapping.Button = (Xbox360Button)Enum.ToObject(typeof(Xbox360Button), intValue);
                    }
                    break;

                case "istrigger":
                    if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                    {
                        mapping.IsTrigger = reader.GetBoolean();
                    }
                    break;

                case "islefttrigger":
                    if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                    {
                        mapping.IsLeftTrigger = reader.GetBoolean();
                    }
                    break;

                case "iscombo":
                    if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                    {
                        mapping.IsCombo = reader.GetBoolean();
                    }
                    break;

                case "modifierbutton":
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        var intValue = reader.GetInt32();
                        mapping.ModifierButton = (Xbox360Button)Enum.ToObject(typeof(Xbox360Button), intValue);
                    }
                    break;

                case "mainbutton":
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        var intValue = reader.GetInt32();
                        mapping.MainButton = (Xbox360Button)Enum.ToObject(typeof(Xbox360Button), intValue);
                    }
                    break;
            }
        }

        return mapping;
    }

    public override void Write(Utf8JsonWriter writer, GamepadButtonMapping value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        // 将 Button 序列化为整数值
        // Xbox360Button 是标志枚举，需要先转换为 object 再转为 int
        var buttonValue = Convert.ToInt32(Enum.ToObject(typeof(Xbox360Button), value.Button));
        writer.WriteNumber("button", buttonValue);
        writer.WriteBoolean("isTrigger", value.IsTrigger);
        writer.WriteBoolean("isLeftTrigger", value.IsLeftTrigger);
        
        // 组合键相关字段
        writer.WriteBoolean("isCombo", value.IsCombo);
        if (value.IsCombo)
        {
            var modifierValue = Convert.ToInt32(Enum.ToObject(typeof(Xbox360Button), value.ModifierButton));
            var mainValue = Convert.ToInt32(Enum.ToObject(typeof(Xbox360Button), value.MainButton));
            writer.WriteNumber("modifierButton", modifierValue);
            writer.WriteNumber("mainButton", mainValue);
        }
        
        writer.WriteEndObject();
    }
}
