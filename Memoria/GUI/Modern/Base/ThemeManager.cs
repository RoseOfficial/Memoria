using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;

namespace Memoria.GUI.Modern.Base;

/// <summary>
/// Manages themes and styling for the modern Memoria GUI.
/// Provides consistent colors, spacing, and styling across all components.
/// Styling is applied per-window via scoped Push/Pop calls inside the window's
/// PreDraw/PostDraw hooks; the shared Dalamud ImGui style is never mutated,
/// so Memoria's theme does not leak into other plugins.
/// </summary>
public static class ThemeManager
{
    public enum Theme
    {
        Dark,
        Light,
        Memoria // Custom Memoria theme
    }

    private static Theme _currentTheme = Theme.Memoria;
    private static Dictionary<string, Vector4> _customColors = [];

    /// <summary>
    /// Current active theme
    /// </summary>
    public static Theme CurrentTheme
    {
        get => _currentTheme;
        set => _currentTheme = value;
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
    /// Initialize the theme system. Populates the in-memory color dictionary used by
    /// Colors.X accessors and the per-window PushWindowStyle helper.
    /// Does NOT mutate the shared Dalamud ImGui style.
    /// </summary>
    public static void Initialize()
    {
        SetupThemes();
    }

    private static void SetupThemes()
    {
        // Memoria Theme (Custom)
        SetupMemoriaTheme();

        // Dark theme
        SetupDarkTheme();

        // Light theme
        SetupLightTheme();
    }

    private static void SetupMemoriaTheme()
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
            _customColors[$"Memoria_{color.Key}"] = color.Value;
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

    /// <summary>
    /// Pushes the current theme's window-chrome styling (colors + style vars) onto the
    /// ImGui stack. Scoped to the caller: dispose the returned handle in PostDraw to pop
    /// them all back off. Call in a Window's PreDraw override so only Memoria windows
    /// see the custom styling — other plugins are unaffected.
    /// </summary>
    public static IDisposable PushWindowStyle()
    {
        var colorCount = 0;
        var varCount = 0;

        void PushC(ImGuiCol id, Vector4 c)
        {
            ImGui.PushStyleColor(id, c);
            colorCount++;
        }

        void PushV(ImGuiStyleVar v, float f)
        {
            ImGui.PushStyleVar(v, f);
            varCount++;
        }

        // Window / surface backgrounds
        PushC(ImGuiCol.WindowBg, Colors.Background);
        PushC(ImGuiCol.ChildBg, Colors.Surface);
        PushC(ImGuiCol.PopupBg, Colors.Surface);
        PushC(ImGuiCol.Border, Colors.Border);
        PushC(ImGuiCol.BorderShadow, Vector4.Zero);

        PushC(ImGuiCol.FrameBg, Colors.Surface);
        PushC(ImGuiCol.FrameBgHovered, Colors.InteractiveHover * 0.4f);
        PushC(ImGuiCol.FrameBgActive, Colors.InteractiveActive * 0.6f);

        PushC(ImGuiCol.TitleBg, Colors.Surface);
        PushC(ImGuiCol.TitleBgActive, Colors.Primary * 0.8f);
        PushC(ImGuiCol.TitleBgCollapsed, Colors.Surface * 0.8f);

        PushC(ImGuiCol.MenuBarBg, Colors.Surface);

        PushC(ImGuiCol.ScrollbarBg, Colors.Background);
        PushC(ImGuiCol.ScrollbarGrab, Colors.Interactive * 0.6f);
        PushC(ImGuiCol.ScrollbarGrabHovered, Colors.InteractiveHover * 0.8f);
        PushC(ImGuiCol.ScrollbarGrabActive, Colors.InteractiveActive);

        PushC(ImGuiCol.CheckMark, Colors.Primary);
        PushC(ImGuiCol.SliderGrab, Colors.Primary);
        PushC(ImGuiCol.SliderGrabActive, Colors.PrimaryLight);

        PushC(ImGuiCol.Button, Colors.Interactive * 0.6f);
        PushC(ImGuiCol.ButtonHovered, Colors.InteractiveHover * 0.8f);
        PushC(ImGuiCol.ButtonActive, Colors.InteractiveActive);

        PushC(ImGuiCol.Header, Colors.Interactive * 0.4f);
        PushC(ImGuiCol.HeaderHovered, Colors.InteractiveHover * 0.6f);
        PushC(ImGuiCol.HeaderActive, Colors.InteractiveActive * 0.8f);

        PushC(ImGuiCol.Separator, Colors.Border);
        PushC(ImGuiCol.SeparatorHovered, Colors.InteractiveHover);
        PushC(ImGuiCol.SeparatorActive, Colors.InteractiveActive);

        PushC(ImGuiCol.ResizeGrip, Colors.Interactive * 0.4f);
        PushC(ImGuiCol.ResizeGripHovered, Colors.InteractiveHover * 0.6f);
        PushC(ImGuiCol.ResizeGripActive, Colors.InteractiveActive);

        PushC(ImGuiCol.Tab, Colors.Surface);
        PushC(ImGuiCol.TabHovered, Colors.InteractiveHover * 0.8f);
        PushC(ImGuiCol.TabActive, Colors.Interactive * 0.8f);
        PushC(ImGuiCol.TabUnfocused, Colors.Surface * 0.9f);
        PushC(ImGuiCol.TabUnfocusedActive, Colors.Interactive * 0.6f);

        PushC(ImGuiCol.DockingPreview, Colors.Primary * 0.7f);
        PushC(ImGuiCol.DockingEmptyBg, Colors.Background);

        PushC(ImGuiCol.PlotLines, Colors.Primary);
        PushC(ImGuiCol.PlotLinesHovered, Colors.PrimaryLight);
        PushC(ImGuiCol.PlotHistogram, Colors.Primary * 0.8f);
        PushC(ImGuiCol.PlotHistogramHovered, Colors.PrimaryLight);

        PushC(ImGuiCol.Text, Colors.TextPrimary);
        PushC(ImGuiCol.TextDisabled, Colors.TextMuted);
        PushC(ImGuiCol.TextSelectedBg, Colors.Primary * 0.3f);

        PushC(ImGuiCol.NavHighlight, Colors.Primary);
        PushC(ImGuiCol.NavWindowingHighlight, Colors.Primary * 0.8f);
        PushC(ImGuiCol.NavWindowingDimBg, Colors.Background * 0.2f);
        PushC(ImGuiCol.ModalWindowDimBg, Colors.Background * 0.6f);

        // Rounding / spacing
        PushV(ImGuiStyleVar.WindowRounding, 4.0f);
        PushV(ImGuiStyleVar.ChildRounding, 4.0f);
        PushV(ImGuiStyleVar.FrameRounding, 3.0f);
        PushV(ImGuiStyleVar.PopupRounding, 4.0f);
        PushV(ImGuiStyleVar.ScrollbarRounding, 9.0f);
        PushV(ImGuiStyleVar.GrabRounding, 3.0f);
        PushV(ImGuiStyleVar.TabRounding, 4.0f);

        return new StyleGuard(() =>
        {
            if (colorCount > 0) ImGui.PopStyleColor(colorCount);
            if (varCount > 0) ImGui.PopStyleVar(varCount);
        });
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
