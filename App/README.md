# Sun Sport — ứng dụng đặt sân

Source mới nằm trong thư mục `App`. Công nghệ theo The Sun Cinema: Expo/React Native + TypeScript, React Navigation, Zustand, Axios; ASP.NET Core .NET 8 + EF Core + SQL Server.

## Phạm vi bước đầu: đăng nhập và đăng ký

- Một màn hình đăng nhập dùng trên web/mobile cho `player`, `owner`, `admin`.
- Xác thực email/mật khẩu thật từ `DatSanThongMinh.dbo.users`, kiểm tra bcrypt, trạng thái và vai trò.
- JWT chứa vai trò lấy từ DB; sau đăng nhập mở màn hình xác nhận/hồ sơ tương ứng, có đăng xuất.
- Khôi phục phiên bằng `GET /api/me`. Mobile lưu token bằng SecureStore; web dùng sessionStorage. Không có refresh token ở bước này; phiên hết hạn cần đăng nhập lại.
- Đăng ký (`POST /api/auth/register`) cho `player` và `owner`, theo luồng Cinema: nút "Tạo tài khoản Sun Sport" trên màn đăng nhập mở form Họ tên, Số điện thoại (bắt buộc, 10 số), Email, Mật khẩu (6–72 ký tự), Xác nhận mật khẩu và chọn vai trò. Không có đăng ký `admin`; backend kiểm tra lại vai trò. Email được lưu chữ thường; email/SĐT trùng trả 409 (`EMAIL_TAKEN`/`PHONE_TAKEN`). Đăng ký xong tự đăng nhập. Không thêm cột ngày sinh/giới tính/tỉnh như Cinema vì bảng `users` không có các cột này.
- Giao diện người chơi (19/11/2026): đăng nhập bằng tài khoản `player` sẽ vào khung 5 tab Trang chủ · Tìm sân · Gợi ý AI · Lịch đặt · Tài khoản (thanh dưới trên điện thoại, thanh bên trái trên web rộng). Đã xong **Trang chủ** với dữ liệu thật qua `GET /api/player/home`: trận sắp tới, sân nổi bật (VIP còn hạn trong `default_radius_km`), sân gần bạn theo khoảng cách từ vị trí trong hồ sơ (hoặc `lat`/`lng` gửi lên). Đã xong **Tìm sân** qua `GET /api/player/venues` (tên/khu vực không dấu, môn, bán kính, sân có mái theo từng sân con, tiện ích, ngày + giờ + thời lượng để chỉ hiện cụm còn sân trống kèm giá khung giờ), xem danh sách hoặc bản đồ Leaflet + OpenStreetMap, nút dùng vị trí GPS. Thời lượng tối đa 3 giờ, khớp với đặt sân. Gợi ý AI, Lịch đặt và Tài khoản đang là màn tạm.
- Chi tiết cụm sân + giữ chỗ (19/11/2026, F04 + bước đầu UC07):
  - `GET /api/player/venues/{id}?date=` trả thông tin cụm sân và lưới giờ của từng sân con trong ngày (trống / đã có người đặt / đã qua / chưa có giá) trong 30 ngày tới. Màn chi tiết có bản đồ, nút Chỉ đường (Google Maps), chọn môn, chọn ngày. Chạm ô để chọn giờ, chạm tiếp ô cùng hàng để chơi 1–3 giờ liên tiếp trên một sân con. Mở từ Tìm sân đang lọc theo giờ thì khung đó được chọn sẵn.
  - `POST /api/player/bookings` {courtId, date, startHour, hours} tạo booking `pending`, giữ chỗ `ai_settings.booking_hold_minutes` phút (mặc định 10). Mỗi người chơi giữ được 1 chỗ cùng lúc (`409 HOLD_EXISTS`). Trong một transaction backend khóa sân con (`UPDLOCK`), chuyển các giữ chỗ đã hết hạn của sân con đó sang `cancelled` (vì trigger `trg_bookings_no_overlap` vẫn coi `pending` hết hạn là đang chiếm sân), rồi mới thêm booking. Trùng giờ trả `409 SLOT_TAKEN`.
  - Màn **Giữ chỗ** đếm ngược theo giờ máy chủ, cho hủy giữ chỗ (`POST /api/player/bookings/{id}/cancel`, chỉ booking chưa thanh toán). `GET /api/player/bookings/{id}`, `GET /api/player/bookings/active-hold`. Nút Thanh toán chưa bật: thanh toán giả lập là bước tiếp theo.
- Giao diện chủ sân (19/11/2026, bước O1): tài khoản `owner` vào khung tab Cụm sân · Lịch đặt · Doanh thu · Tài khoản. Đã xong **Cụm sân của tôi** (trạng thái Chờ duyệt / Đã duyệt / Bị từ chối kèm lý do) và **form đăng ký/sửa cụm sân 4 bước** theo UC04: thông tin + ghim vị trí trên bản đồ + giờ mở cửa (cho phép đóng cửa 24:00) + tiện ích + mô tả → sân con (môn, tên, trong nhà/có mái/ngoài trời, bật/tắt) → bảng giá theo môn × ngày × khung giờ (nút tạo nhanh giờ thường + cao điểm 17–21h, kiểm tra không chồng giờ) → xem lại và gửi. API: `GET/POST /api/owner/venues`, `GET/PUT /api/owner/venues/{id}`. Cụm sân mới luôn ở `pending`; sửa sân bị từ chối sẽ gửi lại; sửa tên/địa chỉ/vị trí của sân đã duyệt sẽ quay về `pending`, các thay đổi khác áp dụng ngay. Sân con đã lưu không đổi môn và không xóa (chỉ ngừng hoạt động) để giữ lịch sử booking. Chưa có ảnh. **Người chơi chỉ thấy sân sau khi admin duyệt.**
- Giao diện admin (19/11/2026, UC05/F12): tài khoản `admin` vào khung tab Duyệt sân · Thống kê · Cấu hình · Tài khoản (Thống kê/Cấu hình chưa làm). **Duyệt sân**: lọc Chờ duyệt / Đã duyệt / Bị từ chối / Đã ẩn kèm số lượng, sân chờ lâu nhất lên đầu. **Xem xét cụm sân**: bản đồ ghim + nút đối chiếu trên Google Maps, thông tin, chủ sân, sân con, bảng giá. Thao tác:
  - Chờ duyệt → **Duyệt** (cần ít nhất 1 sân con đang hoạt động và có bảng giá, nếu không trả 400 `VENUE_INCOMPLETE`) hoặc **Từ chối** (lý do 5–500 ký tự, chủ sân thấy lý do và sửa để gửi lại).
  - Đã duyệt/Bị từ chối → **Ẩn** (lý do bắt buộc): `venues.status = 'deleted'`, lý do lưu ở `reject_reason`; các booking `pending`/`confirmed` chưa tới giờ bị hủy kèm `cancel_reason`; payment `paid` chuyển `refund_pending` (payout `pending` → `none`). Chưa có thao tác bỏ ẩn.
  - API: `GET /api/admin/venues?status=`, `GET /api/admin/venues/{id}`, `POST /api/admin/venues/{id}/approve|reject|hide` (chỉ role `admin`, khác trả 403).
- Màn hình hồ sơ là điểm kiểm tra luồng đăng nhập, chưa xây trang chủ/nghiệp vụ của ba actor.
- Quyết định mới ngày 06/11/2026 cho phép API xác thực tối thiểu để thực hiện yêu cầu kết nối DB thật. Các chức năng khác vẫn làm frontend từng màn hình trước, chờ người dùng xem.
- API AI, thời tiết và PaymentGateway triển khai sau theo hướng dẫn người dùng; bước này không gọi các dịch vụ đó.

## Cấu trúc

```text
App/
  frontend/        # Expo: screens, components, navigation, services, api, store
  backend/         # API: Controllers, Services, Repositories, Data, Models, DTOs
  backend.Tests/   # xUnit: các trường hợp xác thực và JWT
  database/        # Schema SQL Server tham chiếu, giữ nguyên từ bản đã tạo DB
  scripts/         # Tạo cấu hình local và kiểm tra đăng nhập thật
```

Database đã được tạo trước trong SQL Server. Bản SQL trong `App/database` là nguồn tham chiếu từ bước này; file ngoài `App` giữ làm bản cũ. Backend chỉ ánh xạ bảng `users` đang dùng, không tự tạo/sửa bảng hoặc chạy migrations.

## Chạy local trên Windows

Cần Node.js, npm, .NET SDK 8 trở lên có runtime .NET 8; SQL Server instance `SQLEXPRESS` đang chạy và có database `DatSanThongMinh` với dữ liệu đã nhập. Chạy backend bằng tài khoản Windows có quyền truy cập database, như tài khoản dùng SSMS.

Mở terminal tại `App` và chuẩn bị cấu hình:

```powershell
.\scripts\setup-local.ps1
```

Script tạo `backend/appsettings.Local.json` với Windows Authentication và khóa JWT ngẫu nhiên, cùng `frontend/.env`. Các file này được ignore. Script không đổi dữ liệu hoặc dịch vụ SQL Server. Có thể truyền `-ServerInstance 'TEN-MAY\SQLEXPRESS' -Database 'DatSanThongMinh'` nếu máy khác.

Terminal 1, tại `App`:

```powershell
dotnet restore .\backend\DatSan.Api.csproj
dotnet run --project .\backend\DatSan.Api.csproj
```

Terminal 2, tại `App`:

```powershell
Set-Location .\frontend
npm install
npm run web
```

Web: **http://localhost:8081**. API: **http://localhost:5100/api**. Swagger khi chạy development: **http://localhost:5100/swagger**. `GET /api/health` trả `ready` chỉ khi đọc được bảng `users` thật.

## Đăng nhập thử bằng dữ liệu hiện có

| Vai trò | Email | Mật khẩu dữ liệu mẫu |
| --- | --- | --- |
| Người chơi | `an@datsan.vn` | `123456` |
| Chủ sân | `owner1@datsan.vn` | `123456` |
| Quản trị viên | `admin@datsan.vn` | `123456` |

Đây là tài khoản từ schema/demo đã có, không phải tài khoản giả trong frontend. Không cần chọn vai trò trước khi đăng nhập. Mật khẩu sai/tài khoản không tồn tại nhận cùng thông báo; tài khoản bị khóa không được đăng nhập.

## Điện thoại thật và IP

Chạy `npm start` trong `frontend`, mở bằng Expo Go tương thích SDK 54 (cùng SDK Cinema). Máy tính và điện thoại dùng cùng mạng. Khi `EXPO_PUBLIC_API_URL` để trống, frontend tự dùng hostname của web hoặc địa chỉ máy chạy Expo và cổng `5100`.

Nếu API chạy trên máy khác, sửa `frontend/.env`:

```dotenv
EXPO_PUBLIC_API_URL=http://DIA_CHI_IP_MAY_BACKEND:5100/api
EXPO_PUBLIC_API_PORT=5100
```

Khởi động lại Expo sau khi sửa `.env`. Android emulator có thể dùng `http://10.0.2.2:5100/api`. Backend lắng nghe cổng `5100` trên các interface khi chạy profile local; cấu hình CORS chứa localhost và địa chỉ IPv4 của máy lúc chạy `setup-local.ps1`. Nếu IP thay đổi, chạy lại script để cập nhật CORS. Không tự thay đổi tường lửa của Windows; nếu thiết bị không kết nối được, kiểm tra quyền truy cập mạng/cổng trên máy.

## Kiểm tra

Tại `App`:

```powershell
dotnet test .\backend.Tests\DatSan.Api.Tests.csproj
.\scripts\verify-auth.ps1
.\scripts\verify-player.ps1
.\scripts\verify-owner.ps1
Set-Location .\frontend
npm run typecheck
npm run build:web
```

Trên trình duyệt, nút "Dùng vị trí hiện tại" chỉ hoạt động ở `http://localhost` hoặc `https` (quy định của trình duyệt); mở web qua IP LAN thì dùng vị trí trong hồ sơ. Bản đồ cần Internet để tải ô OpenStreetMap (trên điện thoại còn tải Leaflet từ unpkg).

`verify-owner.ps1` (chỉ đọc, các yêu cầu ghi đều bị từ chối) kiểm tra API cụm sân của chủ sân: danh sách, chi tiết có sân con và bảng giá, chủ sân khác nhận 404, người chơi 403, dữ liệu sai 400 và không tạo thêm dòng nào.

`verify-player.ps1` (chỉ đọc) kiểm tra tìm sân (lọc môn, tìm không dấu, giá khung giờ 16:00–18:00 = 270.000đ ở Cụm sân A, lỗi 400) và trang chủ người chơi: sắp xếp theo khoảng cách, sân VIP trong bán kính, không hiện sân chờ duyệt, vị trí GPS ghi đè hồ sơ, 401/400/403.

`verify-auth.ps1` yêu cầu API đang chạy và dữ liệu demo hiện có. Script kiểm tra đăng nhập ba vai trò, đọc hồ sơ bằng JWT, sai mật khẩu, email không tồn tại, validation, truy cập không có JWT, và các trường hợp đăng ký bị từ chối (vai trò admin, SĐT sai, mật khẩu ngắn, email/SĐT trùng); không sửa dữ liệu. Muốn thử đăng ký thành công, dùng giao diện với email/SĐT mới — thao tác này tạo một dòng thật trong `dbo.users`.

Các biến môi trường backend có thể ghi đè cấu hình: `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes`. Client chỉ chứa URL API, không chứa tài khoản SQL hoặc khóa JWT.
