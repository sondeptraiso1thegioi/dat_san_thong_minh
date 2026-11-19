namespace NhaXe.Modules.ThongBao.Application;

/// <summary>Đọc / ghi bảng thong_bao. Chỉ module ThongBao được đụng bảng này.</summary>
public interface IThongBaoStore
{
    /// <summary>100 thông báo mới nhất của tài khoản. cacLoai = null nghĩa là mọi loại.</summary>
    Task<IReadOnlyList<ThongBaoDto>> LayAsync(int taiKhoanId, IReadOnlyCollection<string>? cacLoai, CancellationToken ct);
    Task<int> DemChuaDocAsync(int taiKhoanId, CancellationToken ct);
    /// <returns>false nếu thông báo không thuộc tài khoản này.</returns>
    Task<bool> DanhDauDaDocAsync(int taiKhoanId, int thongBaoId, CancellationToken ct);
    /// <returns>Số thông báo vừa được đánh dấu đã đọc.</returns>
    Task<int> DocHetAsync(int taiKhoanId, CancellationToken ct);
    /// <summary>
    /// Ghi một thông báo cho từng tài khoản. Chống gửi lặp: tài khoản đã nhận đúng thông báo đó (cùng loại, cùng nội dung,
    /// cùng tham chiếu) trong <paramref name="chongLapGiay"/> giây thì bỏ qua. Trả về các bản ghi THỰC SỰ đã thêm.
    /// </summary>
    Task<IReadOnlyList<TinDaLuu>> ThemAsync(IReadOnlyList<int> taiKhoanIds, string loai, string noiDung, string? hanhDong, int? thamChieuId,
                                            int chongLapGiay, CancellationToken ct);
}

/// <summary>Một thông báo vừa lưu: id để đẩy SignalR (bấm thẻ nổi thì đánh dấu đúng thông báo đó đã đọc).</summary>
public record TinDaLuu(int TaiKhoanId, int Id);
