using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AlphaScope.GUI.Modern.Base;

/// <summary>
/// Base class for all modern GUI components in AlphaScope.
/// Provides common functionality, theming support, and lifecycle management.
/// </summary>
public abstract class BaseComponent : IDisposable
{
    protected bool _disposed = false;
    protected string _id;
    protected bool _visible = true;
    protected Vector2 _size = Vector2.Zero;
    protected Vector2 _position = Vector2.Zero;
    
    /// <summary>
    /// Unique identifier for this component instance
    /// </summary>
    public string Id => _id;
    
    /// <summary>
    /// Whether this component is currently visible
    /// </summary>
    public virtual bool Visible
    {
        get => _visible;
        set => _visible = value;
    }
    
    /// <summary>
    /// Size of the component
    /// </summary>
    public virtual Vector2 Size
    {
        get => _size;
        set => _size = value;
    }
    
    /// <summary>
    /// Position of the component
    /// </summary>
    public virtual Vector2 Position
    {
        get => _position;
        set => _position = value;
    }

    protected BaseComponent(string id)
    {
        _id = id ?? throw new ArgumentNullException(nameof(id));
    }

    /// <summary>
    /// Main render method - calls internal render with safety checks
    /// </summary>
    public void Render()
    {
        if (_disposed || !_visible) return;

        try
        {
            // Apply global scaling
            using var style = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, 
                ImGuiHelpers.ScaledVector2(4f, 3f));
            
            OnRender();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the entire UI
            Plugin.Log.Error($"Error rendering component {_id}: {ex}");
            ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Error in {_id}");
        }
    }

    /// <summary>
    /// Override this method to implement component-specific rendering
    /// </summary>
    protected abstract void OnRender();

    /// <summary>
    /// Called when component needs to update its state (before rendering)
    /// </summary>
    public virtual void Update()
    {
        if (_disposed) return;
        OnUpdate();
    }

    /// <summary>
    /// Override this method to implement component-specific updates
    /// </summary>
    protected virtual void OnUpdate() { }

    /// <summary>
    /// Helper method to create a styled button with global scaling
    /// </summary>
    protected bool StyledButton(string label, Vector2? size = null)
    {
        var buttonSize = size ?? ImGuiHelpers.ScaledVector2(100f, 25f);
        return ImGui.Button(label, buttonSize);
    }

    /// <summary>
    /// Helper method to create styled text with proper scaling
    /// </summary>
    protected void StyledText(string text, Vector4? color = null)
    {
        if (color.HasValue)
        {
            ImGui.TextColored(color.Value, text);
        }
        else
        {
            ImGui.Text(text);
        }
    }

    /// <summary>
    /// Helper method to create a separator with proper spacing
    /// </summary>
    protected void StyledSeparator()
    {
        ImGuiHelpers.ScaledDummy(5f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5f);
    }

    /// <summary>
    /// Helper method to show a "Coming Soon" placeholder
    /// </summary>
    protected void ShowComingSoon(string featureName)
    {
        var color = new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
        
        ImGuiHelpers.ScaledDummy(10f);
        
        // Center the content
        var windowWidth = ImGui.GetWindowWidth();
        var textWidth = ImGui.CalcTextSize($"🚧 {featureName}").X;
        ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
        
        ImGui.TextColored(color, $"🚧 {featureName}");
        
        var comingSoonText = "Coming Soon in Future Update";
        var comingSoonWidth = ImGui.CalcTextSize(comingSoonText).X;
        ImGui.SetCursorPosX((windowWidth - comingSoonWidth) * 0.5f);
        
        using var font = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.6f);
        ImGui.Text(comingSoonText);
        
        ImGuiHelpers.ScaledDummy(10f);
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        
        OnDispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override this method to implement component-specific disposal
    /// </summary>
    protected virtual void OnDispose() { }
}