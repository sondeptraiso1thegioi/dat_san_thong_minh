# Quy ước dự án đặt sân

- Toàn bộ source, cấu hình, tài liệu và kiểm thử mới nằm trong `App/`.
- Tham khảo Cinema: Expo + TypeScript + React Navigation, Zustand, Axios; ASP.NET Core .NET 8 + EF Core + SQL Server. PaymentGateway giả lập triển khai sau.
- Làm từng chức năng/màn hình để người dùng xem trước khi chuyển sang phần khác.
- Quyết định ngày 06/11/2026: bắt đầu bằng frontend đăng nhập cho ba actor và kết nối database thật. API đăng nhập, JWT và đọc hồ sơ tối thiểu được phép triển khai trong bước này; chưa triển khai các nghiệp vụ backend khác.
- Quyết định ngày 19/11/2026 (thay quy định "chỉ làm frontend" ở trên cho các màn tiếp theo):
  - Làm giao diện người chơi theo từng màn; mỗi màn làm frontend kèm API dữ liệu thật và nối luôn, dừng cho người dùng xem trước khi sang màn tiếp.
  - Thanh toán dùng cổng giả lập giống Cinema (PaymentGateway), không dùng VNPay; báo cáo sẽ sửa sau.
  - Hủy booking đã thanh toán chỉ khi còn từ 24 giờ trở lên trước giờ chơi; payment chuyển `refund_pending` để admin hoàn tiền. Booking chưa thanh toán hủy lúc nào cũng được.
  - Bản đồ dùng Leaflet + OpenStreetMap (WebView trên điện thoại, trực tiếp trên web); không dùng react-native-maps vì Android cần Google Maps API key.
  - Thông tin tài khoản chỉ gồm các cột có trong `dbo.users`; không thêm trường kiểu Cinema (ngày sinh, giới tính, tỉnh).
  - Chủ sân (UC04): cụm sân mới luôn `pending`, người chơi chỉ thấy sân `approved`; màn admin duyệt (UC05) làm ngay sau bước O1. Sửa tên/địa chỉ/vị trí của sân đã duyệt → về `pending`; sửa sân bị từ chối → gửi lại (`pending`); giờ mở cửa, tiện ích, mô tả, sân con, bảng giá áp dụng ngay. Sân con đã lưu không đổi môn, không xóa (chỉ ngừng hoạt động).
  - Đặt sân: mỗi lần 1–3 giờ liên tiếp trên một sân con, bắt đầu giờ chẵn, trong 30 ngày tới (Tìm sân cũng tối đa 3 giờ). Phải thanh toán trong thời gian giữ chỗ (`booking_hold_minutes`), không có trả tại sân. Mỗi người chơi giữ 1 chỗ cùng lúc. Giữ chỗ hết hạn được dọn sang `cancelled` khi có người đặt cùng sân con.
  - Admin (UC05): chỉ duyệt/từ chối sân `pending`; duyệt cần ≥1 sân con hoạt động và có bảng giá. Ẩn sân = `status 'deleted'` + lý do trong `reject_reason`, hủy booking sắp tới và chuyển payment `paid` sang `refund_pending`, tất cả trong một lần lưu.
  - Ảnh cụm sân chưa làm ở bước O1 (cột `photos` giữ `[]`); quyết định cách lưu ảnh sau.
- Người dùng tự thực hiện việc cấu hình và gọi API AI/thời tiết theo hướng dẫn. Không tự gọi các dịch vụ đó.
- Schema chuẩn: `database/database-sqlserver.sql` trong `App/`; database hiện có là `DatSanThongMinh`. Không tự tạo/sửa bảng hay chạy EF migrations/EnsureCreated/EnsureDeleted.
- Tài khoản có đúng một role: `player`, `owner`, `admin`. Backend lấy role từ `dbo.users`, không lấy quyền từ lựa chọn phía client. Hash mật khẩu hiện có là bcrypt.
- Không copy secret hoặc các nhánh code demo dùng user đầu tiên của Cinema. Secret local phải được ignore.
- Trước khi sửa source, nêu các file/phần dự định sửa. Kiểm tra chạy thực tế phù hợp với thay đổi.

