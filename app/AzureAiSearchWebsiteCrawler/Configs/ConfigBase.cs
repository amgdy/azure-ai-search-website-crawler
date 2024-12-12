namespace AzureAiSearchWebsiteCrawler.Configs;

public abstract class ConfigBase
{
    public string ConfigSection
    {
        get
        {
            var typeName = GetType().Name;

            ReadOnlySpan<string> suffixList = ["Options", "Settings", "Config"];
            foreach (var suffix in suffixList)
            {
                if (typeName.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return typeName.AsSpan(0, typeName.Length - suffix.Length).ToString();
                }
            }

            return typeName;
        }
    }
}
