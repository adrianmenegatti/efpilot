namespace EfPilot.Core.Profiles;

public static class ProfileNameGenerator
{
    public static string FromDbContext(string dbContextName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbContextName);

        var name = dbContextName;

        if (name.StartsWith("App", StringComparison.OrdinalIgnoreCase) && name.Length > 3)
        {
            name = name[3..];
        }

        name = RemoveSuffix(name, "DbContext");
        name = RemoveSuffix(name, "Context");

        return string.IsNullOrWhiteSpace(name)
            ? dbContextName
            : name;
    }

    private static string RemoveSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }
}