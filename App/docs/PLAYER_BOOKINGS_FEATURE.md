# Sun Sport — Chức năng Lịch đặt của người chơi

## 1. Phạm vi đã bổ sung

- Thay tab `Lịch đặt` đang là `ComingSoonScreen` bằng màn hình đọc dữ liệu booking thật.
- Có ba bộ lọc: `Tất cả`, `Sắp tới`, `Lịch sử`.
- Danh sách có phân trang API, tải thêm và kéo xuống để làm mới.
- Mỗi thẻ hiển thị cụm sân, sân con, môn, loại sân, ngày/giờ, giá, trạng thái booking và trạng thái payment nếu có.
- Mở chi tiết booking từ danh sách bằng màn hình chi tiết/giữ chỗ hiện có.
- Người chơi hủy được giữ chỗ `pending` chưa thanh toán.
- Booking `confirmed` chỉ được hủy khi còn ít nhất 24 giờ trước giờ bắt đầu; payment `paid` được chuyển thành `refund_pending` trong cùng transaction. Việc hoàn tiền thực tế vẫn do Admin xử lý ở chức năng riêng.
- API chỉ trả booking của người chơi đang đăng nhập; truy cập booking của người khác trả về 404.

## 2. API mới và API được mở rộng

### GET `/api/player/bookings`

Query:

- `view`: `all` (mặc định), `upcoming`, `history`.
- `page`: mặc định `1`.
- `pageSize`: mặc định `20`, giới hạn `1..50`.

Ví dụ:

```http
GET /api/player/bookings?view=upcoming&page=1&pageSize=20
Authorization: Bearer <access-token>
```

Response data:

```json
{
  "items": [
    {
      "id": "123",
      "status": "confirmed",
      "venueId": "10",
      "venueName": "Sun Sport",
      "venueAddress": "Cầu Giấy, Hà Nội",
      "courtName": "Sân cầu lông 01",
      "sport": "badminton",
      "environment": "indoor",
      "startAt": "2026-11-21T09:00:00+07:00",
      "endAt": "2026-11-21T10:00:00+07:00",
      "hours": 1,
      "price": 120000,
      "holdExpiresAt": null,
      "cancelReason": null,
      "createdAt": "2026-11-19T09:00:00+07:00",
      "paymentStatus": "paid",
      "canCancel": true,
      "cancellationInfo": "Khi hủy, giao dịch sẽ chuyển sang chờ hoàn tiền để Admin xử lý."
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "view": "upcoming",
  "serverNow": "2026-11-19T08:00:00+07:00"
}
```

### POST `/api/player/bookings/{id}/cancel`

- `pending`: đổi booking sang `cancelled`, ghi lý do hủy hoặc hết hạn giữ chỗ.
- `confirmed`: cần payment booking có `status = paid`, và `start_at >= serverNow + 24 giờ`; trong transaction đổi booking sang `cancelled`, payment sang `refund_pending`.
- Booking đã hủy/hoàn thành không thể hủy lần nữa.
- Lỗi nghiệp vụ trả mã `CANCELLATION_WINDOW_PASSED`, `PAID_PAYMENT_NOT_FOUND` hoặc `BOOKING_NOT_CANCELLABLE`.

### GET `/api/player/bookings/{id}`

Response detail được bổ sung `paymentStatus`, `canCancel`, `cancellationInfo` để giao diện hiển thị đúng quyền hủy từ backend. Frontend không tự quyết định chính sách hủy.

## 3. Quy tắc phân loại danh sách

- `upcoming`: booking `confirmed` chưa kết thúc và giữ chỗ `pending` còn hạn.
- `history`: booking `cancelled`, `completed`, `pending` đã hết hạn và booking `confirmed` đã kết thúc.
- `all`: tất cả booking của người chơi.
- Booking pending hết hạn vẫn được lưu `pending` trong database cho đến khi quy trình dọn dẹp hiện có cập nhật; API hiển thị status hiệu lực là `expired`.
- Danh sách `upcoming` sắp theo thời điểm bắt đầu tăng dần; `history`/`all` sắp theo thời điểm bắt đầu giảm dần.

## 4. Không thay đổi database

Không thêm bảng/cột, không chạy migration và không thay schema trong `database/database-sqlserver.sql`. Feature chỉ dùng các cột đã có trong `dbo.bookings` và `dbo.payments`.

## 5. Kiểm tra

Backend unit tests được bổ sung cho:

- Lọc upcoming/history/all.
- Phân trang và quyền sở hữu booking.
- Hủy pending còn hạn/hết hạn.
- Hủy confirmed đúng 24 giờ và chuyển payment sang `refund_pending`.
- Từ chối hủy confirmed khi chưa đủ 24 giờ hoặc không có payment paid.
- Kiểm tra tham số lọc không hợp lệ.

Khi có .NET SDK và SQL Server local, chạy:

```powershell
dotnet test App/backend.Tests/DatSan.Api.Tests.csproj
```

Kiểm tra API qua Swagger hoặc Postman sau khi đăng nhập bằng tài khoản `player`.

## 6. Giới hạn còn lại

- Luồng thanh toán mô phỏng chưa được triển khai trong patch này; màn pending vẫn hiển thị nút thanh toán bị khóa và ghi chú bước thanh toán sẽ làm sau.
- `refund_pending` chỉ là trạng thái chờ Admin xử lý, chưa chuyển tiền/hoàn tiền tự động.
- Lịch sử hoàn tiền chi tiết và biên nhận thanh toán sẽ thuộc chức năng Payment riêng.
