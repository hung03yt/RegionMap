using System;

namespace AbpRegionMap.RegionMappingService.Services;
public class RegionNewMappingDto
{
    public string ProvinceName { get; set; } = default!;
    public string WardName { get; set; } = default!;
    public int? IsAmbigious { get; set; }
}
