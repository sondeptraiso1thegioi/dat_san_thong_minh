/* =====================================================================
   V006: dữ liệu mẫu cho luồng xe máy chia khu (đề cương v10)
   - Thêm "Khu D" (xe máy) để có 2 khu xe máy, thử được việc chọn khu gần cổng còn chỗ.
   - Chia lại sức chứa xe máy (20): Khu C 12, Khu D 8 (Khu D gần cổng hơn, nhỏ hơn).
   - Camera lối vào Khu D, nút "Đầu khu D" nối thẳng với "Lối vào" (8 m).
   Chỉ chạy khi chưa có Khu D (chạy lại an toàn). Chỉ dùng cho môi trường demo.
   ===================================================================== */
USE NhaXeThongMinh;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.khu_vuc WHERE ten = N'Khu D')
   AND EXISTS (SELECT 1 FROM dbo.khu_vuc WHERE ten = N'Khu C')
   AND EXISTS (SELECT 1 FROM dbo.nut_duong_di WHERE ten = N'Lối vào')
BEGIN
    DECLARE @xm INT = (SELECT id FROM dbo.loai_xe WHERE ten = 'XE_MAY');
    DECLARE @tong INT = (SELECT suc_chua FROM dbo.loai_xe WHERE id = @xm);
    INSERT dbo.khu_vuc (ten, loai_xe_id, toa_do_so_do, suc_chua)
    VALUES (N'Khu D', @xm, N'{"x":40,"y":300,"w":240,"h":80}', @tong * 2 / 5);
    DECLARE @d INT = SCOPE_IDENTITY();
    UPDATE dbo.khu_vuc SET suc_chua = @tong - @tong * 2 / 5 WHERE ten = N'Khu C';
    INSERT dbo.camera (khu_vuc_id, ten, nguon_anh, dang_bat, vai_tro)
    VALUES (@d, N'CAM-D-LOI-VAO', '/demo/camera/cam-d-loi-vao.jpg', 1, 'LOI_VAO_KHU');
    INSERT dbo.nut_duong_di (ten, toa_do, khu_vuc_id) VALUES (N'Đầu khu D', N'{"x":30,"y":300}', @d);
    DECLARE @n INT = SCOPE_IDENTITY();
    DECLARE @cong INT = (SELECT MIN(id) FROM dbo.nut_duong_di WHERE ten = N'Lối vào');
    INSERT dbo.canh_duong_di (nut_a_id, nut_b_id, do_dai)
    VALUES (CASE WHEN @cong < @n THEN @cong ELSE @n END, CASE WHEN @cong < @n THEN @n ELSE @cong END, 8.0);
END
GO
PRINT N'V006 dữ liệu mẫu khu xe máy: xong';
GO
