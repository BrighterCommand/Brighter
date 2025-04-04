using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Paramore.Test.Helpers.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Centers the specified title string within a field of a specified width, padded with a specified character.
        /// </summary>
        /// <param name="titleString">The title string to be centered.</param>
        /// <param name="titleChar">The character used to pad the title string on both sides.</param>
        /// <param name="indentChar">The character used to pad the title string on both sides, inside the title character padding.</param>
        /// <param name="indent">The number of padding characters to add to each side of the title string.</param>
        /// <returns>A new string that represents the centered title string, padded with the specified characters.</returns>
        public static string? CenterTitle(this string? titleString, char titleChar, char indentChar, int indent)
        {
            if (string.IsNullOrEmpty(titleString))
            {
                return string.Empty;
            }

            int titleLength = titleString!.Length + ((1 + indent) * 2);
            StringBuilder sb = new();
            sb.Append(titleChar, titleLength);
            sb.AppendLine();
            sb.Append(indentChar);
            sb.Append(' ', indent);
            sb.Append(titleString);
            sb.Append(' ', indent);
            sb.Append(indentChar);
            sb.AppendLine();
            sb.Append(titleChar, titleLength);

            return sb.ToString();
        }

        /// <summary>
        /// Centers the specified title strings within a field of a specified width, padded with specified characters.
        /// </summary>
        /// <param name="titleStrings">The collection of title strings to be centered.</param>
        /// <param name="titleChar">The character used to pad the title string on both sides.</param>
        /// <param name="indentChar">The character used to pad the title string within the specified width.</param>
        /// <param name="indent">The number of padding characters to add to each side of the title string.</param>
        /// <returns>A new string that represents the centered title strings, padded with the specified characters.</returns>
        public static string? CenterTitles(this IEnumerable<string>? titleStrings, char titleChar, char indentChar, int indent)
        {
            if (titleStrings is null)
            {
                return string.Empty;
            }

            IList<string> titleList = titleStrings.ToList();

            if (!titleList.Any())
            {
                return string.Empty;
            }

            int maxTitleLength = titleList.Max(s => s.Length + ((1 + indent) * 2));
            var sb = new StringBuilder();
            sb.Append(titleChar, maxTitleLength);
            sb.AppendLine();

            // Calculate the total length of the longest title string.
            foreach (string titleString in titleList)
            {
                int stringIndent = ((maxTitleLength - titleString.Length) / 2) - 1;
                sb.Append(indentChar);
                sb.Append(' ', stringIndent);
                sb.Append(titleString);
                sb.Append(' ', maxTitleLength - titleString.Length - stringIndent - 2);
                sb.Append(indentChar);
                sb.AppendLine();
            }

            sb.Append(titleChar, maxTitleLength);
            return sb.ToString();
        }

        /// <summary>
        /// Centers a title string within a decorative border, using specified characters for the title border and indentation.
        /// </summary>
        /// <param name="titleString">The title string to be centered. If <see langword="null"/> or empty, a default centered title will be generated.</param>
        /// <returns>
        /// A formatted string where the title is centered within a decorative border.
        /// </returns>
        /// <remarks>
        /// This overload uses default characters for the title border and indentation.
        /// </remarks>
        public static string? CenterTitle(this string? titleString)
            => CenterTitle(titleString, '*', ' ', 5);

        /// <summary>
        /// Removes the namespace from a fully qualified type or member name, returning only the simple name.
        /// </summary>
        /// <param name="displayName">The fully qualified name of the type or member.</param>
        /// <returns>
        /// A string containing the simple name without the namespace. 
        /// If <paramref name="displayName"/> is <see langword="null"/> or empty, an empty string is returned.
        /// </returns>
        /// <example>
        /// For input "System.Collections.Generic.List", the method returns "List".
        /// </example>
        public static string RemoveNamespace(this string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return string.Empty;
            }
            int lastPeriod = displayName.LastIndexOf('.');
            return lastPeriod > 0 ? displayName[(lastPeriod + 1)..] : displayName;
        }
    }
}
