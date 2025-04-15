using System;
using System.Linq;

namespace Paramore.Brighter.Gcp.Tests.Helper
{
    public static class DataGenerator
    {
        public static string CreateString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[new Random().Next(s.Length)]).ToArray());
        }    
    }
}
