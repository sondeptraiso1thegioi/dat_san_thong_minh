# PROMPT DỰ ÁN: HỆ THỐNG ĐẶT SÂN THỂ THAO THÔNG MINH

> Cách dùng: dán toàn bộ file này vào AI trước khi giao việc, hoặc lưu thành `AGENTS.md` / `CLAUDE.md` ở thư mục gốc của repo để AI tự đọc. Sau đó giao **một** việc cụ thể, ví dụ: "Làm module bookings của backend theo mục 7 và 8".

> Quyết định ngày 30/10/2026: dùng bộ công cụ và cổng thanh toán giả lập như **The Sun Cinema**. Cấu trúc mới nằm trong `App/`, gồm `frontend`, `backend`, `PaymentGateway`. Các lựa chọn công nghệ và thanh toán cũ đã được thay thế trong tài liệu này.

---

## 1. Vai trò của bạn (AI)

Bạn là lập trình viên full-stack trong một nhóm sinh viên làm đồ án môn học, thời hạn 6 tuần. Hãy viết code chạy được, dễ đọc, đúng phạm vi bên dưới. Không tự thêm tính năng ngoài phạm vi. Quyết định mới được người dùng chốt có ưu tiên hơn tài liệu; cập nhật tài liệu khi thay đổi yêu cầu. Chỉ hỏi lại nếu còn mâu thuẫn chưa được người dùng giải quyết.

## 2. Tổng quan dự án

Ứng dụng đặt sân cho 4 môn: **tennis, pickleball, cầu lông (badminton), bóng bàn (table_tennis)**. Một cụm sân (venue) có nhiều sân con (court). Mỗi sân con thuộc một môn và một loại: `indoor` (trong nhà), `covered` (có mái che), `outdoor` (ngoài trời).

Ba actor, **mỗi tài khoản chỉ một vai trò**, cả ba dùng được trên **mobile và web**:

| Actor | Chức năng |
| --- | --- |
| Người dùng (`player`) | Tìm sân gần tôi; nhận gợi ý sân thông minh (AI); xem lưới giờ trống; đặt sân và thanh toán QR giả lập; xem lịch sử, hủy sân; chỉ đường bằng Google Maps |
| Chủ sân (`owner`) | Đăng ký cụm sân (địa chỉ, ghim vị trí, giờ mở cửa, số sân con, loại mái che, tiện ích); bảng giá theo khung giờ; xác nhận hoặc hủy booking; xem doanh thu (đã trừ hoa hồng); mua gói VIP |
| Admin (`admin`) | Duyệt, từ chối, sửa, ẩn (xóa mềm) cụm sân; thống kê lưu lượng sân; quản lý gói VIP và tỷ lệ hoa hồng; đối soát tiền trả chủ sân; khóa tài khoản; chỉnh cấu hình module AI |

## 3. Tech stack (đã chốt theo The Sun Cinema)

| Phần | Công nghệ |
| --- | --- |
| App (mobile + web, một codebase) | React Native + **Expo** (TypeScript), **React Navigation** (native stack + bottom tabs) |
| State và gọi API | **Zustand**, **Axios**; tách `api`, `services`, `store` như Cinema |
| Lưu phiên đăng nhập | Mobile: Expo SecureStore; web: sessionStorage như Cinema |
| Backend | **ASP.NET Core Web API (.NET 8, C#)**, REST API, JWT Bearer và phân quyền theo role |
| Cơ sở dữ liệu | **SQL Server 2022+** (Express/Developer), quản lý bằng **SQL Server Management Studio (SSMS)** theo yêu cầu giảng viên; schema chuẩn tại `database/database-sqlserver.sql` |
| ORM | **Entity Framework Core 8**, provider `Microsoft.EntityFrameworkCore.SqlServer`; database-first từ DB có sẵn. **Không** để ORM tự tạo hoặc sửa bảng |
| Công cụ API | Swagger/Swashbuckle, dependency injection như Cinema |
| Cập nhật lịch trống | SignalR như Cinema; chỉ để làm mới giao diện, DB vẫn quyết định chống trùng lịch |
| Lưu ảnh | MinIO qua S3 SDK như Cinema |
| Thời tiết | Open-Meteo (miễn phí, không cần API key) |
| Thanh toán | **PaymentGateway giả lập riêng bằng ASP.NET Core (.NET 8)** như Cinema: tạo bill, trả QR, nút thanh toán demo và callback HMAC-SHA256 |
| Bản đồ | Mobile: `react-native-maps`; web: Leaflet + OpenStreetMap. Chỉ đường: mở link Google Maps |
| LLM (tùy chọn) | Gemini hoặc Claude API, chỉ để hiểu câu người dùng gõ |
| Kiểm thử backend | **xUnit** cho logic quan trọng và kiểm thử tích hợp SQL Server cho transaction/trigger |
| Chạy local | SQL Server Express/Developer đang có + ASP.NET Core API + PaymentGateway; MinIO khi dùng upload ảnh. Docker Compose khi đóng gói |

Cấu trúc repo:

```
/App/frontend   # Expo app cho cả 3 actor
  App.tsx       # Khởi tạo phiên, điều hướng theo vai trò
  src/
    navigation/ # Root navigator và navigator riêng cho player/owner/admin
    screens/    # Auth, Player, Owner, Admin, Profile
    components/
    api/
    services/
    store/
    hooks/
    types/
    constants/
    utils/
/App/backend    # ASP.NET Core Web API
  Controllers/
  Services/Interface/
  Services/Implements/
  Repositories/Interface/
  Repositories/Implements/
  DTOs/
  Models/
  Data/         # AppDbContext và cấu hình EF Core
  Config/
  Middleware/
  Hubs/
  Recommendation/ # Logic AI tách riêng để trình bày
/App/PaymentGateway # ASP.NET Core: Controllers, Services, Models, Utils
/App/backend.Tests  # xUnit; nằm ngoài thư mục source của backend
/database/database-sqlserver.sql # schema + dữ liệu mẫu SQL Server (nguồn sự thật duy nhất)
/docker-compose.yml
```

Tham khảo source tại `MobileApp---The-Sun-Cinema-App/`; chỉ tạo/sửa ứng dụng đặt sân trong `App/`. Dùng cùng cách chia tầng và thành phần dùng chung, điều chỉnh nghiệp vụ cho đặt sân. Giữ 12 bảng hiện có; không đưa bộ bảng ASP.NET Identity của Cinema vào DB đặt sân. Xác thực dựa trên `users`, mật khẩu bcrypt hiện có và mỗi tài khoản một role.

## 4. Quy ước code

- Frontend dùng TypeScript strict; backend/gateway dùng C# bật nullable reference types. Tên biến, hàm, bảng bằng tiếng Anh; chuỗi hiển thị cho người dùng bằng tiếng Việt.
- **Tiền là số nguyên VNĐ** (`number` nguyên an toàn ở TS, `long` ở C#, `BIGINT` ở DB). Tính tỷ lệ hoa hồng bằng `decimal`, làm tròn `MidpointRounding.AwayFromZero`, kết quả tiền là `long`. Không dùng `float`/`double` cho tiền. ID `BIGINT` trả dưới dạng chuỗi trong JSON để tránh mất chính xác ở JavaScript.
- Thời gian lưu `DATETIMEOFFSET(3)`, mặc định UTC; hiển thị theo `Asia/Ho_Chi_Minh`. Chuỗi tiếng Việt dùng `NVARCHAR` và literal SQL `N'...'`.
- JSON lưu `NVARCHAR(MAX)` với `ISJSON`; EF Core ánh xạ thành `string`, backend dùng `System.Text.Json` để chuyển đổi với DTO. Bản `database/database.sql` là PostgreSQL tham khảo, không dùng làm schema backend mới.
- Backend chia Controller → Service → Repository → AppDbContext như Cinema. DTO validate bằng DataAnnotations và `[ApiController]`. Mọi endpoint nghiệp vụ (trừ đăng ký, đăng nhập, callback gateway) cần JWT và `[Authorize]`/policy kiểm tra role và quyền sở hữu tài nguyên. Callback dùng chữ ký HMAC. Các endpoint mô phỏng `MockGateway` chỉ phục vụ môi trường demo.
- Lỗi trả về dạng `{ "message": "...", "code": "BOOKING_OVERLAP" }` với HTTP status đúng nghĩa.
- Secret (JWT, khóa HMAC gateway, DB, MinIO, LLM key) dùng .NET User Secrets hoặc biến môi trường ở backend/gateway; `appsettings.json` chỉ chứa cấu hình công khai hoặc giá trị mẫu. Frontend có `.env.example` cho `EXPO_PUBLIC_API_URL`, `EXPO_PUBLIC_GATEWAY_URL` và URL công khai cần thiết; không chứa secret. Không commit secret.
- Muốn sửa schema: sửa `database/database-sqlserver.sql` và báo cả nhóm; database đã có dữ liệu cần SQL nâng cấp có kiểm soát. Dùng EF Core scaffold để đọc DB; không dùng EF Migrations, `Database.Migrate()`, `EnsureCreated()` hoặc `EnsureDeleted()` để tự sửa DB. Bảng `bookings` và `price_rules` có trigger: cấu hình `UseSqlOutputClause(false)` trong EF Core 8 để ghi dữ liệu tương thích.

## 5. Cơ sở dữ liệu (tóm tắt, chi tiết xem `database/database-sqlserver.sql`)

12 bảng: `users`, `venues`, `courts`, `price_rules`, `bookings`, `payments`, `vip_plans`, `vip_subscriptions`, `weather_cache`, `suggestion_logs`, `ai_settings`, `reports`. Có hàm SQL `dbo.distance_km(lat1, lng1, lat2, lng2)` (Haversine). Ảnh sân nằm trong `venues.photos` dạng mảng JSON.

Các cột thanh toán `vnp_txn_ref`, `vnp_transaction_no`, `vnp_response_code` giữ tên cũ để tương thích database đã tạo. Với gateway giả lập, chúng lần lượt lưu mã yêu cầu thanh toán duy nhất, `billId` của gateway và mã kết quả demo. DTO dùng tên trung lập `orderId`, `billId`, `responseCode`; các tên cột này không có nghĩa là tích hợp VNPay. Chỉ đổi tên cột bằng SQL nâng cấp riêng nếu sau này có yêu cầu.

Ràng buộc quan trọng đã có sẵn trong DB, code **phải** xử lý lỗi của chúng:

- Trigger `trg_bookings_no_overlap`: hai booking `pending`/`confirmed` không được chồng giờ trên cùng `court_id`, kể cả INSERT/UPDATE nhiều dòng. Lỗi SQL Server `51001` trả HTTP 409 với code `BOOKING_OVERLAP`. Tạo/xác nhận booking trong transaction, khóa court trước bằng `UPDLOCK, HOLDLOCK`; lỗi deadlock `1205` cần rollback/retry có giới hạn.
- Trigger `trg_price_rules_no_overlap`: chặn khung giá chồng nhau cho cùng venue/môn/thứ; lỗi SQL Server `51002` trả HTTP 409, code `PRICE_RULE_OVERLAP`. Khung liền nhau được phép.
- `payments`: với `paid`, `refund_pending`, `refunded`, bắt buộc có `paid_at` và `commission_amount + owner_amount = amount`; hoa hồng booking đúng phép làm tròn. Mỗi booking/lượt mua VIP tối đa một payment `paid`; lần thử thất bại và khoản nhận thêm cần hoàn vẫn được lưu.
- Booking `pending` bắt buộc có `hold_expires_at > created_at`. VIP `active`/`expired` bắt buộc đủ ngày bắt đầu/kết thúc hợp lệ.
- `venues.status`: `pending` → `approved` / `rejected`; ẩn sân là `deleted` (xóa mềm, không `DELETE`).

## 6. Quy tắc nghiệp vụ

1. **Đặt sân:** tạo booking `pending` với `hold_expires_at = now + 10 phút` (lấy từ `ai_settings.booking_hold_minutes`). Một `BackgroundService` dùng `PeriodicTimer` chạy mỗi phút chuyển booking `pending` quá hạn sang `cancelled`.
2. **Giá:** tính từ `price_rules` theo môn, thứ trong tuần (0 = Chủ nhật), giờ. Booking dài nhiều giờ thì cộng giá từng giờ.
3. **Chỉ đặt được** khi venue `approved`, court `is_active`, giờ nằm trong `open_time`–`close_time` và ở tương lai.
4. **Thanh toán QR giả lập qua PaymentGateway:** xem mục 8.
5. **Hoa hồng:** nền tảng trừ `commission_rate` (mặc định 0.08, trong `ai_settings`) trên mỗi booking đã thanh toán. `commission_amount = round(amount × rate)`, `owner_amount = amount − commission_amount`, `payout_status = 'pending'`. Thanh toán gói VIP thì toàn bộ là doanh thu nền tảng (`owner_amount = 0`, `payout_status = 'none'`).
6. **VIP:** gói có thời hạn. Venue có `vip_subscriptions` `active` và `now()` nằm trong `start_at`–`end_at` thì là VIP.
   - Trang chủ: mục "Sân nổi bật" chỉ gồm sân VIP trong bán kính mặc định, gần trước xa sau.
   - Danh sách gợi ý AI và tìm sân: **không đổi thứ tự**, sân VIP chỉ có huy hiệu "VIP" và viền màu.
7. **Ẩn sân:** khi admin ẩn venue, mọi booking tương lai của venue đó chuyển `cancelled` với `cancel_reason`; payment đã ghi nhận cần hoàn chuyển `refund_pending`, chỉ chuyển `refunded` sau bước xác nhận hoàn tiền giả lập của admin. Payout đang `pending` chuyển `none` để dừng trả; nếu đã `paid` thì giữ dấu vết để đối soát. Cập nhật trong transaction; SQL không tự gọi gateway. Đối soát và hoàn tiền trong đồ án đều là mô phỏng.
8. **Đăng ký sân miễn phí.** Nền tảng kiếm tiền từ hoa hồng và gói VIP.

## 7. Module AI: gợi ý sân thông minh (phần trọng tâm của đồ án)

Không huấn luyện mô hình. Viết bằng C# trong `App/backend/Recommendation/`, tách logic lọc, chấm điểm, thời tiết và sinh lý do để trình bày khi bảo vệ. Không cần service Python riêng.

**Endpoint:** `POST /api/recommendations`

```json
// Request: gửi text HOẶC các trường cấu trúc
{ "text": "tối thứ bảy 7 giờ đánh tennis gần nhà" }
{ "sport": "tennis", "date": "2026-11-07", "time": "19:00", "radius_km": 5, "lat": 21.03, "lng": 105.79 }
```

```json
// Response
{
  "parsed": { "sport": "tennis", "date": "2026-11-07", "time": "19:00", "radius_km": 5 },
  "items": [
    { "court_id": 3, "venue_id": 2, "venue_name": "Cụm sân B", "court_name": "Tennis mái 1",
      "start_at": "2026-11-07T19:00:00+07:00", "end_at": "2026-11-07T20:00:00+07:00",
      "distance_km": 2.54, "price": 200000, "environment": "covered", "is_vip": false,
      "score": 0.58, "breakdown": { "G": 0.5, "T": 1, "W": 1, "H": 0, "P": 0.67 },
      "reason": "Có mái che, dự báo mưa 70% lúc 19:00, cách 2,5 km" }
  ],
  "suggestion_log_id": 123
}
```

**Các bước:**

1. **Hiểu yêu cầu (nếu có `text`):** gọi LLM, yêu cầu trả **chỉ JSON** `{sport, date (YYYY-MM-DD), time (HH:mm), radius_km}`; truyền ngày hiện tại vào prompt để LLM hiểu "mai", "thứ bảy". Validate JSON. Sai hoặc thiếu `sport` thì trả HTTP 422 kèm các trường đã hiểu được để app mở form điền tiếp. Không có LLM key thì bỏ qua bước này (chỉ nhận dạng cấu trúc).
2. **Vị trí:** dùng `lat/lng` trong request; không có thì lấy `users.lat/lng`.
3. **Lọc ứng viên:** venue `approved`, court `is_active`, đúng môn, `distance_km ≤ radius_km`; các khung 1 giờ còn trống trong khoảng `time ± time_window_hours` (mặc định 2), nằm trong giờ mở cửa.
4. **Thời tiết:** lấy dự báo theo giờ cho từng venue từ `weather_cache`; thiếu hoặc cũ hơn 1 giờ thì gọi Open-Meteo và ghi cache:
   `https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lng}&hourly=precipitation_probability,temperature_2m,wind_speed_10m,uv_index&timezone=Asia/Ho_Chi_Minh&forecast_days=7`
   (Kiểm tra lại tên tham số theo tài liệu Open-Meteo.)
5. **Chấm điểm** từng cặp (court, khung giờ), mọi thành phần trong khoảng 0–1, trọng số lấy từ `ai_settings.weights`:

   `score = 0.30·G + 0.25·T + 0.25·W + 0.10·H − 0.10·P`

   | Ký hiệu | Cách tính |
   | --- | --- |
   | G (gần) | `1 − distance_km / radius_km` |
   | T (khớp giờ) | `1 − |giờ gợi ý − giờ muốn| / time_window_hours` |
   | W (thời tiết) | tra bảng dưới; "sheltered" = `indoor` hoặc `covered` |
   | H (thói quen) | `min(số booking completed/confirmed của user ở court này, 3) / 3` |
   | P (giá) | `giá / giá cao nhất trong các ứng viên` |

   Bảng W (ngưỡng nằm trong `ai_settings.weather_rules`), xét theo thứ tự, gặp điều kiện đầu tiên thì dừng:

   | Điều kiện tại giờ chơi | sheltered | outdoor |
   | --- | --- | --- |
   | Xác suất mưa ≥ 50% | 1.0 | 0.0 (kèm cảnh báo "có thể mưa") |
   | Nhiệt độ ≥ 35°C hoặc UV ≥ 8 | 0.8 | 0.3 |
   | Gió ≥ 30 km/h | 0.8 | 0.3 |
   | Còn lại (trời đẹp) | 0.6 | 1.0 |

   Không có dự báo (lỗi API hoặc ngoài 7 ngày): W = 0.5 cho mọi sân.
6. **Sắp xếp** giảm dần theo score; bằng điểm thì giờ sớm hơn trước. Lấy `top_k` (mặc định 5). Gắn `is_vip` nhưng **không đổi thứ tự**.
7. **Lý do:** sinh từ 1–2 thành phần đóng góp lớn nhất, ví dụ "Sân bạn hay đặt", "Có mái che, dự báo mưa 70%", "Đúng giờ bạn muốn", "Cách 1,2 km".
8. **Ghi log** vào `suggestion_logs` (query, items, sau này cập nhật `chosen_item` khi user đặt từ gợi ý; booking tạo từ gợi ý có `source = 'ai'`).

**Test bắt buộc (viết bằng xUnit cho hàm chấm điểm C# thuần, không gọi DB):**

- Ca 1 (mưa 70%, tennis 19:00, bán kính 5 km): A outdoor 1,2 km 150k, H=1 · B covered 2,5 km 200k · C indoor 4,0 km 300k VIP. Kỳ vọng thứ tự **B (≈0.58), A (≈0.53), C (≈0.46)**, C có `is_vip = true`.
- Ca 1b (trời đẹp 28°C, cùng dữ liệu): thứ tự **A (≈0.78), B (≈0.48), C (≈0.36)**.
- Ca 2 (cầu lông 18:00, trời đẹp): X indoor 1,0 km kín lúc 18:00, trống 17:00 và 19:00, H=1 · Y indoor 3,0 km trống 18:00, cùng giá 120k. Kỳ vọng **X 17:00 (0.515), X 19:00 (0.515), Y 18:00 (0.42)**.

## 8. Thanh toán giả lập giống The Sun Cinema

Một ứng dụng ASP.NET Core riêng tại `App/PaymentGateway`, tham khảo `PaymentGateway` của Cinema. Backend đặt sân quản lý booking, payment, hoa hồng và VIP; gateway tạo bill/QR và gửi callback. Không tích hợp ngân hàng hay VNPay sandbox.

### Luồng tạo bill và thanh toán demo

1. App gọi `POST /api/Payment/checkout` với `{ booking_id }` hoặc `{ vip_subscription_id }` (chỉ một trường). Backend kiểm tra JWT, quyền sở hữu, trạng thái và hạn giữ chỗ; lấy số tiền từ DB, không nhận tổng tiền do app tự tính. Tạo payment `pending`, sinh `orderId` duy nhất và lưu vào `payments.vnp_txn_ref`.
2. Backend gọi `POST /api/MockGateway/create-bill` trên gateway với `{ bookingId: orderId, amount, orderInfo, signature }`. Tên trường `bookingId` giữ theo giao thức Cinema, nhưng giá trị là mã lần thanh toán, dùng được cho cả booking lẫn VIP. `amount` là số nguyên VNĐ, không nhân 100.
3. Hai service dùng chung khóa HMAC-SHA256; chuỗi ký đúng thứ tự Cinema: `bookingId={orderId}&amount={amount}`, UTF-8, chữ ký hex chữ thường. Khóa chỉ ở backend/gateway. Gateway kiểm tra chữ ký, lưu bill để đối chiếu khi thanh toán, trả `{ billId, qrUrl }`. QR demo có thể dùng cùng dịch vụ tạo QR như Cinema; không đưa secret vào QR.
4. Backend lưu `billId` vào `vnp_transaction_no`, trả `{ data: { paymentId, orderId, amount, billId, qrUrl } }`. Khi gateway lỗi, ghi nhận lần thử thất bại và cho phép thử lại khi booking còn hạn; không để transaction DB mở trong lúc gọi HTTP.
5. App hiển thị QR, số tiền, đếm ngược và nút **"Thanh toán giả lập"** như Cinema. Nút gọi `POST /api/MockGateway/user-pay` với `{ bookingId: orderId, amount }`. Gateway đối chiếu bill đã lưu, ký và gửi `{ bookingId: orderId, amount, signature }` tới `POST /api/Payment/callback` của backend.

### Callback và trạng thái

- Callback không cần JWT; phải xác minh HMAC bằng so sánh constant-time, tra đúng payment theo `orderId`, kiểm tra số tiền với DB. Sai chữ ký → HTTP 400 `INVALID_SIGNATURE`; không có payment → HTTP 404 `PAYMENT_NOT_FOUND`; sai tiền → HTTP 400 `PAYMENT_AMOUNT_MISMATCH`.
- Callback thành công hợp lệ: trong **một transaction**, khóa và kiểm tra trạng thái hiện tại, cập nhật payment `paid`, `paid_at`, mã kết quả demo `00`, tính hoa hồng; chuyển booking còn hạn sang `confirmed` hoặc kích hoạt VIP với ngày bắt đầu/kết thúc hợp lệ. Khóa court trước khi kiểm tra lịch/xác nhận booking theo mục 5.
- Callback lặp của payment đã xử lý trả HTTP 200 và không ghi nhận doanh thu, xác nhận booking hoặc gia hạn VIP lần nữa. Hai lần thanh toán khác nhau cùng một booking/VIP phải được xử lý theo ràng buộc DB, không thu doanh thu hai lần.
- Tiền demo đến sau khi booking hết hạn/hủy, hoặc đã có lần thanh toán khác thành công: không xác nhận lại booking; ghi nhận khoản đã nhận thành `refund_pending` với thông tin chia tiền hợp lệ, `payout_status = 'none'`, để xử lý hoàn tiền giả lập. Không đổi payment đã nhận tiền sang `failed`.
- App đọc trạng thái bằng `GET /api/Payment/{paymentId}/status` (JWT, kiểm tra chủ sở hữu). Chỉ hiển thị thành công theo kết quả backend; không lấy dữ liệu lưu cục bộ hay thao tác bấm nút làm bằng chứng đã thanh toán.
- Môi trường local dùng URL backend/gateway cấu hình được; callback là HTTP giữa hai service, không bắt buộc ngrok. Điện thoại thật dùng địa chỉ LAN truy cập được từ thiết bị thay cho `localhost`.

## 9. Danh sách API chính

Các đường dẫn nghiệp vụ trong bảng dưới đều có tiền tố `/api` khi triển khai. Riêng endpoint thanh toán giữ cách đặt tên Cinema ở mục 8.

| Method + path | Role | Mô tả |
| --- | --- | --- |
| `POST /auth/register`, `POST /auth/login` | công khai | Đăng ký (`player` hoặc `owner`), đăng nhập trả JWT |
| `GET/PATCH /me` | mọi role | Hồ sơ, địa chỉ, lat/lng |
| `GET /venues/nearby?sport&lat&lng&radius_km&date&time` | player | Tìm sân gần tôi, sắp theo khoảng cách, kèm `is_vip` |
| `GET /venues/featured?lat&lng` | player | Sân VIP cho trang chủ |
| `GET /venues/:id`, `GET /courts/:id/availability?date` | player | Chi tiết, lưới giờ trống |
| `POST /recommendations` | player | Module AI (mục 7) |
| `POST /bookings`, `GET /bookings/me`, `POST /bookings/:id/cancel` | player | Đặt, xem, hủy |
| `POST /api/Payment/checkout`, `GET /api/Payment/{paymentId}/status` | player/owner | Thanh toán giả lập booking/VIP, kiểm tra quyền sở hữu |
| `POST /api/Payment/callback` | gateway, xác thực HMAC | Ghi nhận kết quả thanh toán giả lập |
| `POST/PATCH /owner/venues`, `/owner/venues/:id/courts`, `/owner/venues/:id/prices` | owner | Quản lý sân, sân con, bảng giá |
| `GET /owner/bookings`, `GET /owner/revenue` | owner | Booking và doanh thu (gross, hoa hồng, thực nhận) |
| `GET /vip-plans`, `POST /owner/vip-subscriptions` | owner | Mua gói VIP |
| `GET /admin/venues?status=pending`, `POST /admin/venues/:id/approve`, `/reject`, `/hide`, `PATCH /admin/venues/:id` | admin | Kiểm duyệt sân |
| `GET /admin/stats/traffic?from&to&group_by=hour|day|venue` | admin | Thống kê lưu lượng |
| `GET /admin/payouts`, `POST /admin/payouts/mark-paid` | admin | Đối soát tiền trả chủ sân |
| `GET/PUT /admin/settings/:key` | admin | Trọng số AI, hoa hồng, ngưỡng thời tiết |
| `CRUD /admin/vip-plans`, `POST /admin/users/:id/lock` | admin | Gói VIP, khóa tài khoản |

## 10. Màn hình app (React Navigation, chia navigator theo role)

`App.tsx` khởi tạo phiên đăng nhập. Root navigator chọn Auth, Player, Owner hoặc Admin dựa trên phiên và role; mỗi vai trò có stack/tab riêng. Tách màn hình, component, API service và Zustand store như Cinema.

- **Chung:** đăng nhập, đăng ký, hồ sơ (ghim vị trí trên bản đồ).
- **Player:** Trang chủ (Sân nổi bật + ô "Bạn muốn chơi gì?"), Gợi ý cho bạn (form hoặc ô gõ câu, danh sách kèm lý do và điểm thời tiết), Tìm sân gần tôi (danh sách + bản đồ), Chi tiết sân (ảnh, tiện ích, giá, lưới giờ), Xác nhận đặt và thanh toán, Lịch sử đặt sân, nút Chỉ đường mở `https://www.google.com/maps/dir/?api=1&destination={lat},{lng}`.
- **Owner:** Danh sách cụm sân (trạng thái duyệt), Thêm/sửa cụm sân, Sân con, Bảng giá, Booking, Doanh thu, Mua VIP.
- **Admin:** Sân chờ duyệt, Quản lý sân, Thống kê (biểu đồ theo giờ/ngày), Đối soát, Cài đặt AI, Gói VIP, Người dùng.
- Giao diện tiếng Việt, dùng được trên màn hình điện thoại và máy tính.

## 11. Ngoài phạm vi: KHÔNG làm

- Ghép đối, tìm bạn chơi, chat, đánh giá sân.
- Huấn luyện mô hình học máy, service Python riêng.
- Google Maps JavaScript/Directions API có key (chỉ dùng link chỉ đường).
- Thanh toán tiền thật, VNPay sandbox, MoMo, ví điện tử khác; chỉ dùng PaymentGateway giả lập như Cinema.
- Tự ý thêm bảng hoặc cột mà không cập nhật `database/database-sqlserver.sql`.

## 12. Kế hoạch 6 tuần

Các mốc dưới là kế hoạch tham khảo. Thứ tự thực hiện phải theo quy trình người dùng chốt tại mục 13: làm frontend trước, chỉ bắt đầu backend và gateway sau khi người dùng xác nhận frontend ổn; không tự triển khai đồng loạt theo kế hoạch tuần.

1. Tuần 1: repo, DB, khung Expo + ASP.NET Core + PaymentGateway, auth và phân quyền 3 role.
2. Tuần 2: owner quản lý cụm sân, sân con, bảng giá; admin duyệt, sửa, ẩn sân.
3. Tuần 3: tìm sân gần tôi, lưới giờ trống, đặt sân chống trùng, bản đồ, chỉ đường.
4. Tuần 4: QR và callback PaymentGateway giả lập cho booking/VIP, trang chủ Sân nổi bật, hoa hồng.
5. Tuần 5: module AI (lọc, chấm điểm, thời tiết, lý do, LLM) và test bắt buộc ở mục 7.
6. Tuần 6: thống kê admin, đối soát giả lập, dữ liệu demo, kiểm thử, sửa lỗi, Docker Compose đóng gói.

Nếu trễ tiến độ: cắt gói VIP trước, sau đó đến lớp LLM.

## 13. Cách làm việc với AI

Quy trình người dùng chốt ngày 30/10/2026, áp dụng cho toàn bộ quá trình triển khai:

1. **Làm từng chức năng, từng màn hình.** Mỗi lần chỉ triển khai phần đang được giao; liệt kê file sẽ tạo hoặc sửa trước khi viết. Hoàn thiện phần đó, kiểm tra, hướng dẫn chạy/xem và để người dùng nhận xét trước khi chuyển sang phần tiếp theo. Không tự viết toàn bộ dự án trong một lượt.
2. **Frontend trước.** Dựng giao diện và điều hướng Expo theo từng màn hình. Dùng dữ liệu mẫu cục bộ và trạng thái mô phỏng khi cần; ghi rõ phần nào đang là demo. Chưa tạo backend, PaymentGateway hoặc kết nối API thật trong giai đoạn duyệt frontend.
3. **Chờ người dùng duyệt frontend.** Chỉ bắt đầu viết backend/gateway khi người dùng xác nhận frontend ổn và cho phép chuyển sang giai đoạn đó. Việc đồng ý một màn hình riêng không tự động cho phép bắt đầu backend.
4. **Backend sau, cũng theo từng chức năng.** Sau khi được phép, triển khai API tương ứng, rồi nối frontend với backend và kiểm tra dữ liệu/luồng thực tế.
5. **Cấu hình IP khi nối hai bên.** Đến bước tích hợp mới xác định địa chỉ backend/gateway, cổng và URL gọi từ frontend theo môi trường chạy (web, giả lập, điện thoại thật); hướng dẫn người dùng cách cấu hình và kiểm tra. Chưa tự gắn IP hoặc giả định kết nối đã hoạt động ở giai đoạn frontend.
6. **API AI và API thời tiết do người dùng tự thực hiện theo hướng dẫn.** Khi đến bước cần tích hợp hoặc gọi các API này, báo trước và hướng dẫn từng bước để người dùng làm. Không tự tạo tài khoản/key, tự cấu hình dịch vụ hay tự gửi request đến API AI/thời tiết. Trong lúc dựng frontend, chỉ dùng dữ liệu mẫu cho gợi ý và thời tiết. Các ví dụ tích hợp ở mục 7 mô tả đích đến, không cho phép AI tự thao tác thay người dùng.

- Code backend phải kèm test cho logic quan trọng (giá, chồng giờ, hoa hồng, chấm điểm AI).
- Khi chưa rõ yêu cầu, hỏi lại thay vì đoán.
