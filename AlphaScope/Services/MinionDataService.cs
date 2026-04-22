using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;

namespace AlphaScope.Services;

/// <summary>
/// Service that provides comprehensive minion name-to-ID mapping using Dalamud's Lumina integration.
/// Builds a complete mapping of all minions from the Companion sheet at initialization for fast O(1) lookups.
/// Handles case-insensitive matching and provides fallback mechanisms for edge cases.
/// </summary>
public sealed class MinionDataService : IDisposable
{
    private readonly ILogger<MinionDataService> _logger;
    private readonly IDataManager _dataManager;
    
    /// <summary>
    /// Dictionary mapping minion names to their IDs, case-insensitive
    /// </summary>
    private readonly Dictionary<string, uint> _nameToIdMapping;
    
    /// <summary>
    /// Dictionary mapping minion IDs to their names for reverse lookups
    /// </summary>
    private readonly Dictionary<uint, string> _idToNameMapping;
    
    /// <summary>
    /// Dictionary mapping minion IDs to their icon IDs for direct icon access
    /// </summary>
    private readonly Dictionary<uint, uint> _idToIconMapping;
    
    /// <summary>
    /// Total number of minions successfully mapped
    /// </summary>
    public int MinionCount => _nameToIdMapping.Count;

    /// <summary>
    /// Initializes the MinionDataService and builds the comprehensive minion mapping
    /// </summary>
    /// <param name="logger">Logger for diagnostics and debugging</param>
    /// <param name="dataManager">Dalamud data manager for accessing Lumina data</param>
    public MinionDataService(ILogger<MinionDataService> logger, IDataManager dataManager)
    {
        _logger = logger;
        _dataManager = dataManager;
        _nameToIdMapping = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        _idToNameMapping = new Dictionary<uint, string>();
        _idToIconMapping = new Dictionary<uint, uint>();
        
        BuildMinionMapping();
    }
    
    /// <summary>
    /// Builds the comprehensive minion name-to-ID mapping from Lumina Companion sheet
    /// </summary>
    private void BuildMinionMapping()
    {
        try
        {
            _logger.LogInformation("Building comprehensive minion mapping from Lumina data...");
            
            var companionSheet = _dataManager.GetExcelSheet<Companion>();
            if (companionSheet == null)
            {
                _logger.LogError("Failed to load Companion sheet from Lumina - minion mapping will be empty");
                return;
            }
            
            var processedCount = 0;
            var duplicateCount = 0;
            var invalidCount = 0;
            
            foreach (var companion in companionSheet)
            {
                try
                {
                    // Skip companions without valid data
                    if (companion.RowId == 0)
                    {
                        continue;
                    }
                    
                    // Get the minion name
                    var name = companion.Singular.ExtractText();
                    
                    // Skip entries with empty or null names
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        invalidCount++;
                        continue;
                    }
                    
                    // Skip placeholder or invalid entries (common in game data)
                    if (name.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Unknown", StringComparison.OrdinalIgnoreCase) ||
                        name.Length < 2)
                    {
                        invalidCount++;
                        continue;
                    }
                    
                    var minionId = companion.RowId;
                    
                    // Handle potential duplicate names by using the first occurrence
                    if (_nameToIdMapping.ContainsKey(name))
                    {
                        duplicateCount++;
                        _logger.LogDebug("Duplicate minion name found: '{Name}' (existing ID: {ExistingId}, new ID: {NewId}) - keeping first occurrence", 
                            name, _nameToIdMapping[name], minionId);
                        continue;
                    }
                    
                    // Get the icon ID from the Companion sheet
                    var iconId = companion.Icon;
                    
                    // Add to all mappings
                    _nameToIdMapping[name] = minionId;
                    _idToNameMapping[minionId] = name;
                    _idToIconMapping[minionId] = iconId;
                    processedCount++;
                    
                    // Log a few examples for verification during development
                    if (processedCount <= 10)
                    {
                        _logger.LogDebug("Mapped minion: '{Name}' -> ID {Id}", name, minionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing companion entry with RowId {RowId}", companion.RowId);
                    invalidCount++;
                }
            }
            
            _logger.LogInformation("Minion mapping built successfully: {ProcessedCount} minions mapped, {DuplicateCount} duplicates skipped, {InvalidCount} invalid entries skipped",
                processedCount, duplicateCount, invalidCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error building minion mapping from Lumina data");
        }
    }
    
    /// <summary>
    /// Gets the minion ID for a given minion name using case-insensitive matching
    /// </summary>
    /// <param name="minionName">The name of the minion to look up</param>
    /// <returns>The minion ID if found, null if not found</returns>
    public uint? GetMinionId(string minionName)
    {
        if (string.IsNullOrWhiteSpace(minionName))
        {
            return null;
        }
        
        return _nameToIdMapping.TryGetValue(minionName.Trim(), out var id) ? id : null;
    }
    
    /// <summary>
    /// Gets the minion name for a given minion ID
    /// </summary>
    /// <param name="minionId">The ID of the minion to look up</param>
    /// <returns>The minion name if found, null if not found</returns>
    public string? GetMinionName(uint minionId)
    {
        return _idToNameMapping.TryGetValue(minionId, out var name) ? name : null;
    }
    
    /// <summary>
    /// Gets the icon ID for a given minion ID
    /// </summary>
    /// <param name="minionId">The ID of the minion to look up</param>
    /// <returns>The icon ID if found, null if not found</returns>
    public uint? GetMinionIconId(uint minionId)
    {
        return _idToIconMapping.TryGetValue(minionId, out var iconId) ? iconId : null;
    }
    
    /// <summary>
    /// Checks if a minion name exists in the mapping
    /// </summary>
    /// <param name="minionName">The name of the minion to check</param>
    /// <returns>True if the minion exists, false otherwise</returns>
    public bool ContainsMinion(string minionName)
    {
        if (string.IsNullOrWhiteSpace(minionName))
        {
            return false;
        }
        
        return _nameToIdMapping.ContainsKey(minionName.Trim());
    }
    
    /// <summary>
    /// Gets all mapped minion names for diagnostic purposes
    /// </summary>
    /// <returns>Collection of all minion names in the mapping</returns>
    public IReadOnlyCollection<string> GetAllMinionNames()
    {
        return _nameToIdMapping.Keys.ToList().AsReadOnly();
    }
    
    /// <summary>
    /// Gets mapping statistics for monitoring and debugging
    /// </summary>
    /// <returns>Statistics about the minion mapping</returns>
    public MinionMappingStats GetStats()
    {
        return new MinionMappingStats
        {
            TotalMinions = _nameToIdMapping.Count,
            MinMinionId = _idToNameMapping.Count > 0 ? _idToNameMapping.Keys.Min() : 0,
            MaxMinionId = _idToNameMapping.Count > 0 ? _idToNameMapping.Keys.Max() : 0,
            SampleMappings = _nameToIdMapping.Take(5).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }
    
    /// <summary>
    /// Attempts to find minion names that partially match the given search term
    /// </summary>
    /// <param name="searchTerm">The term to search for in minion names</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>Collection of minion names that contain the search term</returns>
    public IReadOnlyCollection<string> SearchMinions(string searchTerm, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<string>();
        }
        
        var normalizedSearch = searchTerm.Trim();
        
        return _nameToIdMapping.Keys
            .Where(name => name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList()
            .AsReadOnly();
    }
    
    public void Dispose()
    {
        // No resources to dispose, but implement IDisposable for consistency
        _logger.LogDebug("MinionDataService disposed");
    }
}

/// <summary>
/// Statistics about the minion mapping for monitoring and debugging
/// </summary>
public class MinionMappingStats
{
    public int TotalMinions { get; set; }
    public uint MinMinionId { get; set; }
    public uint MaxMinionId { get; set; }
    public Dictionary<string, uint> SampleMappings { get; set; } = new();
}