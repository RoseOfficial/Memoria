namespace Memoria.API.Query.Base
{
    /// <summary>
    /// Base class for all API query objects providing common pagination and filtering functionality
    /// </summary>
    public abstract class QueryBase
    {
        /// <summary>
        /// Cursor for pagination (0 for first page)
        /// </summary>
        public int Cursor { get; set; } = 0;
        
        /// <summary>
        /// Indicates if data is currently being fetched
        /// </summary>
        public bool? IsFetching { get; set; }
    }
}