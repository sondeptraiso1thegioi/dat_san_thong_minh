-- Nâng cấp tại chỗ, không xóa tài khoản/nhóm/xe. Chạy bằng SSMS trước API mới.
USE NhaXeThongMinh;
GO
IF COL_LENGTH('dbo.tai_khoan', 'ma_khoi_phuc_hash') IS NULL
    ALTER TABLE dbo.tai_khoan ADD ma_khoi_phuc_hash VARCHAR(64) NULL;
IF COL_LENGTH('dbo.tai_khoan', 'ma_khoi_phuc_het_han_luc') IS NULL
    ALTER TABLE dbo.tai_khoan ADD ma_khoi_phuc_het_han_luc DATETIME2(0) NULL;
IF COL_LENGTH('dbo.tai_khoan', 'gui_ma_khoi_phuc_luc') IS NULL
    ALTER TABLE dbo.tai_khoan ADD gui_ma_khoi_phuc_luc DATETIME2(0) NULL;
IF COL_LENGTH('dbo.tai_khoan', 'phien_ban') IS NULL
    ALTER TABLE dbo.tai_khoan ADD phien_ban ROWVERSION NOT NULL;
GO
