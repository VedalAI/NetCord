namespace NetCord.Rest;

internal interface IPaginationProperties<T, TSelf> where T : struct where TSelf : IPaginationProperties<T, TSelf>
{
    /// <summary>
    /// The starting point for pagination.
    /// </summary>
    public T? From { get; set; }

    /// <summary>
    /// The direction of pagination.
    /// </summary>
    public PaginationDirection? Direction { get; set; }

    /// <summary>
    /// The maximum number of items to retrieve in a single request.
    /// </summary>
    public int? BatchSize { get; set; }
}

public static class PaginationPropertiesStatic
{
    private static readonly Dictionary<Type, Func<object>> _constructors = [];
    private static readonly Dictionary<Type, Func<object, object>> _constructorsWithProperties = [];

    private static readonly Dictionary<Type, string> _genericConstructors = [];
    private static readonly Dictionary<Type, string> _genericConstructorsWithProperties = [];
    
    public static T Create<T>()
    {
        if (_constructors.TryGetValue(typeof(T), out var constructor))
        {
            return (T) constructor();
        }
        
        if (_genericConstructors.TryGetValue(typeof(T).GetGenericTypeDefinition(), out var methodName))
        {
            var method = typeof(T).GetMethod(methodName, [])!;
            return (T) method.Invoke(null, null)!;
        }
        
        throw new InvalidOperationException($"No constructor registered for type {typeof(T)}.");
    }
    
    public static T Create<T>(T properties)
    {
        if (_constructorsWithProperties.TryGetValue(typeof(T), out var constructor))
        {
            return (T) constructor(properties!)!;
        }
        
        if (_genericConstructorsWithProperties.TryGetValue(typeof(T).GetGenericTypeDefinition(), out var methodName))
        {
            var method = typeof(T).GetMethod(methodName, [typeof(T)])!;
            return (T) method.Invoke(null, [properties])!;
        }
        
        throw new InvalidOperationException($"No constructor registered for type {typeof(T)}.");
    }

    public static void Register<T>(Func<T> constructor, Func<T, T> constructorWithProperties)
    {
        _constructors[typeof(T)] = () => constructor()!;
        _constructorsWithProperties[typeof(T)] = (obj) => constructorWithProperties((T)obj!)!;
    }
    
    public static void RegisterGeneric(Type genericType, string createMethodName, string createWithPropertiesMethodName)
    {
        _genericConstructors[genericType] = createMethodName;
        _genericConstructorsWithProperties[genericType] = createWithPropertiesMethodName;
    }
}
