using System;

namespace RavenFS.Studio.Extensions
{
    public static class StringExtensions
    {
        public static string Reverse(this string value)
        {
            var characters = value.ToCharArray();
            Array.Reverse(characters);

            return new string(characters);
        }

        public static bool IsNullOrEmpty(this string value)
        {
            return string.IsNullOrEmpty(value);
        }
    }
}