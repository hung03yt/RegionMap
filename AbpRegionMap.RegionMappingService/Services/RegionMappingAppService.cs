using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Services;
using AbpRegionMap.RegionMappingService.Data;

namespace AbpRegionMap.RegionMappingService.Services;

public class RegionMappingAppService : ApplicationService, IRegionMappingAppService
{
    private readonly RegionMappingServiceDbContext _dbContext;

    public RegionMappingAppService(RegionMappingServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RegionResolveResultDto> ResolveAsync(RegionOldMappingDto input)
    {
        try
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var provinceNorm = Normalize(input.ProvinceName);
            var districtNorm = Normalize(input.DistrictName ?? string.Empty);
            var wardNorm = Normalize(input.WardName ?? string.Empty);

            using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync();

            if (!string.IsNullOrWhiteSpace(wardNorm))
            {
                if (string.IsNullOrWhiteSpace(districtNorm))
                {
                    return new RegionResolveResultDto
                    {
                        Status = false,
                        Code = "NOT_FOUND",
                        Message = "district missing",
                        Data = new RegionResolveDataDto { ProvinceName = null, WardName = null, StreetAddress = null }
                    };
                }

                // province -> alias -> district -> alias -> ward -> alias,
                // 1) province id
                var provId = await GetUnitIdAsync(connection, provinceNorm, "PROVINCE", null);
                if (!provId.HasValue)
                {
                    return new RegionResolveResultDto { Status = false, Code = "NOT_FOUND", Message = "unit NOT_FOUND", Data = new RegionResolveDataDto { ProvinceName = null, WardName = null, StreetAddress = null } };
                }

                // 2) district id (must be child of province)
                var districtId = await GetUnitIdAsync(connection, districtNorm, "DISTRICT", provId);
                if (!districtId.HasValue)
                {
                    return new RegionResolveResultDto { Status = false, Code = "NOT_FOUND", Message = "unit NOT_FOUND", Data = new RegionResolveDataDto { ProvinceName = null, WardName = null, StreetAddress = null } };
                }

                // 3) ward id (must be child of district)
                var wardId = await GetUnitIdAsync(connection, wardNorm, "WARD", districtId);
                if (!wardId.HasValue)
                {
                    return new RegionResolveResultDto { Status = false, Code = "NOT_FOUND", Message = "unit NOT_FOUND", Data = new RegionResolveDataDto { ProvinceName = null, WardName = null, StreetAddress = null } };
                }

                var wardNewSql = @"
                    SELECT
                        p.province_name AS ProvinceName,
                        w.ward_name     AS WardName,
                        wm.is_ambigious AS IsAmbigious
                    FROM dvhc_new_ward_mapping wm
                    JOIN dvhc_new_provinces p ON p.province_id = wm.province_new_id
                    JOIN dvhc_new_wards w ON w.ward_id = wm.ward_new_id
                    WHERE wm.ward_old_id = @WardOldId
                    LIMIT 1
                ";

                var wardResult = await connection.QueryFirstOrDefaultAsync<RegionNewMappingDto>(wardNewSql, new { WardOldId = wardId.Value });

                if (wardResult == null)
                {
                    return new RegionResolveResultDto { Status = false, Code = "NOT_FOUND", Message = "unit NOT_FOUND", Data = new RegionResolveDataDto { ProvinceName = null, WardName = null, StreetAddress = null } };
                }

                var code = (wardResult.IsAmbigious.HasValue && wardResult.IsAmbigious.Value == 1) ? "AMBIGUOUS" : "FOUND";
                if (code == "AMBIGUOUS")
                {
                    return new RegionResolveResultDto { Status = true, Code = code, Message = "unit AMBIGUOUS", Data = new RegionResolveDataDto { ProvinceName = wardResult.ProvinceName, WardName = null, StreetAddress = input.StreetAddress } };
                }

                return new RegionResolveResultDto { Status = true, Code = code, Message = "unit FOUND", Data = new RegionResolveDataDto { ProvinceName = wardResult.ProvinceName, WardName = wardResult.WardName, StreetAddress = input.StreetAddress } };
            }
            //empty ward
            // Lookup province id with alias fallback
            var provinceOldId = await GetUnitIdAsync(connection, provinceNorm, "PROVINCE", null);

            if (!provinceOldId.HasValue)
            {
                return new RegionResolveResultDto
                {
                    Status = false,
                    Code = "NOT_FOUND",
                    Message = "unit NOT_FOUND",
                    Data = new RegionResolveDataDto
                    {
                        ProvinceName = null,
                        WardName = null,
                        StreetAddress = null
                    }
                };
            }

            var provinceNewSql = @"
                SELECT p.province_name AS ProvinceName
                FROM dvhc_new_provinces_mapping pm
                JOIN dvhc_new_provinces p ON p.province_id = pm.province_new_id
                WHERE pm.province_old_id = @ProvinceOldId
                LIMIT 1
            ";

            var newProvinceName = await connection.QueryFirstOrDefaultAsync<string>(
                provinceNewSql,
                new { ProvinceOldId = provinceOldId.Value });

            if (newProvinceName == null)
            {
                return new RegionResolveResultDto
                {
                    Status = false,
                    Code = "NOT_FOUND",
                    Message = "unit NOT_FOUND",
                    Data = new RegionResolveDataDto
                    {
                        ProvinceName = null,
                        WardName = null,
                        StreetAddress = null
                    }
                };
            }

            return new RegionResolveResultDto
            {
                Status = true,
                Code = "FOUND",
                Message = "unit FOUND",
                Data = new RegionResolveDataDto
                {
                    ProvinceName = newProvinceName,
                    WardName = null,
                    StreetAddress = input.StreetAddress
                }
            };
        }
        catch (Exception ex)
        {
            return new RegionResolveResultDto
            {
                Status = false,
                Code = "ERROR",
                Message = ex.Message,
                Data = new RegionResolveDataDto
                {
                    ProvinceName = null,
                    WardName = null,
                    StreetAddress = null
                }
            };
        }
    }

    public Task<string> PingAsync()
    {
        //health check
        return Task.FromResult("pong");
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        input = input.Trim().ToLowerInvariant();

        // Replace common Vietnamese-specific characters
        input = input.Replace('đ', 'd').Replace('Đ', 'd');

        // Decompose and remove diacritics
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        var withoutDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);

        // Remove punctuation and collapse whitespace
        withoutDiacritics = Regex.Replace(withoutDiacritics, "[^0-9a-zA-Z\\s]", " ");
        withoutDiacritics = Regex.Replace(withoutDiacritics, "\\s+", " ").Trim();

        return withoutDiacritics;
    }

    private static async Task<long?> GetUnitIdAsync(IDbConnection connection, string nameNorm, string level, long? parentId)
    {
        if (string.IsNullOrWhiteSpace(nameNorm))
            return null;

        // lookup, no alias
        var candidates = new List<string> { nameNorm };
        // common administrative prefixes in normalized form
        var prefixes = new[] { "quan", "huyen", "thi xa", "thi tran", "phuong", "xa" , "thanh pho", "tp", "tp." };
        foreach (var p in prefixes)
        {
            if (nameNorm.StartsWith(p + " ", StringComparison.Ordinal))
            {
                var trimmed = nameNorm.Substring(p.Length).Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !candidates.Contains(trimmed))
                    candidates.Add(trimmed);
            }
        }

        if (parentId.HasValue)
        {
            var sqlWithParent = @"
                SELECT id FROM cores_units_old
                WHERE level::text = @Level AND parent_id = @ParentId AND name_norm = @Name AND is_deleted::text IN ('0','f','false')
                LIMIT 1
            ";
            foreach (var candidate in candidates)
            {
                var id = await connection.QueryFirstOrDefaultAsync<long?>(sqlWithParent, new { Level = level, ParentId = parentId.Value, Name = candidate });
                if (id.HasValue) return id;
            }
        }
        else
        {
            var sqlNoParent = @"
                SELECT id FROM cores_units_old
                WHERE level::text = @Level AND name_norm = @Name AND is_deleted::text IN ('0','f','false')
                LIMIT 1
            ";
            foreach (var candidate in candidates)
            {
                var id = await connection.QueryFirstOrDefaultAsync<long?>(sqlNoParent, new { Level = level, Name = candidate });
                if (id.HasValue) return id;
            }
        }

        //direct lookup failed, try alias
        if (parentId.HasValue)
        {
            var aliasSqlWithParent = @"
                    SELECT a.unit_id FROM cores_units_old_alias a
                    JOIN cores_units_old u ON u.id = a.unit_id
                    WHERE a.alias_norm = @Name
                        AND a.is_active::text IN ('1','t','true')
                        AND a.is_deleted::text IN ('0','f','false')
                        AND u.level::text = @Level
                        AND u.parent_id = @ParentId
                        AND u.is_deleted::text IN ('0','f','false')
                    ORDER BY a.priority
                    LIMIT 1
            ";
            foreach (var candidate in candidates)
            {
                var aliasId = await connection.QueryFirstOrDefaultAsync<long?>(aliasSqlWithParent, new { Name = candidate, Level = level, ParentId = parentId.Value });
                if (aliasId.HasValue) return aliasId;
            }
        }
        else
        {
                        var aliasSqlNoParent = @"
                                SELECT a.unit_id FROM cores_units_old_alias a
                                JOIN cores_units_old u ON u.id = a.unit_id
                                WHERE a.alias_norm = @Name
                                    AND a.is_active::text IN ('1','t','true')
                                    AND a.is_deleted::text IN ('0','f','false')
                                    AND u.level::text = @Level
                                    AND u.is_deleted::text IN ('0','f','false')
                                ORDER BY a.priority
                                LIMIT 1
                        ";
            foreach (var candidate in candidates)
            {
                var aliasId = await connection.QueryFirstOrDefaultAsync<long?>(aliasSqlNoParent, new { Name = candidate, Level = level });
                if (aliasId.HasValue) return aliasId;
            }
        }

        return null;
    }
}
