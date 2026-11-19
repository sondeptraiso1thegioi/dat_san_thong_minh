/* =====================================================================
   V004: chia bãi thành khu (đề cương v10, mục "Chia khu, ghi nhận xe vào khu và tìm xe")
   - khu_vuc.suc_chua: số xe tối đa của khu; tổng các khu cùng loại = loai_xe.suc_chua
   - camera.vai_tro: LOI_VAO_KHU (đọc biển ở lối vào khu, Model A + PaddleOCR)
                     GIAM_SAT    (nhìn ô đỗ khu ô tô, YOLOv8 PKLot)
   - giu_cho_khu (module TimXe): giữ 1 suất trong khu vài phút sau khi xe vào cổng
   - nguồn vị trí CAMERA_KHU; vi_tri_xe.huong VAO / RA
   - 4 tham số cau_hinh mới
   Chạy lại nhiều lần vẫn an toàn. db-init.sh tự chạy sau nha-xe.sql.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* 1. Sức chứa từng khu */
IF COL_LENGTH('dbo.khu_vuc', 'suc_chua') IS NULL
    ALTER TABLE dbo.khu_vuc ADD suc_chua INT NOT NULL CONSTRAINT DF_khu_vuc_suc_chua DEFAULT 0;
GO
-- Cột vừa thêm thì ràng buộc phải ở batch sau (SQL Server biên dịch cả batch trước khi chạy)
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_khu_vuc_suc_chua')
    ALTER TABLE dbo.khu_vuc ADD CONSTRAINT CK_khu_vuc_suc_chua CHECK (suc_chua >= 0);
GO
-- Dữ liệu cũ: chia đều sức chứa của loại xe cho các khu cùng loại (khu đầu nhận phần dư),
-- chỉ áp cho loại xe mà chưa khu nào có sức chứa.
;WITH k AS (
    SELECT kv.id, kv.loai_xe_id, lx.suc_chua AS tong,
           COUNT(*) OVER (PARTITION BY kv.loai_xe_id) AS so_khu,
           ROW_NUMBER() OVER (PARTITION BY kv.loai_xe_id ORDER BY kv.id) AS stt
    FROM dbo.khu_vuc kv JOIN dbo.loai_xe lx ON lx.id = kv.loai_xe_id
    WHERE NOT EXISTS (SELECT 1 FROM dbo.khu_vuc k2 WHERE k2.loai_xe_id = kv.loai_xe_id AND k2.suc_chua > 0)
)
UPDATE kv SET suc_chua = k.tong / k.so_khu + CASE WHEN k.stt = 1 THEN k.tong % k.so_khu ELSE 0 END
FROM dbo.khu_vuc kv JOIN k ON k.id = kv.id;
GO

/* 2. Vai trò camera */
IF COL_LENGTH('dbo.camera', 'vai_tro') IS NULL
    ALTER TABLE dbo.camera ADD vai_tro VARCHAR(12) NOT NULL CONSTRAINT DF_camera_vai_tro DEFAULT 'GIAM_SAT';
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_camera_vai_tro')
    ALTER TABLE dbo.camera ADD CONSTRAINT CK_camera_vai_tro CHECK (vai_tro IN ('LOI_VAO_KHU', 'GIAM_SAT'));
GO
-- Dữ liệu mẫu: camera có tên chứa "LOI-VAO" là camera lối vào khu
UPDATE dbo.camera SET vai_tro = 'LOI_VAO_KHU' WHERE ten LIKE N'%LOI-VAO%' AND vai_tro <> 'LOI_VAO_KHU';
-- Mỗi khu tối đa 1 camera lối vào (v10: 1 lối vào khu dùng chung cho vào và ra)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_camera_loi_vao_moi_khu')
    CREATE UNIQUE INDEX UX_camera_loi_vao_moi_khu ON dbo.camera(khu_vuc_id) WHERE vai_tro = 'LOI_VAO_KHU';
GO

/* 3. Nguồn vị trí CAMERA_KHU (camera lối vào khu ghi xe vào / ra khu) */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_lg_vi_tri_nguon')
    ALTER TABLE dbo.luot_gui DROP CONSTRAINT CK_lg_vi_tri_nguon;
ALTER TABLE dbo.luot_gui ADD CONSTRAINT CK_lg_vi_tri_nguon
    CHECK (vi_tri_nguon IS NULL OR vi_tri_nguon IN ('QUET', 'CAMERA_KHU', 'NGUOI_DUNG', 'ADMIN'));
GO
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_vtx_nguon')
    ALTER TABLE dbo.vi_tri_xe DROP CONSTRAINT CK_vtx_nguon;
ALTER TABLE dbo.vi_tri_xe ADD CONSTRAINT CK_vtx_nguon
    CHECK (nguon IN ('QUET', 'CAMERA_KHU', 'NGUOI_DUNG', 'ADMIN'));
GO

/* 4. Hướng đi qua lối vào khu */
IF COL_LENGTH('dbo.vi_tri_xe', 'huong') IS NULL
    ALTER TABLE dbo.vi_tri_xe ADD huong VARCHAR(3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_vtx_huong')
    ALTER TABLE dbo.vi_tri_xe ADD CONSTRAINT CK_vtx_huong CHECK (huong IS NULL OR huong IN ('VAO', 'RA'));
-- Camera lối vào khu phải ghi camera, biển đọc được và hướng
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_vtx_camera_khu')
    ALTER TABLE dbo.vi_tri_xe ADD CONSTRAINT CK_vtx_camera_khu
        CHECK (nguon <> 'CAMERA_KHU' OR (camera_id IS NOT NULL AND bien_so_doc_duoc IS NOT NULL AND huong IS NOT NULL));
GO

/* 5. Giữ suất trong khu (module TimXe) */
IF OBJECT_ID('dbo.giu_cho_khu') IS NULL
BEGIN
    CREATE TABLE dbo.giu_cho_khu (
        id               INT IDENTITY(1,1) NOT NULL,
        luot_gui_id      INT            NOT NULL,
        khu_vuc_id       INT            NOT NULL,
        bat_dau_luc      DATETIME2(0)   NOT NULL CONSTRAINT DF_gck_bat_dau DEFAULT SYSDATETIME(),
        het_han_luc      DATETIME2(0)   NOT NULL,
        trang_thai       VARCHAR(10)    NOT NULL CONSTRAINT DF_gck_trang_thai DEFAULT 'DANG_GIU',
        ket_thuc_luc     DATETIME2(0)   NULL,
        CONSTRAINT PK_giu_cho_khu PRIMARY KEY (id),
        -- DANG_GIU: đang giữ; DA_VAO: camera / người dùng đã ghi xe vào khu; HET_HAN: quá phút giữ; HUY: xe ra cổng
        CONSTRAINT CK_gck_trang_thai CHECK (trang_thai IN ('DANG_GIU', 'DA_VAO', 'HET_HAN', 'HUY')),
        CONSTRAINT CK_gck_het_han CHECK (het_han_luc > bat_dau_luc),
        CONSTRAINT CK_gck_ket_thuc CHECK ((trang_thai = 'DANG_GIU' AND ket_thuc_luc IS NULL)
                                       OR (trang_thai <> 'DANG_GIU' AND ket_thuc_luc IS NOT NULL)),
        CONSTRAINT FK_gck_luot_gui FOREIGN KEY (luot_gui_id) REFERENCES dbo.luot_gui(id),
        CONSTRAINT FK_gck_khu_vuc FOREIGN KEY (khu_vuc_id) REFERENCES dbo.khu_vuc(id)
    );
    -- mỗi lượt gửi tối đa 1 suất đang giữ
    CREATE UNIQUE INDEX UX_gck_mot_suat_dang_giu ON dbo.giu_cho_khu(luot_gui_id) WHERE trang_thai = 'DANG_GIU';
    -- đếm suất đang giữ theo khu (chỗ còn = sức chứa − xe trong khu − suất DANG_GIU)
    CREATE INDEX IX_gck_khu_dang_giu ON dbo.giu_cho_khu(khu_vuc_id) INCLUDE (het_han_luc) WHERE trang_thai = 'DANG_GIU';
END
GO

/* 6. Tham số mới */
MERGE dbo.cau_hinh AS d
USING (VALUES
    ('phut_giu_cho_khu',    N'5',  N'Giữ 1 suất trong khu bấy nhiêu phút sau khi xe vào cổng'),
    ('giay_chong_doc_lap',  N'30', N'Cùng biển, cùng camera lối vào khu trong bấy nhiêu giây thì bỏ qua'),
    ('phan_tram_sap_day',   N'20', N'Chỗ còn ≤ bấy nhiêu % sức chứa khu thì hiện "Sắp đầy"'),
    ('phut_chua_dinh_vi',   N'15', N'Xe vào cổng quá bấy nhiêu phút chưa có khu thì vào danh sách chưa định vị')
) AS s (khoa, gia_tri, mo_ta)
ON d.khoa = s.khoa
WHEN NOT MATCHED THEN INSERT (khoa, gia_tri, mo_ta) VALUES (s.khoa, s.gia_tri, s.mo_ta);
GO

PRINT N'V004 chia khu: xong';
GO
