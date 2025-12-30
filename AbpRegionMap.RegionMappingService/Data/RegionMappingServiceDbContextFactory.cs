using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AbpRegionMap.RegionMappingService.Data;

/* This class is needed for EF Core console commands
 * (like Add-Migration and Update-Database commands)
 * */
public class RegionMappingServiceDbContextFactory : IDesignTimeDbContextFactory<RegionMappingServiceDbContext>
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }
    
    public RegionMappingServiceDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<RegionMappingServiceDbContext>()
            .UseNpgsql(GetConnectionStringFromConfiguration(), b =>
            {
                b.MigrationsHistoryTable("__RegionMappingService_Migrations");
            });

        return new RegionMappingServiceDbContext(builder.Options);
    }

    private static string GetConnectionStringFromConfiguration()
    {
        return BuildConfiguration().GetConnectionString(RegionMappingServiceDbContext.DatabaseName)
               ?? throw new ApplicationException($"Could not find a connection string named '{RegionMappingServiceDbContext.DatabaseName}'.");
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}
