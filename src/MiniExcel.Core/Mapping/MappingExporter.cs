namespace MiniExcelLib.Core.Mapping;

public partial class MappingExporter
{
    public MappingRegistry Registry { get; }
    
    public MappingExporter() 
    {
        Registry = new MappingRegistry();
    }

    public MappingExporter(MappingRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    [CreateSyncVersion]
    public async Task SaveAsAsync<T>(Stream? stream, IEnumerable<T>? values, CancellationToken cancellationToken = default)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (values is null)
            throw new ArgumentNullException(nameof(values));

        if (!Registry.HasMapping<T>())
            throw new InvalidOperationException($"No mapping configured for type {typeof(T).Name}. Call Configure<{typeof(T).Name}>() first.");

        var mapping = Registry.GetMapping<T>();
        await MappingWriter<T>.ExportAsync(stream, values, mapping, cancellationToken).ConfigureAwait(false);
    }
}