/* =====================================================================
   V005: xác nhận xe ra theo đề cương v10 (UC-U08)
   - Người có quyền dùng xe trả lời TRƯỚC là có hiệu lực, không cần quản lý đồng ý nữa.
     CK_ycxn_dong_y cũ bắt cả chu_xe_dong_y = 1 VÀ quan_ly_dong_y = 1, nay chỉ cần MỘT bên:
     người dùng xe bấm "Đúng là tôi", hoặc Admin mở tay (quan_ly_*).
   - Hết 60 giây: Admin mở tay ghi ở su_kien_cong (admin_xu_ly_id + ly_do); yêu cầu còn chờ thì đóng bằng cột quan_ly_*.
   Chạy lại nhiều lần vẫn an toàn.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ycxn_dong_y')
    ALTER TABLE dbo.yeu_cau_xac_nhan DROP CONSTRAINT CK_ycxn_dong_y;
GO
ALTER TABLE dbo.yeu_cau_xac_nhan ADD CONSTRAINT CK_ycxn_dong_y
    CHECK (ket_qua <> 'DONG_Y' OR chu_xe_dong_y = 1 OR quan_ly_dong_y = 1);
GO
/* Nhắc hạn theo v10: lúc hết hạn (1 lần) và mỗi ngày quá hạn (lưu ngày nhắc gần nhất) */
IF COL_LENGTH('dbo.dang_ky_cho', 'da_nhac_het_han') IS NULL
    ALTER TABLE dbo.dang_ky_cho ADD da_nhac_het_han BIT NOT NULL CONSTRAINT DF_dkc_nhac_het_han DEFAULT 0;
IF COL_LENGTH('dbo.dang_ky_cho', 'ngay_nhac_qua_han') IS NULL
    ALTER TABLE dbo.dang_ky_cho ADD ngay_nhac_qua_han DATE NULL;
GO
/* Loại thông báo mới: KHU (chỉ dẫn / ghi nhận khu), NHOM (nhóm gia đình), DOI_SOAT (Admin: biển lạ, lệch khu, chưa định vị),
   CHO_THANH_TOAN (xác nhận ra nhưng còn phí quá hạn) */
-- Chỉ làm khi ràng buộc chưa có các loại này: migration sau (V011) mở rộng thêm loại, chạy lại V005 không được ghi đè bản rộng hơn
-- (ghi đè sẽ lỗi khi đã có thông báo loại mới).
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_tb_loai' AND definition LIKE '%CHO_THANH_TOAN%')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_tb_loai')
        ALTER TABLE dbo.thong_bao DROP CONSTRAINT CK_tb_loai;
    ALTER TABLE dbo.thong_bao ADD CONSTRAINT CK_tb_loai CHECK (loai IN ('VAO', 'RA', 'SAP_HET_HAN', 'HET_HAN', 'QUA_HAN',
        'NGHI_MAT', 'HUY_GOI', 'XE_BI_GO', 'KHACH_HUY', 'KHU', 'NHOM', 'DOI_SOAT', 'CHO_THANH_TOAN'));
END
GO
MERGE dbo.cau_hinh AS d
USING (VALUES
    ('giay_gioi_han_ai',  N'3',    N'AI xử lý quá bấy nhiêu giây thì cổng chuyển chế độ thủ công')
) AS s (khoa, gia_tri, mo_ta)
ON d.khoa = s.khoa
WHEN NOT MATCHED THEN INSERT (khoa, gia_tri, mo_ta) VALUES (s.khoa, s.gia_tri, s.mo_ta);
GO
PRINT N'V005 xác nhận người trả lời trước: xong';
GO
