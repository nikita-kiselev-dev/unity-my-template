using System.Collections.Generic;
using System.IO;

namespace Framework.Foundation.File
{
    public static class CsvParser
    {
        public static string[][] Read(string text)
        {
            var result = new List<string[]>();
            
            using var reader = new StringReader(text);

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var values = line.Split(',');

                result.Add(values);
            }

            return result.ToArray();
        }
    }
}