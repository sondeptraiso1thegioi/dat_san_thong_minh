/* =====================================================================
   V013: TẮT các khu ô tô (Khu A, Khu B của dữ liệu mẫu). Nhóm chưa làm phần ô tô.
   - Tắt, KHÔNG xoá: khu_vuc.dang_dung = 0 và camera của khu đó dang_bat = 0. Ô đỗ (o_do) không có cột riêng,
     bị ẩn theo khu. Lý do giữ dữ liệu: khu được nhiều bảng tham chiếu (camera, o_do, luot_gui, vi_tri_xe...),
     nha-xe.sql tạo lại Khu A, B khi reset DB, và sau này làm ô tô chỉ cần bật lại ở trang Quản lý bãi.
   - Chỉ chạy MỘT lần (đánh dấu 'v013_da_tat_khu_o_to' trong cau_hinh): Admin bật lại khu thì lần chạy sau không tắt nữa.
   - Chỉ đụng camera thuộc khu ô tô (camera giám sát CAM-A1, CAM-B1). Camera cổng không nằm trong các khu này.
   Chạy lại nhiều lần an toàn. db-init.sh tự chạy sau nha-xe.sql.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.cau_hinh WHERE khoa = 'v013_da_tat_khu_o_to')
BEGIN
    BEGIN TRAN;

    DECLARE @khu TABLE (id INT PRIMARY KEY);
    INSERT @khu (id)
    SELECT k.id FROM dbo.khu_vuc k
    WHERE k.dang_dung = 1
      AND k.loai_xe_id = (SELECT id FROM dbo.loai_xe WHERE ten = 'O_TO');

    UPDATE dbo.camera   SET dang_bat = 0  WHERE khu_vuc_id IN (SELECT id FROM @khu);
    UPDATE dbo.khu_vuc  SET dang_dung = 0 WHERE id IN (SELECT id FROM @khu);

    INSERT dbo.cau_hinh (khoa, gia_tri, mo_ta)
    VALUES ('v013_da_tat_khu_o_to', N'1', N'V013 đã tắt các khu ô tô (Admin bật lại được ở Quản lý bãi)');

    COMMIT;
    PRINT N'V013: đã tắt khu ô tô và camera của khu';
END
ELSE
    PRINT N'V013: đã chạy trước đó, bỏ qua (giữ khu Admin đã bật lại)';
GO
