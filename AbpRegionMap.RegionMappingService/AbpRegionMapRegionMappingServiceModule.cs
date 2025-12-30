using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.AspNetCore.Serilog;
using Microsoft.OpenApi.Models;
using AbpRegionMap.RegionMappingService.Data;
using Prometheus;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerUI;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundJobs.RabbitMQ;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Data;
using Volo.Abp.Uow;
using Volo.Abp.DistributedLocking;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.AuditLogging;
using Volo.Saas;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.MultiTenancy;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Http.Client;
using Volo.Abp.LanguageManagement;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.LanguageManagement.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Saas.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using AbpRegionMap.RegionMappingService.HealthChecks;

namespace AbpRegionMap.RegionMappingService;

[DependsOn(
    typeof(BlobStoringDatabaseEntityFrameworkCoreModule),
    typeof(AbpSettingManagementEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(LanguageManagementEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    typeof(AbpFeatureManagementEntityFrameworkCoreModule),
    typeof(AbpAuditLoggingEntityFrameworkCoreModule),
    typeof(SaasEntityFrameworkCoreModule),
    typeof(AbpRegionMapRegionMappingServiceContractsModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpAspNetCoreMvcModule),
    // RabbitMQ and Redis caching modules are intentionally omitted for local/query-only runs
    // typeof(AbpEventBusRabbitMqModule),
    // typeof(AbpBackgroundJobsRabbitMqModule),
    // typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpHttpClientModule)
    )]
public class AbpRegionMapRegionMappingServiceModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var env = context.Services.GetHostingEnvironment();
        var runWithoutAuth = configuration.GetValue<bool>("RunWithoutAuth", false);

    // Feature flags to disable external dependencies for local/dev runs
    var rabbitEnabled = configuration.GetValue<bool?>("RabbitMQ:Enabled") ?? true;

        var redis = CreateRedisConnection(configuration);

    ConfigurePII(configuration);
        ConfigureMultiTenancy();
        if (!runWithoutAuth)
        {
            ConfigureJwtBearer(context, configuration);
        }
        ConfigureCors(context, configuration);
        ConfigureSwagger(context, configuration);
        ConfigureDatabase(context);
        ConfigureDistributedCache(configuration);

        // Only configure data-protection and distributed lock when Redis is available.
        if (redis != null)
        {
            ConfigureDataProtection(context, configuration, redis);
            ConfigureDistributedLock(context, redis);
        }
        if (rabbitEnabled)
        {
            ConfigureDistributedEventBus();
        }
        ConfigureIntegrationServices();
        ConfigureAntiForgery(env);
        ConfigureVirtualFileSystem();
        ConfigureObjectMapper(context);
        ConfigureAutoControllers();
        ConfigureDynamicClaims(context);
        ConfigureHealthChecks(context);
        
        context.Services.TransformAbpClaims();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();
    var configuration = context.GetConfiguration();
    var prometheusEnabled = configuration.GetValue<bool?>("Prometheus:Enabled") ?? true;

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseCorrelationId();
        app.UseAbpRequestLocalization();
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseCors();
        app.UseRouting();
        if (prometheusEnabled)
        {
            app.UseHttpMetrics();
        }
        if (!configuration.GetValue<bool>("RunWithoutAuth", false))
        {
            app.UseAuthentication();
            app.UseMultiTenancy();
            app.UseAuthorization();
        }
        else
        {
            app.UseMultiTenancy();
        }

        if (IsSwaggerEnabled(configuration))
        {
            app.UseSwagger();
            app.UseAbpSwaggerUI(options => { ConfigureSwaggerUI(options, configuration); });
        }
        
        app.UseAbpSerilogEnrichers();
        app.UseAuditing();
        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseConfiguredEndpoints(endpoints =>
        {
            if (prometheusEnabled)
            {
                endpoints.MapMetrics();
            }
        });
    }
    
    public override async Task OnPreApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        using var scope = context.ServiceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>();

        // Allow skipping database migrations for read-only / query-only local runs
        var skipMigrations = configuration?.GetValue<bool?>("SkipMigrations") ?? true;
        if (!skipMigrations)
        {
            await scope.ServiceProvider
                .GetRequiredService<RegionMappingServiceRuntimeDatabaseMigrator>()
                .CheckAndApplyDatabaseMigrationsAsync();
        }
    }
    
    private IConnectionMultiplexer? CreateRedisConnection(IConfiguration configuration)
    {
        // Allow disabling Redis for local/dev runs via configuration: "Redis:Enabled": false
        var enabled = configuration.GetValue<bool?>("Redis:Enabled") ?? true;
        if (!enabled) return null;

        var redisConfig = configuration["Redis:Configuration"];
        if (string.IsNullOrWhiteSpace(redisConfig)) return null;

        // Parse configuration and prefer AbortOnConnectFail = false for resiliency in dev
        var options = ConfigurationOptions.Parse(redisConfig);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    }

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddRegionMappingServiceHealthChecks();
    }
    
    private void ConfigurePII(IConfiguration configuration)
    {
        if (configuration.GetValue<bool>(configuration["App:EnablePII"] ?? "false"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }
    }


    private void ConfigureMultiTenancy()
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = true;
        });
    }
    
    private void ConfigureJwtBearer(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddAbpJwtBearer(options =>
            {
                options.Authority = configuration["AuthServer:Authority"];
                options.MetadataAddress = configuration["AuthServer:MetaAddress"]!.EnsureEndsWith('/') + ".well-known/openid-configuration";
                options.RequireHttpsMetadata = configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata");
                options.Audience = configuration["AuthServer:Audience"];
            });
    }

    private void ConfigureVirtualFileSystem()
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<AbpRegionMapRegionMappingServiceModule>();
        });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var corsOrigins = configuration["App:CorsOrigins"];
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                if (corsOrigins != null)
                {
                    builder
                        .WithOrigins(
                            corsOrigins
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .WithAbpExposedHeaders()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });
    }

    private void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        if (IsSwaggerEnabled(configuration))
        {
            var swaggerAuthority = configuration["AuthServer:Authority"] ?? string.Empty;
            context.Services.AddAbpSwaggerGenWithOAuth(
                authority: swaggerAuthority,
                scopes: new Dictionary<string, string>
                {
                    {"RegionMappingService", "RegionMappingService Service API"}
                },
                options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo {Title = "RegionMappingService API", Version = "v1"});
                    options.DocInclusionPredicate((_, _) => true);
                    options.CustomSchemaIds(type => type.FullName);
                });
        }
    }

    private void ConfigureDatabase(ServiceConfigurationContext context)
    {
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.Databases.Configure("AdministrationService", database =>
            {
                database.MappedConnections.Add(AbpPermissionManagementDbProperties.ConnectionStringName);
                database.MappedConnections.Add(AbpFeatureManagementDbProperties.ConnectionStringName);
                database.MappedConnections.Add(AbpSettingManagementDbProperties.ConnectionStringName);
            });
            
            options.Databases.Configure("AuditLoggingService", database =>
            {
                database.MappedConnections.Add(AbpAuditLoggingDbProperties.ConnectionStringName);
            });

            options.Databases.Configure("SaasService", database =>
            {
                database.MappedConnections.Add(SaasDbProperties.ConnectionStringName);
            });
            
            options.Databases.Configure("LanguageService", database =>
            {
                database.MappedConnections.Add(LanguageManagementDbProperties.ConnectionStringName);
            });
        });

        context.Services.AddAbpDbContext<RegionMappingServiceDbContext>(options =>
        {
            options.AddDefaultRepositories();
        });

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(opts =>
            {
                /* Sets default DBMS for this service */
                opts.UseNpgsql();
            });
            
            options.Configure<RegionMappingServiceDbContext>(c =>
            {
                c.UseNpgsql(b =>
                {
                    b.MigrationsHistoryTable("__RegionMappingService_Migrations");
                });
            });
        });
    }
    
    private void ConfigureDistributedCache(IConfiguration configuration)
    {
        Configure<AbpDistributedCacheOptions>(options =>
        {
            options.KeyPrefix = configuration["AbpDistributedCache:KeyPrefix"] ?? "";
        });
    }
    
    private void ConfigureDataProtection(ServiceConfigurationContext context, IConfiguration configuration, IConnectionMultiplexer redis)
    {
        context.Services
            .AddDataProtection()
            .SetApplicationName(configuration["DataProtection:ApplicationName"]!)
            .PersistKeysToStackExchangeRedis(redis, configuration["DataProtection:Keys"]);
    }

    private void ConfigureDistributedLock(ServiceConfigurationContext context, IConnectionMultiplexer redis)
    {
        context.Services.AddSingleton<IDistributedLockProvider>(
            _ => new RedisDistributedSynchronizationProvider(redis.GetDatabase())
        );
    }

    private void ConfigureDistributedEventBus()
    {
        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.Inboxes.Configure(config =>
            {
                config.UseDbContext<RegionMappingServiceDbContext>();
            });

            options.Outboxes.Configure(config =>
            {
                config.UseDbContext<RegionMappingServiceDbContext>();
            });
        });
    }
    
    private void ConfigureIntegrationServices()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ExposeIntegrationServices = true;
        });
    }
    
    private void ConfigureAntiForgery(IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            Configure<AbpAntiForgeryOptions>(options =>
            {
                /* Disabling ABP's auto anti forgery validation feature, because
                 * when we run the application in "localhost" domain, it will share
                 * the cookies between other applications (like the authentication server)
                 * and anti-forgery validation filter uses the other application's tokens
                 * which will fail the process unnecessarily.
                 */
                options.AutoValidate = false;
            });
        }
    }
    
    private void ConfigureObjectMapper(ServiceConfigurationContext context)
    {
        context.Services.AddAutoMapperObjectMapper<AbpRegionMapRegionMappingServiceModule>();

        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<AbpRegionMapRegionMappingServiceModule>(validate: true);
        });
    }
    
    private void ConfigureAutoControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options
                .ConventionalControllers
                .Create(typeof(AbpRegionMapRegionMappingServiceModule).Assembly, opts =>
                {
                    opts.RemoteServiceName = RegionMappingRemoteServiceConsts.RemoteServiceName;
                    opts.RootPath = RegionMappingRemoteServiceConsts.ModuleName;
                });
        });
    }
    
    private static void ConfigureSwaggerUI(SwaggerUIOptions options, IConfiguration configuration)
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RegionMappingService API");
        options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
        options.OAuthScopes("RegionMappingService");
    }
    
    private static bool IsSwaggerEnabled(IConfiguration configuration)
    {
        return bool.Parse(configuration["Swagger:IsEnabled"] ?? "true");
    }

    private void ConfigureDynamicClaims(ServiceConfigurationContext context)
    {
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }
}