using System;
using System.Text;
using System.Windows.Input;

namespace BetterGenshinImpact.Model;

public readonly partial record struct HotKey(Key Key, ModifierKeys Modifiers = ModifierKeys.None, MouseButton MouseButton = MouseButton.Left, string RawString = "")
{
    public override string ToString()
    {
        // 如果有原始字符串（手柄按键），直接返回
        if (!string.IsNullOrEmpty(RawString))
        {
            return RawString;
        }
        
        if (Key == Key.None && Modifiers == ModifierKeys.None && MouseButton == MouseButton.Left)
        {
            return "< None >";
        }

        if (MouseButton != MouseButton.Left)
        {
            return MouseButton.ToString();
        }

        var buffer = new StringBuilder();

        if (Modifiers.HasFlag(ModifierKeys.Control))
            buffer.Append("Ctrl + ");
        if (Modifiers.HasFlag(ModifierKeys.Shift))
            buffer.Append("Shift + ");
        if (Modifiers.HasFlag(ModifierKeys.Alt))
            buffer.Append("Alt + ");
        if (Modifiers.HasFlag(ModifierKeys.Windows))
            buffer.Append("Win + ");

        buffer.Append(Key);

        return buffer.ToString();
    }

    public bool IsEmpty => Key == Key.None && Modifiers == ModifierKeys.None && MouseButton == MouseButton.Left && string.IsNullOrEmpty(RawString);

    public static HotKey FromString(string str)
    {
        var key = Key.None;
        var modifiers = ModifierKeys.None;
        var mouseButton = MouseButton.Left;
        if (string.IsNullOrWhiteSpace(str) || string.Equals(str, "< None >"))
        {
            return new HotKey(key, modifiers, mouseButton);
        }

        if (str == MouseButton.XButton1.ToString())
        {
            return new HotKey(key, modifiers, MouseButton.XButton1);
        }

        if (str == MouseButton.XButton2.ToString())
        {
            return new HotKey(key, modifiers, MouseButton.XButton2);
        }

        // 检查是否是手柄按键（包含手柄按键名称）
        // 手柄按键格式：单键如 "A", "LB"，组合键如 "LB+A"
        if (IsGamepadButton(str))
        {
            // 手柄按键存储原始字符串
            return new HotKey(key, modifiers, mouseButton, str);
        }

        var parts = str.Split('+');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed == "Ctrl")
                modifiers |= ModifierKeys.Control;
            else if (trimmed == "Shift")
                modifiers |= ModifierKeys.Shift;
            else if (trimmed == "Alt")
                modifiers |= ModifierKeys.Alt;
            else if (trimmed == "Win")
                modifiers |= ModifierKeys.Windows;
            else if (trimmed == "XButton1")
                mouseButton = MouseButton.XButton1;
            else if (trimmed == "XButton2")
                mouseButton = MouseButton.XButton2;
            else
                key = (Key)Enum.Parse(typeof(Key), trimmed);
        }

        return new HotKey(key, modifiers, mouseButton);
    }

    public static bool IsMouseButton(string name)
    {
        return name == MouseButton.XButton1.ToString() || name == MouseButton.XButton2.ToString();
    }
    
    /// <summary>
    /// 检查字符串是否包含手柄按键名称
    /// </summary>
    private static bool IsGamepadButton(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return false;
            
        // 手柄按键名称列表
        string[] gamepadButtons = 
        {
            "A", "B", "X", "Y",
            "LB", "RB", "LT", "RT",
            "Start", "Back",
            "LeftThumb", "RightThumb",
            "DPadUp", "DPadDown", "DPadLeft", "DPadRight"
        };
        
        // 检查单键或组合键中的任意部分
        var parts = str.Split('+');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            foreach (var button in gamepadButtons)
            {
                if (trimmed == button)
                    return true;
            }
        }
        
        return false;
    }
}

public partial record struct HotKey
{
    public static HotKey None { get; } = new();
}