using AbpRegionMap.RegionMappingService.Tests;
using Microsoft.AspNetCore.Builder;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder();
builder.Environment.ContentRootPath = GetWebProjectContentRootPathHelper.Get("AbpRegionMap.RegionMappingService.csproj"); 
await builder.RunAbpModuleAsync<RegionMappingServiceTestsModule>(applicationName: "AbpRegionMap.RegionMappingService");

public partial class TestProgram
{
}
