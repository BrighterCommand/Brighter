#region Licence

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace System.Reflection;

internal static class MethodInfoExtensions
{
#if NETSTANDARD2_0
    /// <summary>
    /// Helper for .NET Standard 2.0 to provide a generic form of Create Delegate
    /// </summary>
    /// <param name="method">The <see cref="MethodInfo"/> we wish to create a delegate for</param>
    /// <typeparam name="T">The type to create the delegate from</typeparam>
    /// <returns></returns>
    public static T CreateDelegate<T>(this MethodInfo method)
        where T : Delegate 
        => (T)method.CreateDelegate(typeof(T));
#endif
}
