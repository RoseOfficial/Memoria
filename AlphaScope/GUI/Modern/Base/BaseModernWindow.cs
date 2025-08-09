using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace AlphaScope.GUI.Modern.Base;

/// <summary>
/// Modern base window class that provides enhanced functionality over standard Dalamud windows.
/// Includes component management, theming, accessibility features, and modern UI patterns.
/// </summary>
public abstract class BaseModernWindow : Window
{
    protected readonly List<BaseComponent> _components = [];
    protected bool _isInitialized = false;
    protected DateTime _lastUpdate = DateTime.UtcNow;
    
    /// <summary>
    /// Whether to show the window title bar
    /// </summary>
    protected virtual bool ShowTitleBar => true;
    
    /// <summary>
    /// Whether to enable docking for this window
    /// </summary>
    protected virtual bool EnableDocking => false;
    
    /// <summary>
    /// Window background alpha (for transparency)
    /// </summary>
    protected virtual float BackgroundAlpha => 1.0f;

    protected BaseModernWindow(string windowId, string title = "") 
        : base(windowId, ImGuiWindowFlags.None)
    {
        WindowName = $"{title}###_{windowId}";
        
        // Set up default window properties
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        // Configure window flags
        var flags = ImGuiWindowFlags.None;
        if (!ShowTitleBar)
            flags |= ImGuiWindowFlags.NoTitleBar;
        if (EnableDocking)
            flags |= ImGuiWindowFlags.NoDocking;
        
        Flags = flags;
    }

    /// <summary>
    /// Adds a component to this window
    /// </summary>
    public void AddComponent(BaseComponent component)
    {
        if (component == null) throw new ArgumentNullException(nameof(component));
        _components.Add(component);
    }

    /// <summary>
    /// Removes a component from this window
    /// </summary>
    public void RemoveComponent(BaseComponent component)
    {
        _components.Remove(component);
    }

    /// <summary>
    /// Gets a component by its ID
    /// </summary>
    public T? GetComponent<T>(string id) where T : BaseComponent
    {
        return _components.Find(c => c.Id == id) as T;
    }

    /// <summary>
    /// Main draw method with modern enhancements
    /// </summary>
    public override void Draw()
    {
        try
        {
            // Initialize if needed
            if (!_isInitialized)
            {
                OnInitialize();
                _isInitialized = true;
            }

            // Update components
            var now = DateTime.UtcNow;
            if ((now - _lastUpdate).TotalMilliseconds > 16) // ~60 FPS
            {
                OnUpdate();
                foreach (var component in _components)
                {
                    component.Update();
                }
                _lastUpdate = now;
            }

            // Apply window styling
            using var bgAlpha = ImRaii.PushStyle(ImGuiStyleVar.Alpha, BackgroundAlpha);
            using var windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, 
                ImGuiHelpers.ScaledVector2(8f, 8f));

            // Enable docking if specified
            if (EnableDocking)
            {
                var dockspaceId = ImGui.GetID("DockSpace");
                ImGui.DockSpace(dockspaceId, Vector2.Zero, ImGuiDockNodeFlags.None);
            }

            OnDraw();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error in window {WindowName}: {ex}");
            ShowError($"An error occurred in {WindowName}");
        }
    }

    /// <summary>
    /// Override this method to implement window-specific drawing
    /// </summary>
    protected virtual void OnDraw()
    {
        // Render all components
        foreach (var component in _components)
        {
            component.Render();
        }
    }

    /// <summary>
    /// Called once when the window is first drawn
    /// </summary>
    protected virtual void OnInitialize() { }

    /// <summary>
    /// Called every frame before drawing
    /// </summary>
    protected virtual void OnUpdate() { }

    /// <summary>
    /// Shows an error message in the window
    /// </summary>
    protected void ShowError(string message)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
        ImGui.TextWrapped(message);
        ImGui.PopStyleColor();
    }

    /// <summary>
    /// Shows a loading indicator
    /// </summary>
    protected void ShowLoading(string message = "Loading...")
    {
        var windowSize = ImGui.GetWindowSize();
        var textSize = ImGui.CalcTextSize(message);
        
        // Center the loading text
        ImGui.SetCursorPos(new Vector2(
            (windowSize.X - textSize.X) * 0.5f,
            (windowSize.Y - textSize.Y) * 0.5f
        ));
        
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), message);
        
        // Add a simple spinner
        ImGui.SameLine();
        var time = (float)ImGui.GetTime();
        var spinner = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";
        var index = (int)(time * 8) % spinner.Length;
        ImGui.Text($"{spinner[index]}");
    }

    /// <summary>
    /// Creates a modern header section
    /// </summary>
    protected void DrawHeader(string title, string? subtitle = null)
    {
        using var headerFont = ImRaii.PushFont(UiBuilder.MonoFont);
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), title);
        
        if (!string.IsNullOrEmpty(subtitle))
        {
            using var subtitleFont = ImRaii.PushFont(UiBuilder.DefaultFont);
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), subtitle);
        }
        
        ImGuiHelpers.ScaledDummy(5f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5f);
    }

    /// <summary>
    /// Creates a collapsible section
    /// </summary>
    protected bool DrawCollapsibleSection(string header, bool defaultOpen = true)
    {
        ImGui.SetNextItemOpen(defaultOpen, ImGuiCond.FirstUseEver);
        return ImGui.CollapsingHeader(header);
    }

    /// <summary>
    /// Called when the window is being closed
    /// </summary>
    protected virtual void OnWindowClose() { }

    public override void OnClose()
    {
        base.OnClose();
        OnWindowClose();
        
        // Dispose components
        foreach (var component in _components)
        {
            component.Dispose();
        }
        _components.Clear();
    }
}