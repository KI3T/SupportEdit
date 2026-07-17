using System.Collections.Generic;
using System.Text.RegularExpressions;
using SupportEdit.Core.Models;

namespace SupportEdit.Core.Services
{
    // Parser mẫu theo yêu cầu
    public class ErrorParser
    {
        private static readonly Regex LineRegex = new Regex(
            "^XML(?<xml>\\d+):\\s*<STT>(?<stt>\\d+)</STT>\\s*:\\s*(?<field>[A-Z0-9_]+)\\s+(?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static List<XmlErrorItem> Parse(string input)
        {
            var list = new List<XmlErrorItem>();
            if (string.IsNullOrWhiteSpace(input)) return list;

            var lines = input.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var m = LineRegex.Match(line);
                if (!m.Success) continue;
                var item = new XmlErrorItem
                {
                    Xml = int.Parse(m.Groups["xml"].Value),
                    Stt = int.Parse(m.Groups["stt"].Value),
                    Field = m.Groups["field"].Value,
                    Message = m.Groups["message"].Value,
                    RawLine = line
                };
                list.Add(item);
            }

            return list;
        }
    }
}
