using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Colors;

namespace AlphaScope.GUI.Modern.Base;

/// <summary>
/// Manages themes and styling for the modern AlphaScope GUI.
/// Provides consistent colors, spacing, and styling across all components.
/// </summary>
public static class ThemeManager
{
    public enum Theme
    {
        Dark,
        Light,
        AlphaScope // Custom AlphaScope theme
    }

    private static Theme _currentTheme = Theme.AlphaScope;
    private static Dictionary<string, Vector4> _customColors = [];

    /// <summary>
    /// Current active theme
    /// </summary>
    public static Theme CurrentTheme
    {
        get => _currentTheme;
        set
        {
            _currentTheme = value;
            ApplyTheme(value);
        }
    }

    /// <summary>
    /// Common colors used throughout the application
    /// </summary>
    public static class Colors
    {
        // Primary colors
        public static Vector4 Primary => GetColor("Primary");
        public static Vector4 PrimaryDark => GetColor("PrimaryDark");
        public static Vector4 PrimaryLight => GetColor("PrimaryLight");
        
        // Status colors
        public static Vector4 Success => GetColor("Success");
        public static Vector4 Warning => GetColor("Warning");
        public static Vector4 Error => GetColor("Error");
        public static Vector4 Info => GetColor("Info");
        
        // UI colors
        public static Vector4 Background => GetColor("Background");
        public static Vector4 Surface => GetColor("Surface");
        public static Vector4 OnSurface => GetColor("OnSurface");
        public static Vector4 Border => GetColor("Border");
        
        // Text colors
        public static Vector4 TextPrimary => GetColor("TextPrimary");
        public static Vector4 TextSecondary => GetColor("TextSecondary");
        public static Vector4 TextMuted => GetColor("TextMuted");
        
        // Interactive colors
        public static Vector4 Interactive => GetColor("Interactive");
        public static Vector4 InteractiveHover => GetColor("InteractiveHover");
        public static Vector4 InteractiveActive => GetColor("InteractiveActive");
    }

    /// <summary>
    /// Initialize the theme system
    /// </summary>
    public static void Initialize()
    {
        SetupThemes();
        ApplyTheme(_currentTheme);
    }

    private static void SetupThemes()
    {
        // AlphaScope Theme (Custom)
        SetupAlphaScopeTheme();
        
        // Dark theme
        SetupDarkTheme();
        
        // Light theme  
        SetupLightTheme();
    }

    private static void SetupAlphaScopeTheme()
    {
        var colors = new Dictionary<string, Vector4>
        {
            // Primary colors - Blue/Purple scheme
            ["Primary"] = new Vector4(0.4f, 0.6f, 1.0f, 1.0f),
            ["PrimaryDark"] = new Vector4(0.2f, 0.4f, 0.8f, 1.0f),
            ["PrimaryLight"] = new Vector4(0.6f, 0.8f, 1.0f, 1.0f),
            
            // Status colors
            ["Success"] = ImGuiColors.HealerGreen,
            ["Warning"] = ImGuiColors.ParsedOrange,
            ["Error"] = ImGuiColors.DalamudRed,
            ["Info"] = ImGuiColors.TankBlue,
            
            // UI colors
            ["Background"] = new Vector4(0.06f, 0.06f, 0.09f, 1.0f),
            ["Surface"] = new Vector4(0.1f, 0.1f, 0.15f, 1.0f),
            ["OnSurface"] = new Vector4(0.9f, 0.9f, 0.95f, 1.0f),
            ["Border"] = new Vector4(0.3f, 0.3f, 0.4f, 0.5f),
            
            // Text colors
            ["TextPrimary"] = new Vector4(0.95f, 0.95f, 0.98f, 1.0f),
            ["TextSecondary"] = new Vector4(0.8f, 0.8f, 0.85f, 1.0f),
            ["TextMuted"] = new Vector4(0.6f, 0.6f, 0.65f, 1.0f),
            
            // Interactive colors
            ["Interactive"] = new Vector4(0.4f, 0.6f, 1.0f, 1.0f),
            ["InteractiveHover"] = new Vector4(0.5f, 0.7f, 1.0f, 1.0f),
            ["InteractiveActive"] = new Vector4(0.3f, 0.5f, 0.9f, 1.0f)
        };

        foreach (var color in colors)
        {
            _customColors[$"AlphaScope_{color.Key}"] = color.Value;
        }
    }

    private static void SetupDarkTheme()
    {
        var colors = new Dictionary<string, Vector4>
        {
            ["Primary"] = new Vector4(0.5f, 0.5f, 0.9f, 1.0f),
            ["PrimaryDark"] = new Vector4(0.3f, 0.3f, 0.7f, 1.0f),
            ["PrimaryLight"] = new Vector4(0.7f, 0.7f, 1.0f, 1.0f),
            
            ["Success"] = new Vector4(0.4f, 0.8f, 0.4f, 1.0f),
            ["Warning"] = new Vector4(1.0f, 0.8f, 0.4f, 1.0f),
            ["Error"] = new Vector4(0.9f, 0.4f, 0.4f, 1.0f),
            ["Info"] = new Vector4(0.4f, 0.7f, 0.9f, 1.0f),
            
            ["Background"] = new Vector4(0.1f, 0.1f, 0.1f, 1.0f),
            ["Surface"] = new Vector4(0.15f, 0.15f, 0.15f, 1.0f),
            ["OnSurface"] = new Vector4(0.9f, 0.9f, 0.9f, 1.0f),
            ["Border"] = new Vector4(0.4f, 0.4f, 0.4f, 0.5f),
            
            ["TextPrimary"] = new Vector4(0.9f, 0.9f, 0.9f, 1.0f),
            ["TextSecondary"] = new Vector4(0.7f, 0.7f, 0.7f, 1.0f),
            ["TextMuted"] = new Vector4(0.5f, 0.5f, 0.5f, 1.0f),
            
            ["Interactive"] = new Vector4(0.5f, 0.5f, 0.9f, 1.0f),
            ["InteractiveHover"] = new Vector4(0.6f, 0.6f, 1.0f, 1.0f),
            ["InteractiveActive"] = new Vector4(0.4f, 0.4f, 0.8f, 1.0f)
        };

        foreach (var color in colors)
        {
            _customColors[$"Dark_{color.Key}"] = color.Value;
        }
    }

    private static void SetupLightTheme()
    {
        var colors = new Dictionary<string, Vector4>
        {
            ["Primary"] = new Vector4(0.2f, 0.4f, 0.8f, 1.0f),
            ["PrimaryDark"] = new Vector4(0.1f, 0.3f, 0.7f, 1.0f),
            ["PrimaryLight"] = new Vector4(0.4f, 0.6f, 0.9f, 1.0f),
            
            ["Success"] = new Vector4(0.2f, 0.6f, 0.2f, 1.0f),
            ["Warning"] = new Vector4(0.8f, 0.6f, 0.2f, 1.0f),
            ["Error"] = new Vector4(0.8f, 0.2f, 0.2f, 1.0f),
            ["Info"] = new Vector4(0.2f, 0.5f, 0.8f, 1.0f),
            
            ["Background"] = new Vector4(0.95f, 0.95f, 0.95f, 1.0f),
            ["Surface"] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            ["OnSurface"] = new Vector4(0.1f, 0.1f, 0.1f, 1.0f),
            ["Border"] = new Vector4(0.6f, 0.6f, 0.6f, 0.5f),
            
            ["TextPrimary"] = new Vector4(0.1f, 0.1f, 0.1f, 1.0f),
            ["TextSecondary"] = new Vector4(0.3f, 0.3f, 0.3f, 1.0f),
            ["TextMuted"] = new Vector4(0.5f, 0.5f, 0.5f, 1.0f),
            
            ["Interactive"] = new Vector4(0.2f, 0.4f, 0.8f, 1.0f),
            ["InteractiveHover"] = new Vector4(0.3f, 0.5f, 0.9f, 1.0f),
            ["InteractiveActive"] = new Vector4(0.1f, 0.3f, 0.7f, 1.0f)
        };

        foreach (var color in colors)
        {
            _customColors[$"Light_{color.Key}"] = color.Value;
        }
    }

    private static Vector4 GetColor(string colorName)
    {
        var key = $"{_currentTheme}_{colorName}";
        return _customColors.TryGetValue(key, out var color) ? color : Vector4.One;
    }

    private static void ApplyTheme(Theme theme)
    {
        // Apply theme to ImGui style
        var style = ImGui.GetStyle();
        
        switch (theme)
        {
            case Theme.Dark:
                ImGui.StyleColorsDark();
                break;
            case Theme.Light:
                ImGui.StyleColorsLight();
                break;
            case Theme.AlphaScope:
                ApplyAlphaScopeStyle(style);
                break;
        }
    }

    private static void ApplyAlphaScopeStyle(ImGuiStylePtr style)
    {
        // Base on dark theme
        ImGui.StyleColorsDark();
        
        // Customize with AlphaScope colors
        var colors = style.Colors;
        
        colors[(int)ImGuiCol.WindowBg] = Colors.Background;
        colors[(int)ImGuiCol.ChildBg] = Colors.Surface;
        colors[(int)ImGuiCol.PopupBg] = Colors.Surface;
        
        colors[(int)ImGuiCol.Border] = Colors.Border;
        colors[(int)ImGuiCol.BorderShadow] = Vector4.Zero;
        
        colors[(int)ImGuiCol.FrameBg] = Colors.Surface;
        colors[(int)ImGuiCol.FrameBgHovered] = Colors.InteractiveHover * 0.4f;
        colors[(int)ImGuiCol.FrameBgActive] = Colors.InteractiveActive * 0.6f;
        
        colors[(int)ImGuiCol.TitleBg] = Colors.Surface;
        colors[(int)ImGuiCol.TitleBgActive] = Colors.Primary * 0.8f;
        colors[(int)ImGuiCol.TitleBgCollapsed] = Colors.Surface * 0.8f;
        
        colors[(int)ImGuiCol.MenuBarBg] = Colors.Surface;
        
        colors[(int)ImGuiCol.ScrollbarBg] = Colors.Background;
        colors[(int)ImGuiCol.ScrollbarGrab] = Colors.Interactive * 0.6f;
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = Colors.InteractiveHover * 0.8f;
        colors[(int)ImGuiCol.ScrollbarGrabActive] = Colors.InteractiveActive;
        
        colors[(int)ImGuiCol.CheckMark] = Colors.Primary;
        colors[(int)ImGuiCol.SliderGrab] = Colors.Primary;
        colors[(int)ImGuiCol.SliderGrabActive] = Colors.PrimaryLight;
        
        colors[(int)ImGuiCol.Button] = Colors.Interactive * 0.6f;
        colors[(int)ImGuiCol.ButtonHovered] = Colors.InteractiveHover * 0.8f;
        colors[(int)ImGuiCol.ButtonActive] = Colors.InteractiveActive;
        
        colors[(int)ImGuiCol.Header] = Colors.Interactive * 0.4f;
        colors[(int)ImGuiCol.HeaderHovered] = Colors.InteractiveHover * 0.6f;
        colors[(int)ImGuiCol.HeaderActive] = Colors.InteractiveActive * 0.8f;
        
        colors[(int)ImGuiCol.Separator] = Colors.Border;
        colors[(int)ImGuiCol.SeparatorHovered] = Colors.InteractiveHover;
        colors[(int)ImGuiCol.SeparatorActive] = Colors.InteractiveActive;
        
        colors[(int)ImGuiCol.ResizeGrip] = Colors.Interactive * 0.4f;
        colors[(int)ImGuiCol.ResizeGripHovered] = Colors.InteractiveHover * 0.6f;
        colors[(int)ImGuiCol.ResizeGripActive] = Colors.InteractiveActive;
        
        colors[(int)ImGuiCol.Tab] = Colors.Surface;
        colors[(int)ImGuiCol.TabHovered] = Colors.InteractiveHover * 0.8f;
        colors[(int)ImGuiCol.TabActive] = Colors.Interactive * 0.8f;
        colors[(int)ImGuiCol.TabUnfocused] = Colors.Surface * 0.9f;
        colors[(int)ImGuiCol.TabUnfocusedActive] = Colors.Interactive * 0.6f;
        
        colors[(int)ImGuiCol.DockingPreview] = Colors.Primary * 0.7f;
        colors[(int)ImGuiCol.DockingEmptyBg] = Colors.Background;
        
        colors[(int)ImGuiCol.PlotLines] = Colors.Primary;
        colors[(int)ImGuiCol.PlotLinesHovered] = Colors.PrimaryLight;
        colors[(int)ImGuiCol.PlotHistogram] = Colors.Primary * 0.8f;
        colors[(int)ImGuiCol.PlotHistogramHovered] = Colors.PrimaryLight;
        
        colors[(int)ImGuiCol.Text] = Colors.TextPrimary;
        colors[(int)ImGuiCol.TextDisabled] = Colors.TextMuted;
        colors[(int)ImGuiCol.TextSelectedBg] = Colors.Primary * 0.3f;
        
        colors[(int)ImGuiCol.NavHighlight] = Colors.Primary;
        colors[(int)ImGuiCol.NavWindowingHighlight] = Colors.Primary * 0.8f;
        colors[(int)ImGuiCol.NavWindowingDimBg] = Colors.Background * 0.2f;
        colors[(int)ImGuiCol.ModalWindowDimBg] = Colors.Background * 0.6f;
        
        // Adjust spacing and sizing
        style.WindowRounding = 4.0f;
        style.ChildRounding = 4.0f;
        style.FrameRounding = 3.0f;
        style.PopupRounding = 4.0f;
        style.ScrollbarRounding = 9.0f;
        style.GrabRounding = 3.0f;
        style.TabRounding = 4.0f;
    }

    /// <summary>
    /// Apply a temporary style override
    /// </summary>
    public static IDisposable PushStyle(ImGuiStyleVar styleVar, float value)
    {
        ImGui.PushStyleVar(styleVar, value);
        return new StyleGuard(() => ImGui.PopStyleVar());
    }

    /// <summary>
    /// Apply a temporary style override
    /// </summary>
    public static IDisposable PushStyle(ImGuiStyleVar styleVar, Vector2 value)
    {
        ImGui.PushStyleVar(styleVar, value);
        return new StyleGuard(() => ImGui.PopStyleVar());
    }

    /// <summary>
    /// Apply a temporary color override
    /// </summary>
    public static IDisposable PushColor(ImGuiCol colorId, Vector4 color)
    {
        ImGui.PushStyleColor(colorId, color);
        return new StyleGuard(() => ImGui.PopStyleColor());
    }

    private class StyleGuard : IDisposable
    {
        private readonly Action _dispose;
        
        public StyleGuard(Action dispose)
        {
            _dispose = dispose;
        }
        
        public void Dispose() => _dispose();
    }
}