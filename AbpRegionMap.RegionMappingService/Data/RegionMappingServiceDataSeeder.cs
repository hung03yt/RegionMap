using Volo.Abp.DependencyInjection;

namespace AbpRegionMap.RegionMappingService.Data;

public class RegionMappingServiceDataSeeder : ITransientDependency
{
    private readonly ILogger<RegionMappingServiceDataSeeder> _logger;

    public RegionMappingServiceDataSeeder(
        ILogger<RegionMappingServiceDataSeeder> logger)
    {
        _logger = logger;
    }

    public async Task SeedAsync(Guid? tenantId = null)
    {
        _logger.LogInformation("Seeding data...");
        
        //...
    }
}