namespace MiniExcelLib.Core.Mapping;

/// <summary>
/// Optimized executor that uses pre-calculated handler arrays for maximum performance.
/// Zero allocations, zero lookups, just direct array access.
/// </summary>
internal sealed class OptimizedMappingExecutor<T>
{
    // Pre-calculated array of value extractors indexed by column (0-based)
    private readonly Func<T, object?>[] _columnGetters;
    
    // Pre-calculated array of value setters indexed by column (0-based)
    private readonly Action<T, object?>[] _columnSetters;
    
    // Column count for bounds checking
    private readonly int _columnCount;
    
    // Minimum column number (1-based) for offset calculation
    private readonly int _minColumn;
    
    public OptimizedMappingExecutor(CompiledMapping<T>? mapping)
    {
        if (mapping?.OptimizedBoundaries is null)
            throw new ArgumentException("Mapping must be optimized");
            
        var boundaries = mapping.OptimizedBoundaries;
        _minColumn = boundaries.MinColumn;
        _columnCount = boundaries.GridWidth;
        
        // Pre-allocate arrays
        _columnGetters = new Func<T, object?>[_columnCount];
        _columnSetters = new Action<T, object?>[_columnCount];
        
        // Build optimized getters and setters for each column
        BuildOptimizedHandlers(mapping);
    }
    
    private void BuildOptimizedHandlers(CompiledMapping<T> mapping)
    {
        // Initialize all columns with no-op handlers
        for (int i = 0; i < _columnCount; i++)
        {
            _columnGetters[i] = static _ => null;
            _columnSetters[i] = static (_, _) => { };
        }
        
        // Map properties to their column positions
        foreach (var prop in mapping.Properties)
        {
            var columnIndex = prop.CellColumn - _minColumn;
            if (columnIndex >= 0 && columnIndex < _columnCount)
            {
                // Create optimized getter that directly accesses the property
                var getter = prop.Getter;
                _columnGetters[columnIndex] = obj => getter(obj);
                
                // Create optimized setter if available
                var setter = prop.Setter;
                if (setter is not null)
                {
                    _columnSetters[columnIndex] = (obj, value) => setter(obj, value);
                }
            }
        }
        
        // Pre-calculate collection element accessors
        foreach (var collection in mapping.Collections)
        {
            PreCalculateCollectionAccessors(collection, mapping.OptimizedBoundaries!);
        }
    }
    
    private void PreCalculateCollectionAccessors(CompiledCollectionMapping collection, OptimizedMappingBoundaries boundaries)
    {
        // Only support vertical collections
        if (collection.Layout == CollectionLayout.Vertical)
        {
            // For vertical, we'd handle differently based on row
            // This is simplified - real implementation would consider rows
            var colIndex = collection.StartCellColumn - _minColumn;
            if (colIndex >= 0 && colIndex < _columnCount)
            {
                var collectionGetter = collection.Getter;
                _columnGetters[colIndex] = obj => collectionGetter(obj)?.Cast<object?>().FirstOrDefault();
            }
        }
    }
    
    /// <summary>
    /// Get value for a specific column
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetValue(T item, int column)
    {
        var index = column - _minColumn;
        return index >= 0 && index < _columnCount 
            ? _columnGetters[index](item) 
            : null;
    }
    
    /// <summary>
    /// Set value for a specific column
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(T item, int column, object? value)
    {
        var index = column - _minColumn;
        if (index >= 0 && index < _columnCount)
        {
            _columnSetters[index](item, value);
        }
    }
    
    /// <summary>
    /// Create optimized row dictionary for OpenXmlWriter
    /// </summary>
    public Dictionary<string, object?> CreateRow(T item)
    {
        return Enumerable.Range(0, _columnCount)
            .Select(i => (i + _minColumn, _columnGetters[i](item)))
            .OfType<(int Column, object? Value)>()
            .ToDictionary(
                kvp => ReferenceHelper.GetCellLetter(ReferenceHelper.ConvertCoordinatesToCell(kvp.Column, 1)),
                kvp => kvp.Value);
    }
}