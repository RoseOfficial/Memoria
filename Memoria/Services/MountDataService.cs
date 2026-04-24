using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;

namespace Memoria.Services;

/// <summary>
/// Service that provides comprehensive mount name-to-ID mapping using Dalamud's Lumina integration.
/// Builds a complete mapping of all mounts from the Mount sheet at initialization for fast O(1) lookups.
/// Handles case-insensitive matching and provides fallback mechanisms for edge cases.
/// </summary>
public sealed class MountDataService : IDisposable
{
    private readonly ILogger<MountDataService> _logger;
    private readonly IDataManager _dataManager;
    
    /// <summary>
    /// Dictionary mapping mount names to their IDs, case-insensitive
    /// </summary>
    private readonly Dictionary<string, uint> _nameToIdMapping;
    
    /// <summary>
    /// Dictionary mapping mount IDs to their names for reverse lookups
    /// </summary>
    private readonly Dictionary<uint, string> _idToNameMapping;
    
    /// <summary>
    /// Dictionary mapping mount IDs to their icon IDs for direct icon access
    /// </summary>
    private readonly Dictionary<uint, uint> _idToIconMapping;
    
    /// <summary>
    /// Total number of mounts successfully mapped
    /// </summary>
    public int MountCount => _nameToIdMapping.Count;

    /// <summary>
    /// Initializes the MountDataService and builds the comprehensive mount mapping
    /// </summary>
    /// <param name="logger">Logger for diagnostics and debugging</param>
    /// <param name="dataManager">Dalamud data manager for accessing Lumina data</param>
    public MountDataService(ILogger<MountDataService> logger, IDataManager dataManager)
    {
        _logger = logger;
        _dataManager = dataManager;
        _nameToIdMapping = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        _idToNameMapping = new Dictionary<uint, string>();
        _idToIconMapping = new Dictionary<uint, uint>();
        
        BuildMountMapping();
    }
    
    /// <summary>
    /// Builds the comprehensive mount name-to-ID mapping from Lumina Mount sheet
    /// </summary>
    private void BuildMountMapping()
    {
        try
        {
            _logger.LogInformation("Building comprehensive mount mapping from Lumina data...");
            
            var mountSheet = _dataManager.GetExcelSheet<Mount>();
            if (mountSheet == null)
            {
                _logger.LogError("Failed to load Mount sheet from Lumina - mount mapping will be empty");
                return;
            }
            
            var processedCount = 0;
            var duplicateCount = 0;
            var invalidCount = 0;
            
            foreach (var mount in mountSheet)
            {
                try
                {
                    // Skip mounts without valid data
                    if (mount.RowId == 0)
                    {
                        continue;
                    }
                    
                    // Get the mount name
                    var name = mount.Singular.ExtractText();
                    
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
                    
                    var mountId = mount.RowId;
                    
                    // Handle potential duplicate names by using the first occurrence
                    if (_nameToIdMapping.ContainsKey(name))
                    {
                        duplicateCount++;
                        _logger.LogDebug("Duplicate mount name found: '{Name}' (existing ID: {ExistingId}, new ID: {NewId}) - keeping first occurrence", 
                            name, _nameToIdMapping[name], mountId);
                        continue;
                    }
                    
                    // Get the icon ID from the Mount sheet
                    var iconId = mount.Icon;
                    
                    // Add to all mappings
                    _nameToIdMapping[name] = mountId;
                    _idToNameMapping[mountId] = name;
                    _idToIconMapping[mountId] = iconId;
                    processedCount++;
                    
                    // Log a few examples for verification during development
                    if (processedCount <= 10)
                    {
                        _logger.LogDebug("Mapped mount: '{Name}' -> ID {Id}", name, mountId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing mount entry with RowId {RowId}", mount.RowId);
                    invalidCount++;
                }
            }
            
            _logger.LogInformation("Mount mapping built successfully: {ProcessedCount} mounts mapped, {DuplicateCount} duplicates skipped, {InvalidCount} invalid entries skipped",
                processedCount, duplicateCount, invalidCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error building mount mapping from Lumina data");
        }
    }
    
    /// <summary>
    /// Gets the mount ID for a given mount name using case-insensitive matching
    /// </summary>
    /// <param name="mountName">The name of the mount to look up</param>
    /// <returns>The mount ID if found, null if not found</returns>
    public uint? GetMountId(string mountName)
    {
        if (string.IsNullOrWhiteSpace(mountName))
        {
            return null;
        }
        
        return _nameToIdMapping.TryGetValue(mountName.Trim(), out var id) ? id : null;
    }
    
    /// <summary>
    /// Gets the mount name for a given mount ID
    /// </summary>
    /// <param name="mountId">The ID of the mount to look up</param>
    /// <returns>The mount name if found, null if not found</returns>
    public string? GetMountName(uint mountId)
    {
        return _idToNameMapping.TryGetValue(mountId, out var name) ? name : null;
    }
    
    /// <summary>
    /// Gets the icon ID for a given mount ID
    /// </summary>
    /// <param name="mountId">The ID of the mount to look up</param>
    /// <returns>The icon ID if found, null if not found</returns>
    public uint? GetMountIconId(uint mountId)
    {
        return _idToIconMapping.TryGetValue(mountId, out var iconId) ? iconId : null;
    }
    
    /// <summary>
    /// Checks if a mount name exists in the mapping
    /// </summary>
    /// <param name="mountName">The name of the mount to check</param>
    /// <returns>True if the mount exists, false otherwise</returns>
    public bool ContainsMount(string mountName)
    {
        if (string.IsNullOrWhiteSpace(mountName))
        {
            return false;
        }
        
        return _nameToIdMapping.ContainsKey(mountName.Trim());
    }
    
    /// <summary>
    /// Gets all mapped mount names for diagnostic purposes
    /// </summary>
    /// <returns>Collection of all mount names in the mapping</returns>
    public IReadOnlyCollection<string> GetAllMountNames()
    {
        return _nameToIdMapping.Keys.ToList().AsReadOnly();
    }
    
    /// <summary>
    /// Gets mapping statistics for monitoring and debugging
    /// </summary>
    /// <returns>Statistics about the mount mapping</returns>
    public MountMappingStats GetStats()
    {
        return new MountMappingStats
        {
            TotalMounts = _nameToIdMapping.Count,
            MinMountId = _idToNameMapping.Count > 0 ? _idToNameMapping.Keys.Min() : 0,
            MaxMountId = _idToNameMapping.Count > 0 ? _idToNameMapping.Keys.Max() : 0,
            SampleMappings = _nameToIdMapping.Take(5).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }
    
    /// <summary>
    /// Attempts to find mount names that partially match the given search term
    /// </summary>
    /// <param name="searchTerm">The term to search for in mount names</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <returns>Collection of mount names that contain the search term</returns>
    public IReadOnlyCollection<string> SearchMounts(string searchTerm, int maxResults = 10)
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
        _logger.LogDebug("MountDataService disposed");
    }
}

/// <summary>
/// Statistics about the mount mapping for monitoring and debugging
/// </summary>
public class MountMappingStats
{
    public int TotalMounts { get; set; }
    public uint MinMountId { get; set; }
    public uint MaxMountId { get; set; }
    public Dictionary<string, uint> SampleMappings { get; set; } = new();
}