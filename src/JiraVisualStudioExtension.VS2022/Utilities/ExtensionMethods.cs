using JiraVisualStudioExtension.Properties;
using System.Collections.Generic;
using System;
using System.Linq;

namespace JiraVisualStudioExtension.Utilities
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Makes the string the specified length inclusive of the the trailing string.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="totalLength">The total width.</param>
        /// <param name="trailingString">The trailing string.</param>
        /// <returns></returns>
        [ContractAnnotation("str:null => null; str:notnull => notnull")]
        public static string TrimTo(this string str, int totalLength, string trailingString)
        {
            return string.IsNullOrEmpty(str) || str.Length <= totalLength
                ? str
                : str.Substring(0, totalLength - trailingString.Length) + trailingString;
        }

        /// <summary>
        /// Finds the longest common prefix of the given strings
        /// </summary>
        public static string LongestCommonPrefix(this ICollection<string> strings, bool caseSensitive)
        {
            if (strings.Count == 0)
            {
                return "";
            }

            var compareFunc = caseSensitive
                ? (c1, c2) => c1 == c2
                : (Func<char, char, bool>)((c1, c2) => char.ToLower(c1) == char.ToLower(c2));

            var shortest = strings.OrderBy(s => s.Length).First();

            var end = shortest.Length;
            foreach (var s in strings)
            {
                for (var x = 0; x < shortest.Length; x++)
                    if (!compareFunc(s[x], shortest[x]))
                    {
                        if (x < end)
                            end = x;
                        break;
                    }
            }

            return shortest.Substring(0, end);
        }
    }
}