namespace System.Reflection;

internal static class MethodInfoExtensions
{
#if NETSTANDARD2_0
    public static T CreateDelegate<T>(this MethodInfo method)
        where T : Delegate 
        => (T)method.CreateDelegate(typeof(T));
#endif
}
