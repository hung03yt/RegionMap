using AbpRegionMap.RegionMappingService.Localization;
using Volo.Abp.Application.Services;

namespace AbpRegionMap.RegionMappingService;

public abstract class RegionMappingAppService : ApplicationService
{
    protected RegionMappingAppService()
    {
        LocalizationResource = typeof(RegionMappingServiceResource);
        ObjectMapperContext = typeof(AbpRegionMapRegionMappingServiceModule);
    }
}