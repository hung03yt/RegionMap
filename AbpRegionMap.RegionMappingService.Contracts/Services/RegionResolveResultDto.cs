using System.Text.Json.Serialization;

namespace AbpRegionMap.RegionMappingService.Services;

public class RegionResolveResultDto
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "NOT_FOUND";

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public RegionResolveDataDto? Data { get; set; }
}
