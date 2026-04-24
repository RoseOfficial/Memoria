using System.Threading.Tasks;

namespace Memoria.Services;

/// <summary>
/// Interface for getting acquisition methods for collectibles (mounts and minions)
/// </summary>
public interface ICollectiblesAcquisitionService
{
    /// <summary>
    /// Gets the acquisition method for a mount by name
    /// </summary>
    /// <param name="mountName">The name of the mount</param>
    /// <returns>The acquisition method (e.g., "Quest Reward", "Trial Drop", etc.)</returns>
    Task<string> GetMountAcquisitionMethodAsync(string? mountName);
    
    /// <summary>
    /// Gets the acquisition method for a minion by name
    /// </summary>
    /// <param name="minionName">The name of the minion</param>
    /// <returns>The acquisition method (e.g., "Achievement", "Dungeon Drop", etc.)</returns>
    Task<string> GetMinionAcquisitionMethodAsync(string? minionName);
    
    /// <summary>
    /// Forces a refresh of cached acquisition data
    /// </summary>
    Task RefreshCacheAsync();
    
    /// <summary>
    /// Gets the status of the external API
    /// </summary>
    /// <returns>Tuple indicating if API is available and status message</returns>
    Task<(bool IsAvailable, string Status)> GetApiStatusAsync();
}