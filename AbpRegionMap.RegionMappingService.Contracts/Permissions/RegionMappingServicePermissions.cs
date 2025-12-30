using Volo.Abp.Reflection;

namespace AbpRegionMap.RegionMappingService.Permissions;

public class RegionMappingServicePermissions
{
    public const string GroupName = "RegionMappingService";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(RegionMappingServicePermissions));
    }
}