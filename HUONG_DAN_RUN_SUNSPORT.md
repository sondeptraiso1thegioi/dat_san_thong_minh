# SUN SPORT — Hướng dẫn chạy toàn bộ dự án

Tài liệu này hướng dẫn khởi chạy **Backend ASP.NET Core** và **Frontend Expo** trên Windows.

> Lưu ý: File Markdown (`.md`) chỉ chứa hướng dẫn, không thể tự thực thi lệnh như file `.bat`.

## 1. Chuẩn bị

Đảm bảo máy đã cài:

- .NET SDK phù hợp với Target Framework của Backend.
- Node.js và npm.
- SQL Server đang chạy.
- Database `DatSanThongMinh` đã được tạo và cấu hình kết nối trong `appsettings.json` hoặc biến môi trường.

Cấu trúc thư mục dự kiến:

```text
dat_san_thong_minh/
├── App/
│   └── frontend/
│       ├── package.json
│       └── ...
└── ...
```

Mở **hai cửa sổ CMD** tại thư mục gốc dự án.

## 2. Chạy Backend

Trong cửa sổ CMD thứ nhất, tìm file `.csproj` của Backend trong thư mục `App`, sau đó chạy lệnh (thay đường dẫn bằng đúng vị trí file):

```cmd
dotnet run --project "App\<duong-dan-backend>\<TenProject>.csproj" --urls http://localhost:5100
```

Nếu dự án multi-target và cần chỉ định .NET 10:

```cmd
dotnet run --project "App\<duong-dan-backend>\<TenProject>.csproj" --framework net10.0 --urls http://localhost:5100
```

Nếu dự án chỉ target .NET 8, dùng `--framework net8.0` hoặc bỏ tùy chọn `--framework`.

Backend dự kiến chạy tại:

- `http://localhost:5100`

Nếu kết nối SQL Server thất bại, kiểm tra `ConnectionStrings:DefaultConnection` trong cấu hình Backend. Không tự ý xóa database hoặc chạy migration khi chưa sao lưu và xác nhận trạng thái schema.

## 3. Chạy Frontend

Trong cửa sổ CMD thứ hai:

```cmd
cd App\frontend
```

Nếu đây là lần đầu chạy hoặc chưa có thư mục `node_modules`:

```cmd
npm install
```

Sau đó chạy Expo Web:

```cmd
set EXPO_PUBLIC_API_URL=http://localhost:5100
npx expo start --web
```

Nếu muốn chạy Expo ở chế độ thông thường thay vì web:

```cmd
set EXPO_PUBLIC_API_URL=http://localhost:5100
npx expo start
```

Mở địa chỉ được Expo hiển thị trong terminal.

## 4. Kiểm tra lỗi TypeScript

Tại thư mục `App\frontend`, chạy:

```cmd
npm run typecheck
```

Kiểm tra tương thích cấu hình Expo:

```cmd
npx expo-doctor@latest
```

Nếu `expo-doctor` đạt 20/20 nhưng `typecheck` còn lỗi, cần sửa lỗi TypeScript được báo; hai kết quả này kiểm tra các vấn đề khác nhau.

## 5. Thứ tự khởi chạy

1. Khởi động SQL Server.
2. Chạy Backend và chờ ứng dụng báo đã listening tại cổng `5100`.
3. Chạy Frontend bằng Expo.
4. Mở địa chỉ Expo cung cấp và kiểm tra các chức năng gọi API.

## 6. Dừng ứng dụng

Trong từng cửa sổ CMD đang chạy ứng dụng, nhấn:

```text
Ctrl + C
```

## 7. Các lưu ý

- Giữ cả hai cửa sổ CMD mở trong quá trình sử dụng.
- Frontend phải gọi đúng địa chỉ Backend. Nếu Backend dùng cổng khác `5100`, cập nhật `EXPO_PUBLIC_API_URL` tương ứng.
- `localhost` phù hợp khi frontend web và backend chạy trên cùng máy. Khi chạy ứng dụng trên điện thoại thật, cần dùng địa chỉ IP LAN của máy chạy Backend thay cho `localhost`.
- Tài liệu này không tự tạo database, không tự chạy migration và không thay đổi dữ liệu hiện có.
