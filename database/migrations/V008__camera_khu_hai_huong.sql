/* =====================================================================
   V008: camera khu soi HAI HƯỚNG
   - Mỗi khu có camera hướng VÀO (KHU_VAO) và camera hướng RA (KHU_RA).
     Camera KHU_VAO đọc biển → ghi "xe X vào khu A" (số xe trong khu +1);
     camera KHU_RA đọc biển → ghi "xe X ra khỏi khu A" (số xe trong khu −1).
   - LOI_VAO_KHU (một camera dùng chung hai hướng) vẫn hợp lệ cho dữ liệu cũ.
   - Đổi camera LOI_VAO_KHU của dữ liệu mẫu thành KHU_VAO và thêm camera KHU_RA cho mỗi khu xe máy.
   Chạy lại nhiều lần vẫn an toàn. db-init.sh tự chạy sau nha-xe.sql.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* 1. Vai trò mới */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_camera_vai_tro')
    ALTER TABLE dbo.camera DROP CONSTRAINT CK_camera_vai_tro;
ALTER TABLE dbo.camera ADD CONSTRAINT CK_camera_vai_tro
    CHECK (vai_tro IN ('KHU_VAO', 'KHU_RA', 'LOI_VAO_KHU', 'GIAM_SAT'));
GO

/* 2. Mỗi khu tối đa 1 camera mỗi hướng */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_camera_huong_moi_khu')
    CREATE UNIQUE INDEX UX_camera_huong_moi_khu ON dbo.camera(khu_vuc_id, vai_tro) WHERE vai_tro IN ('KHU_VAO', 'KHU_RA');
GO

/* 3. Dữ liệu mẫu: camera lối vào cũ → camera hướng vào (khu chưa có camera KHU_VAO) */
UPDATE c SET vai_tro = 'KHU_VAO', ten = REPLACE(c.ten, N'-LOI-VAO', N'-VAO')
FROM dbo.camera c
WHERE c.vai_tro = 'LOI_VAO_KHU'
  AND NOT EXISTS (SELECT 1 FROM dbo.camera c2 WHERE c2.khu_vuc_id = c.khu_vuc_id AND c2.vai_tro = 'KHU_VAO')
  AND NOT EXISTS (SELECT 1 FROM dbo.camera c3 WHERE c3.ten = REPLACE(c.ten, N'-LOI-VAO', N'-VAO') AND c3.id <> c.id);
GO

/* 4. Mỗi khu xe máy có đủ camera hướng vào và hướng ra */
DECLARE @xm INT = (SELECT id FROM dbo.loai_xe WHERE ten = 'XE_MAY');
INSERT dbo.camera (khu_vuc_id, ten, nguon_anh, dang_bat, vai_tro)
SELECT k.id, N'CAM-' + REPLACE(k.ten, N'Khu ', N'') + N'-VAO', '/demo/camera/khu-vao.jpg', 1, 'KHU_VAO'
FROM dbo.khu_vuc k
WHERE k.loai_xe_id = @xm
  AND NOT EXISTS (SELECT 1 FROM dbo.camera c WHERE c.khu_vuc_id = k.id AND c.vai_tro = 'KHU_VAO')
  AND NOT EXISTS (SELECT 1 FROM dbo.camera c WHERE c.ten = N'CAM-' + REPLACE(k.ten, N'Khu ', N'') + N'-VAO');
INSERT dbo.camera (khu_vuc_id, ten, nguon_anh, dang_bat, vai_tro)
SELECT k.id, N'CAM-' + REPLACE(k.ten, N'Khu ', N'') + N'-RA', '/demo/camera/khu-ra.jpg', 1, 'KHU_RA'
FROM dbo.khu_vuc k
WHERE k.loai_xe_id = @xm
  AND NOT EXISTS (SELECT 1 FROM dbo.camera c WHERE c.khu_vuc_id = k.id AND c.vai_tro = 'KHU_RA')
  AND NOT EXISTS (SELECT 1 FROM dbo.camera c WHERE c.ten = N'CAM-' + REPLACE(k.ten, N'Khu ', N'') + N'-RA');
GO

PRINT N'V008 camera khu hai hướng: xong';
GO
