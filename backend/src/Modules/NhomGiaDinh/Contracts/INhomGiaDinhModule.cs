namespace NhaXe.Modules.NhomGiaDinh.Contracts;

/// <summary>
/// Cổng giao tiếp CÔNG KHAI của module Nhóm gia đình (số 3). Module này sở hữu bảng nhom, thanh_vien_nhom, yeu_cau_tham_gia.
/// GoiCho hỏi "ai cùng nhóm với tài khoản này" để biết ai được dùng xe được chia sẻ; GoiCho không đọc bảng nhóm.
/// API quản lý nhóm dùng các use case nội bộ; Contracts này chỉ cung cấp quyền dùng chung cho module khác.
/// </summary>
public interface INhomGiaDinhModule
{
    /// <summary>Các tài khoản KHÁC cùng nhóm với tài khoản này (rỗng nếu chưa vào nhóm nào).</summary>
    Task<IReadOnlyList<int>> LayNguoiCungNhomAsync(int taiKhoanId, CancellationToken ct = default);
}
