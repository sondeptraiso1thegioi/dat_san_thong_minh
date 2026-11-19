# Sun Sport API — Hỗ trợ .NET 8 và .NET 10

## 1. Cấu hình hiện tại

`App/backend/DatSan.Api.csproj` đã được cấu hình multi-target:

```xml
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

Các package phụ thuộc ASP.NET Core / EF Core được chọn theo target framework:

| Target | JwtBearer | EF Core SQL Server |
|---|---:|---:|
| `net8.0` | `8.0.34` | `8.0.34` |
| `net10.0` | `10.0.15` | `10.0.15` |

Các package dùng chung (BCrypt, Swashbuckle) được giữ nguyên. Frontend Expo không phụ thuộc target framework .NET; frontend chỉ gọi REST API.

## 2. Build theo từng target

Chạy từ thư mục gốc repository:

```powershell
# Restore các dependency
dotnet restore App/backend/DatSan.Api.csproj

# Build bản chạy trên .NET 8
dotnet build App/backend/DatSan.Api.csproj -f net8.0

# Build bản chạy trên .NET 10
dotnet build App/backend/DatSan.Api.csproj -f net10.0
```

Output được đặt riêng theo framework trong `App/backend/bin/Debug/net8.0/` và `App/backend/bin/Debug/net10.0/`.

## 3. Chạy ứng dụng

Mỗi lần khởi chạy, chọn một target framework:

```powershell
# Chạy backend bằng .NET 8
dotnet run --project App/backend/DatSan.Api.csproj -f net8.0

# Hoặc chạy backend bằng .NET 10
dotnet run --project App/backend/DatSan.Api.csproj -f net10.0
```

Hai lệnh là hai lần chạy riêng biệt, không phải một process tự đổi runtime. Nếu muốn chạy hai instance đồng thời, phải cấu hình port khác nhau và không dùng chung một cổng.

Máy phát triển cần cài SDK tương ứng để build. Khi chạy framework-specific output, máy triển khai cần có runtime tương ứng. Nếu chỉ cài .NET 10 runtime thì không thể chạy binary `net8.0` bằng runtime đó; cần cài thêm .NET 8 runtime hoặc chọn build `net10.0`.

## 4. Test

Project test hiện vẫn target `net8.0`, do đó project reference chọn target `net8.0` của API:

```powershell
dotnet test App/backend.Tests/DatSan.Api.Tests.csproj
```

Đây là kiểm thử service/API logic thông qua target .NET 8. Chưa cấu hình test project chạy đa target `net8.0;net10.0`.

## 5. Frontend Expo

Expo/React Native không cần build lại theo .NET target. Chạy frontend như hiện tại:

```powershell
cd App/frontend
npm ci
npx expo start
```

Đảm bảo `EXPO_PUBLIC_API_BASE_URL` trỏ đến backend đang chạy. Nếu dùng điện thoại thật, dùng LAN IP của máy chạy backend thay cho `localhost`.

## 6. Khuyến nghị về hỗ trợ phiên bản

Tại thời điểm cập nhật tài liệu (19/11/2026), .NET 8 đã hết hỗ trợ từ 10/11/2026; .NET 10 là bản LTS đang được hỗ trợ đến 14/11/2028. Vì vậy:

- Giữ `net8.0` để tương thích với môi trường/triển khai hiện hữu trong giai đoạn chuyển tiếp.
- Ưu tiên `net10.0` cho môi trường phát triển và triển khai mới.
- Lên kế hoạch bỏ `net8.0` khi không còn máy chủ hoặc người dùng cần tương thích với runtime này.

Tham khảo chính sách hỗ trợ chính thức: https://dotnet.microsoft.com/en-us/platform/support/policy
