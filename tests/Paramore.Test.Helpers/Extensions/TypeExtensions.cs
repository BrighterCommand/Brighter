using System;
using System.Collections.Generic;
using System.Reflection;

namespace Paramore.Test.Helpers.Extensions
{
    public static class TypeExtensions
    {
        /// <summary>
        /// The built in type names. Extensions to System Type class.
        /// </summary>
        private static readonly Dictionary<Type, string> BuiltInTypeNames = new()
        {
            { typeof(bool), "bool" },
            { typeof(byte), "byte" },
            { typeof(char), "char" },
            { typeof(decimal), "decimal" },
            { typeof(double), "double" },
            { typeof(float), "float" },
            { typeof(int), "int" },
            { typeof(long), "long" },
            { typeof(object), "object" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(string), "string" },
            { typeof(uint), "uint" },
            { typeof(ulong), "ulong" },
            { typeof(ushort), "ushort" },
        };

        /// <summary>
        /// Gets the display name of the full.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>System.String.</returns>
        public static string GetDisplayFullName(this Type? type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (type.GetTypeInfo().IsGenericType)
            {
                string? genericFullName = type.GetGenericTypeDefinition().FullName;

                // Nested types (public or private) have a '+' in their full name
                string[]? parts = genericFullName?.Split('+');

                parts ??= [];

                // Handle nested generic types
                // Examples:
                // ConsoleApp.Program+Foo`1+Bar
                // ConsoleApp.Program+Foo`1+Bar`1
                for (int i = 0; i < parts.Length; i++)
                {
                    string partName = parts[i];

                    int backTickIndex = partName.IndexOf('`');
                    if (backTickIndex >= 0)
                    {
                        // Since '.' is typically used to filter log messages in a hierarchy kind of scenario,
                        // do not include any generic type information as part of the name.
                        // Example:
                        // Microsoft.AspNetCore.Mvc -> log level set as Warning
                        // Microsoft.AspNetCore.Mvc.ModelBinding -> log level set as Verbose
                        partName = partName.Substring(0, backTickIndex);
                    }

                    parts[i] = partName;
                }

                return string.Join(".", parts);
            }

            if (BuiltInTypeNames.TryGetValue(type, out var displayFullName))
            {
                return displayFullName;
            }

            string? fullName = type.FullName;

            if (type.IsNested)
            {
                fullName = fullName?.Replace('+', '.') ?? string.Empty;
            }

            return fullName ?? string.Empty;
        }

        /// <summary>
        /// Gets the logger category name for the specified type.
        /// </summary>
        /// <param name="type">The type for which to get the logger category name.</param>
        /// <returns>The logger category name as a <see cref="string"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="type"/> is null.</exception>
        public static string GetLoggerCategoryName(this Type? type)
        {
            return type.GetDisplayFullName();
        }
    }
}
