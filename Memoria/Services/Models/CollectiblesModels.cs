using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Memoria.Services.Models;

/// <summary>
/// Response model for FFXIVCollect API mount data
/// </summary>
public class FFXIVCollectMount
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("enhanced_description")]
    public string? EnhancedDescription { get; set; }
    
    [JsonPropertyName("tooltip")]
    public string? Tooltip { get; set; }
    
    [JsonPropertyName("movement")]
    public string? Movement { get; set; }
    
    [JsonPropertyName("seats")]
    public int? Seats { get; set; }
    
    [JsonPropertyName("order")]
    public int? Order { get; set; }
    
    [JsonPropertyName("order_group")]
    public int? OrderGroup { get; set; }
    
    [JsonPropertyName("owned")]
    public string? Owned { get; set; }
    
    [JsonPropertyName("patch")]
    public string? Patch { get; set; }
    
    [JsonPropertyName("item_id")]
    public int? ItemId { get; set; }
    
    [JsonPropertyName("tradeable")]
    public bool Tradeable { get; set; }
    
    [JsonPropertyName("sources")]
    public List<CollectibleSource> Sources { get; set; } = new();
    
    [JsonPropertyName("image")]
    public string? Image { get; set; }
    
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

/// <summary>
/// Response model for FFXIVCollect API minion data
/// </summary>
public class FFXIVCollectMinion
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("enhanced_description")]
    public string? EnhancedDescription { get; set; }
    
    [JsonPropertyName("tooltip")]
    public string? Tooltip { get; set; }
    
    [JsonPropertyName("behavior")]
    public MinionBehavior? Behavior { get; set; }
    
    [JsonPropertyName("race")]
    public MinionRace? Race { get; set; }
    
    [JsonPropertyName("summoning")]
    public string? Summoning { get; set; }
    
    [JsonPropertyName("order")]
    public int? Order { get; set; }
    
    [JsonPropertyName("order_group")]
    public int? OrderGroup { get; set; }
    
    [JsonPropertyName("owned")]
    public string? Owned { get; set; }
    
    [JsonPropertyName("patch")]
    public string? Patch { get; set; }
    
    [JsonPropertyName("item_id")]
    public int? ItemId { get; set; }
    
    [JsonPropertyName("tradeable")]
    public bool Tradeable { get; set; }
    
    [JsonPropertyName("sources")]
    public List<CollectibleSource> Sources { get; set; } = new();
    
    [JsonPropertyName("image")]
    public string? Image { get; set; }
    
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

/// <summary>
/// Minion behavior information from FFXIVCollect API
/// </summary>
public class MinionBehavior
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Minion race information from FFXIVCollect API
/// </summary>
public class MinionRace
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Source information for how to obtain a collectible
/// </summary>
public class CollectibleSource
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
    
    [JsonPropertyName("related_type")]
    public string? RelatedType { get; set; }
    
    [JsonPropertyName("related_id")]
    public int? RelatedId { get; set; }
}

/// <summary>
/// Response wrapper for FFXIVCollect API results
/// </summary>
public class FFXIVCollectResponse<T>
{
    [JsonPropertyName("results")]
    public List<T> Results { get; set; } = new();
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("next")]
    public string? Next { get; set; }
    
    [JsonPropertyName("previous")]
    public string? Previous { get; set; }
}

/// <summary>
/// Cached acquisition data for internal use
/// </summary>
public class CachedAcquisitionData
{
    public Dictionary<string, string> MountAcquisitions { get; set; } = new();
    public Dictionary<string, string> MinionAcquisitions { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public bool IsFromApi { get; set; }
}