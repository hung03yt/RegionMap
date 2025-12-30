using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace AbpRegionMap.RegionMappingService.Services;

public interface IRegionMappingAppService : IApplicationService
{
    Task<RegionResolveResultDto> ResolveAsync(RegionOldMappingDto oldMapping);
    Task<string> PingAsync();
}
