namespace System;

internal static class TypeExtensions
{
#if NETSTANDARD2_0
    public static bool IsAssignableTo(this Type source, Type targetType)
        => targetType.IsAssignableFrom(source);
#endif
}
