using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureAiSearchWebsiteCrawler.Configs;

public static class ConfigurationHelper
{
    public static void ConfigureOptions<T>(this IServiceCollection services, IConfiguration configuration) where T : ConfigBase, new()
    {
        var configInstance = new T();
        services.AddOptionsWithValidateOnStart<T>().Bind(configuration.GetSection(configInstance.ConfigSection));
    }
}
