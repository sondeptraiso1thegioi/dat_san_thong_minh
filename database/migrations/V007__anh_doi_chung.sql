/* =====================================================================
   V007: ảnh đối chứng ở cổng (module Hành trình xe, 18/11)
   Lưu khoá tệp ảnh chụp lúc xe vào / xe ra (ảnh nằm trong volume uploads, DB chỉ giữ khoá).
   Cổng ra hiện ảnh vào và ảnh ra cạnh nhau để quản lý đối chiếu.
   Chạy lại nhiều lần vẫn an toàn.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH('dbo.luot_gui', 'anh_vao') IS NULL
    ALTER TABLE dbo.luot_gui ADD anh_vao varchar(120) NULL;
GO
IF COL_LENGTH('dbo.luot_gui', 'anh_ra') IS NULL
    ALTER TABLE dbo.luot_gui ADD anh_ra varchar(120) NULL;
GO
