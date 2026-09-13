using System.Globalization;

namespace Charlotte.Core.Organizer;

public static class TitleRules
{
    public static string Validate(string? title)
    {
        var normalized = title?.Trim() ?? string.Empty;
        var count = StringInfo.ParseCombiningCharacters(normalized).Length;
        if (count is < 1 or > 30)
            throw new OrganizerValidationException("标题必须为 1 至 30 个可见字符。");
        return normalized;
    }
}
