using System.Collections.Generic;
using AlphaScope.API.Query.Base;

namespace AlphaScope.API.Query.Player
{
    /// <summary>
    /// Query object for searching players with various filters and pagination
    /// </summary>
    public class PlayerQueryObject : QueryBase
    {
        /// <summary>
        /// Player's Local Content ID for exact match
        /// </summary>
        public long? LocalContentId { get; set; } = null;
        
        /// <summary>
        /// Player name for search (supports partial matching based on F_MatchAnyPartOfName)
        /// </summary>
        public string? Name { get; set; } = null;
        
        /// <summary>
        /// List of World IDs to filter by specific servers
        /// </summary>
        public List<short>? F_WorldIds { get; set; } = new List<short>();
        
        /// <summary>
        /// Whether to match any part of the name or exact match only
        /// </summary>
        public bool? F_MatchAnyPartOfName { get; set; } = false;
    }
}
