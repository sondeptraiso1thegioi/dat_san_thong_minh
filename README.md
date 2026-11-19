# Nhà xe thông minh tích hợp AI (bản viết lại theo đề cương v10)

Modular Monolith: một API ASP.NET Core trên .NET 10, chia 10 module, mỗi module có
`Contracts / Domain / Application / Infrastructure` và DbContext riêng. Thiết kế module: [`docs/kien-truc-module.md`](docs/kien-truc-module.md).

## Thư mục

| Thư mục | Nội dung |
|---|---|
| `backend/` | `NhaXe.slnx`, `src/SharedKernel` (IClock, LoiNghiepVu, sự kiện), `src/Api` (Program.cs chỉ lắp ráp, middleware lỗi, phát sự kiện), `src/Modules/` (thêm dần), `tests/NhaXe.Tests` |
| `frontend/` | React + TypeScript (Vite). `src/app` (route, khung, composition root), `src/shared` (gọi API, giao diện chung), `src/modules/` (thêm dần) |
| `database/` | `nha-xe.sql` (tạo mới: cấu hình, gói giá, sơ đồ bãi; **không có tài khoản**) + `migrations/V002..V006` (chạy lại nhiều lần vẫn an toàn) |
| `docker/` | Dockerfile API, Dockerfile web, `nginx.conf`, `db-init.sh` |
| `models/` | File ONNX của Model A, PaddleOCR, PKLot (chép vào khi làm module AI) |
| `docs/` | Kiến trúc module và hình luồng từng module |

## Chạy bằng Docker (cách khuyên dùng)

```powershell
copy .env.example .env      # rồi đổi SA_PASSWORD, JWT_KEY và điền ADMIN_SDT, ADMIN_EMAIL, ADMIN_MAT_KHAU
docker compose up -d --build
```

- Giao diện: http://localhost:8080 . Trang "Khung dự án" hiện `API: OK (máy ...)` là đã thông.
- Swagger: http://localhost:8080/swagger
- SQL Server (SSMS): `localhost,14330`, user `sa`, mật khẩu trong `.env`
- API chạy 2 bản; gọi `/api/health` vài lần sẽ thấy `instance` đổi qua lại (NGINX chia tải).
- Tài khoản: không còn tài khoản demo. Người dùng tự **Tạo tài khoản** trên web (lưu vào bảng `tai_khoan`, mật khẩu bcrypt), đăng ký xong tự đăng nhập.
  Admin đầu tiên: API tạo lúc khởi động nếu CSDL chưa có ADMIN, theo `ADMIN_*` trong `.env` (xem log `api` nếu không đăng nhập được).
- Xoá sạch CSDL, tạo lại từ đầu: đặt `RESET_DB=1` trong `.env` rồi `docker compose up db-init`, xong đặt lại `0` (mất mọi tài khoản đã đăng ký).

## Chạy khi code (không Docker cho API / web)

```powershell
docker compose up -d sqlserver db-init            # chỉ CSDL
cd backend;  dotnet test;  dotnet run --project src/Api --urls http://localhost:5080
cd frontend; npm install;  npm run dev            # http://localhost:5173, /api chuyển tới 5080
```

`backend/src/Api/appsettings.Development.json` có chuỗi kết nối tới `localhost,1433`; nếu dùng SQL Server trong Docker thì đổi thành
`localhost,14330` và mật khẩu trong `.env`.
Chạy API không qua Docker thì đặt biến môi trường cho admin đầu tiên trước khi `dotnet run` (PowerShell):
`$env:AdminDauTien__Sdt="09xxxxxxxx"; $env:AdminDauTien__Email="admin@..."; $env:AdminDauTien__MatKhau="..."`.

## Quy trình làm một module

1. Chốt bảng API (đường dẫn, dữ liệu vào / ra).
2. Frontend chạy bằng dữ liệu giả theo bảng đó, duyệt giao diện.
3. Backend: Domain → Application → Infrastructure → Controller, kèm test.
4. Nối frontend với API thật.

Luật phụ thuộc giữa module được kiểm tự động bằng `tests/NhaXe.Tests/Chung/LuatKienTrucTests.cs`.
