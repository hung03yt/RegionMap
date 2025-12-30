using System;
using System.Text.Json.Serialization;

namespace AbpRegionMap.RegionMappingService.Services;
public class RegionOldMappingDto
{
    [JsonPropertyName("province_name")]
    public string ProvinceName { get; set; } = default!;

    [JsonPropertyName("district_name")]
    public string? DistrictName { get; set; }

    [JsonPropertyName("ward_name")]
    public string? WardName { get; set; }

    [JsonPropertyName("street_address")]
    public string? StreetAddress { get; set; }
}
