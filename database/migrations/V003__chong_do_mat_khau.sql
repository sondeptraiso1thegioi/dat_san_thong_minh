-- Nâng cấp tại chỗ, không xoá dữ liệu: đếm số lần sai mật khẩu và tạm khoá đăng nhập (chống dò mật khẩu).
-- Chạy lại nhiều lần vẫn an toàn. db-init.sh tự chạy khi dùng Docker; không Docker thì chạy bằng SSMS trước API mới.
USE NhaXeThongMinh;
GO
IF COL_LENGTH('dbo.tai_khoan', 'so_lan_sai') IS NULL
    ALTER TABLE dbo.tai_khoan ADD so_lan_sai INT NOT NULL CONSTRAINT DF_tai_khoan_so_lan_sai DEFAULT 0;
IF COL_LENGTH('dbo.tai_khoan', 'khoa_dang_nhap_den') IS NULL
    ALTER TABLE dbo.tai_khoan ADD khoa_dang_nhap_den DATETIME2(0) NULL;
GO
