namespace Fish.Services.Updates;

internal static class ReleaseVersion
{
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().TrimStart('v', 'V').Split('+')[0];
        if (text.Contains('-') || !Version.TryParse(text, out var parsed) || parsed.Build < 0) return false;
        version = new Version(parsed.Major, parsed.Minor, parsed.Build, Math.Max(0, parsed.Revision));
        return true;
    }
}
