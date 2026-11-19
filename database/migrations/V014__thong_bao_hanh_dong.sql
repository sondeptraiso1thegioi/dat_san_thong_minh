/* =====================================================================
   V014: Thông báo bấm được (đi thẳng tới màn hình cần làm)
   - thong_bao.hanh_dong VARCHAR(30) NULL: việc người nhận cần làm / chỗ cần xem, đặt theo NGHIỆP VỤ
     (XAC_NHAN_RA, MUA_GOI, DUYET_XE...; danh sách ở HanhDongThongBao trong module ThongBao.Contracts).
     KHÔNG chứa đường dẫn trang: frontend tự quy đổi (thong-bao/dichDen.ts).
   - thong_bao.tham_chieu_id INT NULL: id đối tượng liên quan (luot_gui_id, xe_id, di_sai_khu.id... tuỳ hành động).
   - Thông báo cũ để NULL: frontend suy theo loai.
   - Chỉ mục IX_tb_chong_lap: kiểm tra "đã gửi đúng tin này trong 30 giây chưa" khi thêm thông báo.
   Chạy lại nhiều lần an toàn. db-init.sh tự chạy sau nha-xe.sql.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.thong_bao', 'hanh_dong') IS NULL
    ALTER TABLE dbo.thong_bao ADD hanh_dong VARCHAR(30) NULL;
IF COL_LENGTH('dbo.thong_bao', 'tham_chieu_id') IS NULL
    ALTER TABLE dbo.thong_bao ADD tham_chieu_id INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tb_chong_lap' AND object_id = OBJECT_ID('dbo.thong_bao'))
    CREATE INDEX IX_tb_chong_lap ON dbo.thong_bao (tai_khoan_id, loai, gui_luc DESC) INCLUDE (tham_chieu_id);
GO
