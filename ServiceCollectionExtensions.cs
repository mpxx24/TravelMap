using TravelMap.Services;
using TravelMap.Settings;

namespace TravelMap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTravelMapServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TravelDataSettings>(configuration.GetSection("Storage"));
        services.AddSingleton<ITravelDataService, TravelDataService>();
        return services;
    }
}
