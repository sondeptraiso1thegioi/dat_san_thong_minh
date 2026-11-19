/* =====================================================================
   V011: Quản lý xe (module GoiCho)
   - xe.duyet_luc, xe.duyet_boi_id: lúc duyệt và Admin nào duyệt. Xe đã duyệt từ trước để NULL (giao diện hiện "Không rõ").
   - Loại thông báo mới: XE_CHO_DUYET (báo Admin có xe mới / gửi lại chờ duyệt), KET_QUA_DUYET (báo chủ xe được duyệt / bị từ chối).
   - Chỉ mục tìm biển số trong danh sách xe đã duyệt.
   Chạy lại nhiều lần an toàn. db-init.sh tự chạy sau nha-xe.sql.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* 1. Ai duyệt, lúc nào */
IF COL_LENGTH('dbo.xe', 'duyet_luc') IS NULL
    ALTER TABLE dbo.xe ADD duyet_luc DATETIME2(0) NULL;
IF COL_LENGTH('dbo.xe', 'duyet_boi_id') IS NULL
    ALTER TABLE dbo.xe ADD duyet_boi_id INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_xe_duyet_boi')
    ALTER TABLE dbo.xe ADD CONSTRAINT FK_xe_duyet_boi FOREIGN KEY (duyet_boi_id) REFERENCES dbo.tai_khoan(id);
GO

/* 2. Loại thông báo (giữ mọi loại cũ, thêm 2 loại mới) */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_tb_loai' AND definition LIKE '%KET_QUA_DUYET%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_tb_loai')
        ALTER TABLE dbo.thong_bao DROP CONSTRAINT CK_tb_loai;
    ALTER TABLE dbo.thong_bao ADD CONSTRAINT CK_tb_loai CHECK (loai IN ('VAO', 'RA', 'SAP_HET_HAN', 'HET_HAN', 'QUA_HAN',
        'NGHI_MAT', 'HUY_GOI', 'XE_BI_GO', 'KHACH_HUY', 'KHU', 'NHOM', 'DOI_SOAT', 'CHO_THANH_TOAN',
        'XE_CHO_DUYET', 'KET_QUA_DUYET'));
END
GO

/* 3. Danh sách xe đã duyệt lọc theo trạng thái rồi tìm biển số */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_xe_trang_thai_bien_so')
    CREATE INDEX IX_xe_trang_thai_bien_so ON dbo.xe(trang_thai_duyet, bien_so) INCLUDE (tai_khoan_id, loai_xe_id);
GO

PRINT N'V011 duyệt xe + thông báo: xong';
GO
