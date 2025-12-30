using Volo.Abp.DistributedLocking;
using Volo.Abp.EntityFrameworkCore.Migrations;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.DistributedLocking;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace AbpRegionMap.RegionMappingService.Data;

public class RegionMappingServiceDatabaseMigrationEventHandler : EfCoreDatabaseMigrationEventHandlerBase<RegionMappingServiceDbContext>
{
    private readonly RegionMappingServiceDataSeeder _dataSeeder;

    public RegionMappingServiceDatabaseMigrationEventHandler(
        ILoggerFactory loggerFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        ITenantStore tenantStore,
        IAbpDistributedLock abpDistributedLock,
        IDistributedEventBus distributedEventBus,
        RegionMappingServiceDataSeeder dataSeeder
    ) : base(
        RegionMappingServiceDbContext.DatabaseName,
        currentTenant,
        unitOfWorkManager,
        tenantStore,
        abpDistributedLock,
        distributedEventBus,
        loggerFactory)
    {
        _dataSeeder = dataSeeder;
    }

    protected override async Task SeedAsync(Guid? tenantId)
    {
        await _dataSeeder.SeedAsync(tenantId);
    }
}