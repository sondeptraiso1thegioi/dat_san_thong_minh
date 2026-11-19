/* =====================================================================
   V010: XOÁ HẲN Khu C, Khu D (khu xe máy của dữ liệu mẫu, không có trên bản đồ thật)
   Bãi xe máy chỉ còn 3 khu theo ảnh sơ đồ: R, S, Hầm B1/B2 (V009). Khu ô tô A, B giữ nguyên.
   Chỉ xoá khu tên "Khu C" / "Khu D" thuộc loại XE_MAY và KHÔNG có mã sơ đồ (không đụng khu Admin gắn mã R/S/HAM).
   Xoá theo thứ tự khoá ngoại, trong 1 transaction:
     vi_tri_xe → giu_cho_khu → luot_gui (gỡ vị trí, giữ lượt gửi) → trang_thai_o / o_do → camera
     → canh_duong_di / nut_duong_di → khu_vuc
   Lượt gửi có ghi vị trí ở C/D vẫn GIỮ (lịch sử ra vào, tiền), chỉ xoá thông tin khu của lượt đó.
   Chạy lại nhiều lần an toàn: không còn Khu C, D thì không làm gì. Trên DB mới (RESET_DB=1),
   nha-xe.sql và V006 tạo Khu C, D rồi V010 xoá, kết quả giống DB đang chạy.
   Bản sao lưu trước khi chạy lần đầu: /var/opt/mssql/backup/NhaXe_truoc_V010_20261118.bak (trong volume sqldata).
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

DECLARE @khu TABLE (id INT PRIMARY KEY);
INSERT @khu (id)
SELECT k.id FROM dbo.khu_vuc k
WHERE k.ten IN (N'Khu C', N'Khu D') AND k.ma_so_do IS NULL
  AND k.loai_xe_id = (SELECT id FROM dbo.loai_xe WHERE ten = 'XE_MAY');

IF EXISTS (SELECT 1 FROM @khu)
BEGIN
    BEGIN TRAN;

    DECLARE @cam TABLE (id INT PRIMARY KEY);
    INSERT @cam (id) SELECT c.id FROM dbo.camera c WHERE c.khu_vuc_id IN (SELECT id FROM @khu);
    DECLARE @o TABLE (id INT PRIMARY KEY);
    INSERT @o (id) SELECT o.id FROM dbo.o_do o WHERE o.khu_vuc_id IN (SELECT id FROM @khu);
    DECLARE @nut TABLE (id INT PRIMARY KEY);
    INSERT @nut (id) SELECT n.id FROM dbo.nut_duong_di n WHERE n.khu_vuc_id IN (SELECT id FROM @khu);

    -- 1. Lịch sử vị trí trong khu / do camera của khu ghi
    DELETE FROM dbo.vi_tri_xe WHERE khu_vuc_id IN (SELECT id FROM @khu) OR camera_id IN (SELECT id FROM @cam);
    -- 2. Suất giữ ở khu
    DELETE FROM dbo.giu_cho_khu WHERE khu_vuc_id IN (SELECT id FROM @khu);
    -- 3. Lượt gửi: giữ lượt, gỡ thông tin khu (CK_lg_vi_tri_du: 4 cột cùng NULL)
    UPDATE dbo.luot_gui SET vi_tri_khu_id = NULL, vi_tri_o_id = NULL, vi_tri_luc = NULL, vi_tri_nguon = NULL
    WHERE vi_tri_khu_id IN (SELECT id FROM @khu);
    -- 4. Ô đỗ của khu (khu xe máy thường không có ô)
    DELETE FROM dbo.trang_thai_o WHERE o_do_id IN (SELECT id FROM @o);
    DELETE FROM dbo.o_do WHERE id IN (SELECT id FROM @o);
    -- 5. Camera của khu (ô ở khu khác lỡ trỏ tới camera này thì gỡ camera khỏi ô)
    UPDATE dbo.o_do SET camera_id = NULL WHERE camera_id IN (SELECT id FROM @cam);
    DELETE FROM dbo.camera WHERE id IN (SELECT id FROM @cam);
    -- 6. Nút "Đầu khu C/D" và các cạnh nối vào nó
    DELETE FROM dbo.canh_duong_di WHERE nut_a_id IN (SELECT id FROM @nut) OR nut_b_id IN (SELECT id FROM @nut);
    DELETE FROM dbo.nut_duong_di WHERE id IN (SELECT id FROM @nut);
    -- 7. Khu
    DELETE FROM dbo.khu_vuc WHERE id IN (SELECT id FROM @khu);

    COMMIT;
    PRINT N'V010: đã xoá Khu C, Khu D xe máy của dữ liệu mẫu';
END
ELSE
    PRINT N'V010: không còn Khu C, Khu D, bỏ qua';
GO
