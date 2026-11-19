/* =====================================================================
   V012: Điều hướng trong bãi (mở rộng luồng chọn khu + giữ suất của TimXe)
   Phiên điều hướng = suất giữ (giu_cho_khu). Trạng thái lưu trong DB → trạng thái app thấy:
     DANG_GIU → DANG_DAN · DA_VAO → DA_TOI · KHU_KHAC → TOI_KHU_KHAC · HET_HAN → HET_HAN · HUY (xe ra cổng) → DA_DONG
   ('KHU_KHAC' ngắn để vừa cột trang_thai VARCHAR(10) đang có, không phải sửa cột và các chỉ mục lọc theo cột này.)
   - giu_cho_khu: khu_goi_y_id (khu hệ thống gợi ý lúc qua cổng; khu_vuc_id là khu đích hiện tại, người dùng đổi được),
     khu_thuc_te_id (khu camera thấy xe vào), canh_bao_khu_id / canh_bao_luc (xe tới khu đã đầy, bị chặn).
   - di_sai_khu: mỗi lần đi sai khu (khu khác còn chỗ: chấp nhận; khu đầy: bị chặn + cảnh báo Admin). Admin xem ai hay đi sai.
   Chạy lại nhiều lần an toàn. db-init.sh tự chạy sau nha-xe.sql.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* 1. Cột mới của suất giữ / phiên điều hướng */
IF COL_LENGTH('dbo.giu_cho_khu', 'khu_goi_y_id') IS NULL
    ALTER TABLE dbo.giu_cho_khu ADD khu_goi_y_id INT NULL;
IF COL_LENGTH('dbo.giu_cho_khu', 'khu_thuc_te_id') IS NULL
    ALTER TABLE dbo.giu_cho_khu ADD khu_thuc_te_id INT NULL;
IF COL_LENGTH('dbo.giu_cho_khu', 'canh_bao_khu_id') IS NULL
    ALTER TABLE dbo.giu_cho_khu ADD canh_bao_khu_id INT NULL;
IF COL_LENGTH('dbo.giu_cho_khu', 'canh_bao_luc') IS NULL
    ALTER TABLE dbo.giu_cho_khu ADD canh_bao_luc DATETIME2(0) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_gck_khu_goi_y')
    ALTER TABLE dbo.giu_cho_khu ADD CONSTRAINT FK_gck_khu_goi_y FOREIGN KEY (khu_goi_y_id) REFERENCES dbo.khu_vuc(id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_gck_khu_thuc_te')
    ALTER TABLE dbo.giu_cho_khu ADD CONSTRAINT FK_gck_khu_thuc_te FOREIGN KEY (khu_thuc_te_id) REFERENCES dbo.khu_vuc(id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_gck_canh_bao_khu')
    ALTER TABLE dbo.giu_cho_khu ADD CONSTRAINT FK_gck_canh_bao_khu FOREIGN KEY (canh_bao_khu_id) REFERENCES dbo.khu_vuc(id);
GO
-- Suất cũ: khu gợi ý = khu đã giữ
UPDATE dbo.giu_cho_khu SET khu_goi_y_id = khu_vuc_id WHERE khu_goi_y_id IS NULL;
GO

/* 2. Trạng thái mới KHU_KHAC (tới khu khác khu được chỉ) */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_gck_trang_thai' AND definition LIKE '%KHU_KHAC%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_gck_trang_thai')
        ALTER TABLE dbo.giu_cho_khu DROP CONSTRAINT CK_gck_trang_thai;
    ALTER TABLE dbo.giu_cho_khu ADD CONSTRAINT CK_gck_trang_thai
        CHECK (trang_thai IN ('DANG_GIU', 'DA_VAO', 'KHU_KHAC', 'HET_HAN', 'HUY'));
END
GO

/* 3. Lần đi sai khu */
IF OBJECT_ID('dbo.di_sai_khu') IS NULL
BEGIN
    CREATE TABLE dbo.di_sai_khu (
        id               BIGINT IDENTITY(1,1) NOT NULL,
        luot_gui_id      INT          NOT NULL,
        xe_id            INT          NOT NULL,
        chu_xe_id        INT          NOT NULL,   -- đếm "ai hay đi sai" theo chủ xe
        bien_so          VARCHAR(15)  NOT NULL,
        khu_chi_dinh_id  INT          NULL,       -- khu được chỉ (null: xe không có phiên dẫn đường)
        khu_thuc_te_id   INT          NOT NULL,   -- khu camera thấy xe
        khu_day          BIT          NOT NULL,   -- 1: khu đã đầy, nhân viên không cho vào (camera không +1)
        luc              DATETIME2(0) NOT NULL,
        CONSTRAINT PK_di_sai_khu PRIMARY KEY (id),
        CONSTRAINT FK_dsk_luot_gui FOREIGN KEY (luot_gui_id) REFERENCES dbo.luot_gui(id),
        CONSTRAINT FK_dsk_xe FOREIGN KEY (xe_id) REFERENCES dbo.xe(id),
        CONSTRAINT FK_dsk_chu_xe FOREIGN KEY (chu_xe_id) REFERENCES dbo.tai_khoan(id),
        CONSTRAINT FK_dsk_khu_chi_dinh FOREIGN KEY (khu_chi_dinh_id) REFERENCES dbo.khu_vuc(id),
        CONSTRAINT FK_dsk_khu_thuc_te FOREIGN KEY (khu_thuc_te_id) REFERENCES dbo.khu_vuc(id)
    );
    CREATE INDEX IX_dsk_luc ON dbo.di_sai_khu(luc DESC);
    CREATE INDEX IX_dsk_chu_xe ON dbo.di_sai_khu(chu_xe_id) INCLUDE (khu_day, luc);
    CREATE INDEX IX_dsk_luot_khu ON dbo.di_sai_khu(luot_gui_id, khu_thuc_te_id, luc DESC);
END
GO

PRINT N'V012 phiên điều hướng + đi sai khu: xong';
GO
