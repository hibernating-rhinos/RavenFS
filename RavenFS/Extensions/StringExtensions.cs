using System;

namespace RavenFS.Extensions
{
    public static class StringExtensions
    {
        public static string Reverse(this string value)
        {
            var characters = value.ToCharArray();
            Array.Reverse(characters);

            return new string(characters);
        }
    }
}