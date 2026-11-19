/* =====================================================================
   V009: khu XE MÁY theo ảnh "Phương án bố trí bãi đỗ xe"
   - khu_vuc.ma_so_do: mã chấm trên ảnh sơ đồ (R, S, HAM). Khoá ổn định để ghép khu với ảnh và đồ thị đường đi,
     Admin đổi tên khu cũng không lạc chấm.
   - khu_vuc.dang_dung: 0 = khu tạm tắt (không chọn khi chỉ dẫn, không hiện cho người dùng). KHÔNG xoá dữ liệu.
   - Thêm 3 khu xe máy: Khu R (60), Khu S (40), Hầm xe máy B1/B2 (80). Hầm B1/B2 là MỘT khu vì camera đặt ở
     lối xuống chung, không biết xe đi tầng nào. Sức chứa chỉ đặt lúc thêm, chạy lại không ghi đè số Admin đã sửa.
   - Mỗi khu mới có camera hướng VÀO (KHU_VAO) và hướng RA (KHU_RA) như V008.
   - Tắt Khu C, Khu D (xe máy của dữ liệu mẫu, không có trên bản đồ thật): chỉ làm MỘT lần (đánh dấu trong cau_hinh),
     để Admin bật lại thì lần chạy sau không tắt nữa.
   - Sức chứa mỗi khu xe máy tối đa 80 (backend cũng kiểm tra khi Admin sửa).
   Khu ô tô (A, B...) giữ nguyên. Chạy lại nhiều lần vẫn an toàn. db-init.sh tự chạy sau nha-xe.sql.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* 1. Cột mới của khu_vuc */
IF COL_LENGTH('dbo.khu_vuc', 'ma_so_do') IS NULL
    ALTER TABLE dbo.khu_vuc ADD ma_so_do VARCHAR(10) NULL;
GO
IF COL_LENGTH('dbo.khu_vuc', 'dang_dung') IS NULL
    ALTER TABLE dbo.khu_vuc ADD dang_dung BIT NOT NULL CONSTRAINT DF_khu_vuc_dang_dung DEFAULT 1;
GO
-- Mỗi mã sơ đồ thuộc tối đa 1 khu
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_khu_vuc_ma_so_do')
    CREATE UNIQUE INDEX UX_khu_vuc_ma_so_do ON dbo.khu_vuc(ma_so_do) WHERE ma_so_do IS NOT NULL;
GO

/* 2. Khu R, S, Hầm. Khu cùng tên đã có (Admin tạo tay) thì chỉ gắn mã sơ đồ, không thêm khu trùng. */
DECLARE @xm INT = (SELECT id FROM dbo.loai_xe WHERE ten = 'XE_MAY');
DECLARE @khu TABLE (ma VARCHAR(10), ten NVARCHAR(50), suc_chua INT, toa_do NVARCHAR(200));
INSERT @khu VALUES
    ('R',   N'Khu R',              60, N'{"x":0.476,"y":0.272}'),
    ('S',   N'Khu S',              40, N'{"x":0.85,"y":0.278}'),
    ('HAM', N'Hầm xe máy B1/B2',   80, N'{"x":0.669,"y":0.62}');

UPDATE kv SET ma_so_do = k.ma
FROM dbo.khu_vuc kv JOIN @khu k ON kv.ten = k.ten
WHERE kv.ma_so_do IS NULL AND kv.loai_xe_id = @xm
  AND NOT EXISTS (SELECT 1 FROM dbo.khu_vuc x WHERE x.ma_so_do = k.ma);

INSERT dbo.khu_vuc (ten, loai_xe_id, toa_do_so_do, suc_chua, ma_so_do, dang_dung)
SELECT k.ten, @xm, k.toa_do, k.suc_chua, k.ma, 1
FROM @khu k
WHERE @xm IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.khu_vuc x WHERE x.ma_so_do = k.ma)
  AND NOT EXISTS (SELECT 1 FROM dbo.khu_vuc x WHERE x.ten = k.ten);
GO

/* 3. Camera hướng VÀO / hướng RA cho khu có mã sơ đồ (tên CAM-R-VAO, CAM-HAM-RA...) */
INSERT dbo.camera (khu_vuc_id, ten, nguon_anh, dang_bat, vai_tro)
SELECT k.id, N'CAM-' + k.ma_so_do + N'-' + h.duoi, h.anh, 1, h.vai_tro
FROM dbo.khu_vuc k
CROSS JOIN (VALUES ('KHU_VAO', N'VAO', '/demo/camera/khu-vao.jpg'),
                   ('KHU_RA',  N'RA',  '/demo/camera/khu-ra.jpg')) AS h (vai_tro, duoi, anh)
WHERE k.ma_so_do IN ('R', 'S', 'HAM')
  AND NOT EXISTS (SELECT 1 FROM dbo.camera c WHERE c.khu_vuc_id = k.id AND c.vai_tro = h.vai_tro)
  AND NOT EXISTS (SELECT 1 FROM dbo.camera c WHERE c.ten = N'CAM-' + k.ma_so_do + N'-' + h.duoi);
GO

/* 4. Tắt Khu C, Khu D xe máy của dữ liệu mẫu, chỉ một lần */
IF NOT EXISTS (SELECT 1 FROM dbo.cau_hinh WHERE khoa = 'v009_da_tat_khu_mau')
BEGIN
    UPDATE dbo.khu_vuc SET dang_dung = 0
    WHERE ten IN (N'Khu C', N'Khu D') AND ma_so_do IS NULL
      AND loai_xe_id = (SELECT id FROM dbo.loai_xe WHERE ten = 'XE_MAY');
    INSERT dbo.cau_hinh (khoa, gia_tri, mo_ta)
    VALUES ('v009_da_tat_khu_mau', N'1', N'V009 đã tắt Khu C, D xe máy của dữ liệu mẫu (Admin bật lại được)');
END
GO

/* 5. Sức chứa khu xe máy tối đa 80 */
UPDATE dbo.khu_vuc SET suc_chua = 80
WHERE suc_chua > 80 AND loai_xe_id = (SELECT id FROM dbo.loai_xe WHERE ten = 'XE_MAY');
GO

PRINT N'V009 khu xe máy theo bản đồ: xong';
GO
