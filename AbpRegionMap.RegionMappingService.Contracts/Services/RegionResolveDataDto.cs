using System.Text.Json.Serialization;

namespace AbpRegionMap.RegionMappingService.Services;

public class RegionResolveDataDto
{
    [JsonPropertyName("province_name")]
    public string? ProvinceName { get; set; }

    [JsonPropertyName("ward_name")]
    public string? WardName { get; set; }

    [JsonPropertyName("street_address")]
    public string? StreetAddress { get; set; }
}
