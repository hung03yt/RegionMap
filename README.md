Dự án này là dịch vụ "Region Mapping" (dịch tên địa phương cũ sang mô hình địa phương mới) được xây trên nền tảng ABP Framework, template ABP Microservice.

## Mô tả

Dịch vụ cung cấp API để chuyển tên địa phương/địa điểm từ hệ thống cũ sang tên/địa chỉ chuẩn của hệ thống mới. Ứng dụng sử dụng EF Core (Postgres) cho kết nối DB và Dapper cho các truy vấn thô cần tối ưu.

## Yêu cầu

- .NET SDK 10.x (phiên bản .NET 10 đã được dùng trong repo này)
- PostgreSQL (để kết nối DB nếu bạn muốn chạy tích hợp)
- (Tùy chọn) Redis, RabbitMQ, Prometheus, Elasticsearch — nhưng các dịch vụ này có thể tắt để chạy cục bộ.

> Lưu ý: repository đã thêm các flag cấu hình giúp chạy local mà không cần Redis/RabbitMQ/Prometheus/Elasticsearch. Mặc định, các flag này được bật/tắt trong `appsettings.json`.

## Database

- Service kết nối tới database được chỉ định như sau ở appsettings.json:
"ConnectionStrings": {
    ...
    "RegionMappingService": "Host=localhost;Port=5432;Database=postgres;User ID=postgres;Password=password;Timeout=240;"
}
- Trong sql\\sump_region_mapping.sql chứa toàn bộ thông tin database, phù hợp để import trong postgresql. Database được import vào sẽ là postgres

## Các flag cấu hình quan trọng

Trong `appsettings.json` hoặc biến môi trường, các cờ (flags) sau hữu ích cho phát triển cục bộ:

- `RunWithoutAuth` (bool) — nếu `true` sẽ tắt yêu cầu xác thực và dễ dàng gọi API trong môi trường dev.
- `SkipMigrations` (bool) — nếu `true` sẽ bỏ qua việc chạy migration/runtime database migrator (dùng để tránh distributed lock khi không có Redis).
- `Redis:Enabled` (bool) — bật/tắt kết nối Redis. Khi tắt, phần DataProtection / Distributed Locking sẽ không dùng Redis.
- `RabbitMQ:Enabled` (bool) — bật/tắt sử dụng RabbitMQ/eventbus.
- `Prometheus:Enabled` (bool) — bật/tắt metrics endpoint.
- `ElasticSearch:IsLoggingEnabled` (bool) — bật/tắt gửi logs tới Elasticsearch.

Bạn có thể ghép cờ này trong `appsettings.Development.json` hoặc set bằng biến môi trường (ví dụ: `Redis__Enabled=false`).

## Các lệnh cơ bản (PowerShell)

Mở `pwsh` tại thư mục `services\\regionmapping` và chạy:

```powershell
# Build solution
dotnet build .\\AbpRegionMap.RegionMappingService.sln -c Debug


## Endpoint chính

- POST `/api/app/region-mapping/resolve` — nhập JSON chứa các trường sau (snake_case):

Input mẫu (`RegionOldMappingDto`):

```json
{
  "province_name": "Thành phố Hà Nội",
  "district_name": "Quận Ba Đình",
  "ward_name": "Phường Trúc Bạch",
  "street_address": "Số 1, Phố X"
}
```

Response mẫu (schema chung):

```json
{
  "status": true,
  "code": "FOUND", // hoặc "NOT_FOUND", "AMBIGUOUS", "ERROR"
  "message": "Mô tả ngắn về kết quả",
  "data": {
    "province_name": "Hà Nội",
    "ward_name": "Phúc Xá",
    "street_address": "Số 1, Phố X"
  }
}
```