# Nâng cấp Sun Sport lên Expo SDK 58

## Phiên bản nền

- Expo SDK: `^58.0.0`
- React Native: `0.88.1`
- React / React DOM: `19.3.0`
- TypeScript: `~6.0.3`
- Node.js tối thiểu: `22.13.x` (khuyến nghị Node 22 LTS mới nhất hoặc Node 24 tương thích)

Các package Expo native đã được nâng theo `bundledNativeModules.json` của nhánh SDK 58. `@expo/vector-icons` hiện giữ `15.0.3`, thỏa range `^15.0.2` của SDK 58.

## Thay đổi mã nguồn

React Native 0.87+ dùng Strict TypeScript API. Các ref của component native phải dùng kiểu instance. Đã cập nhật:

- `useRef<TextInput>` thành `useRef<TextInputInstance>` ở Login/Register.
- `forwardRef<TextInput, ...>` thành `forwardRef<TextInputInstance, ...>` ở `AuthField`.
- `useRef<ScrollView>` thành `useRef<ScrollViewInstance>` ở `VenueDetailScreen`.

Đã rà soát các API thường bị loại bỏ trong RN 0.87/0.88: không tìm thấy `InteractionManager`, deep import `react-native/Libraries/*`, `ImageBackground`, prop `Modal.animated` hoặc các prop StatusBar đã bị loại bỏ trong source frontend.

## Cài đặt dependency trên máy Windows

Mở CMD tại `App\frontend`:

```cmd
node --version
npm --version
```

Node phải từ `22.13.x` trở lên. Sau đó thực hiện:

```cmd
if exist package-lock.json del package-lock.json
npm install
npx expo install --fix
npx expo-doctor
npm run typecheck
```

`package-lock.json` SDK 54 đã được bỏ khỏi gói nâng cấp để npm tạo lockfile mới phù hợp SDK 58. Không chạy `npm ci` trước khi đã tạo lockfile mới bằng `npm install`.

## Chạy ứng dụng

```cmd
npm run start
```

Hoặc build/chạy Android native:

```cmd
npx expo run:android
```

Ứng dụng dùng Expo config/prebuild và không chứa thư mục `android/` hoặc `ios/` được quản lý thủ công trong source. SDK 58 sẽ sinh native project theo cấu hình `app.json` khi chạy prebuild/native run. Nếu chạy trên điện thoại thật, cần dùng địa chỉ LAN của máy tính trong `EXPO_PUBLIC_API_BASE_URL`, không dùng `localhost`.

## Ghi chú Expo Go và Development Build

Expo Go chỉ tương thích với SDK mà client đó hỗ trợ. Nếu Expo Go đang cài trên điện thoại báo SDK mismatch, cập nhật Expo Go tương ứng hoặc dùng development build. Với SDK 58, Expo khuyến nghị development build cho ứng dụng cần kiểm soát native runtime và kiểm thử gần với bản phát hành.

## Backend .NET

Việc nâng Expo không thay đổi backend. Backend vẫn hỗ trợ target `net8.0` và `net10.0` theo `DatSan.Api.csproj`. Expo frontend gọi cùng REST API, không phụ thuộc backend đang chạy trên target framework nào.

## Cấu hình và rủi ro khi nâng cấp

- `app.json` được giữ nguyên về bundle ID, scheme, quyền vị trí và cấu hình web hiện có.
- Không thay đổi nghiệp vụ đặt sân, đăng nhập, bản đồ, lịch đặt hoặc API.
- Lockfile cũ bị loại bỏ vì không thể đại diện chính xác cây dependency SDK 58; phải chạy `npm install` để tái tạo.
- Cần chạy `npx expo-doctor`, `npm run typecheck`, mở app trên Android và kiểm tra vị trí, SecureStore, WebView bản đồ, điều hướng và lịch đặt trước khi coi việc nâng cấp hoàn tất trên máy phát triển.
