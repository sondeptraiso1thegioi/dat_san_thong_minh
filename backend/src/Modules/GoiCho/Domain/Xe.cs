using NhaXe.SharedKernel;

namespace NhaXe.Modules.GoiCho.Domain;

public static class TrangThaiDuyet
{
    public const string ChoDuyet = "CHO_DUYET";
    public const string DaDuyet = "DA_DUYET";
    public const string TuChoi = "TU_CHOI";
    public const string BiGo = "BI_GO";
}

/// <summary>Thực thể Xe và các quy tắc của nó. Không phụ thuộc EF Core, ASP.NET hay SQL Server.</summary>
public class Xe
{
    public int Id { get; private set; }
    public int ChuXeId { get; private set; }
    public int LoaiXeId { get; private set; }
    public string BienSo { get; private set; } = "";
    public string? AnhGiayDangKy { get; private set; }
    public string TrangThaiDuyet { get; private set; } = Domain.TrangThaiDuyet.ChoDuyet;
    public bool ChiaSeNhom { get; private set; }
    public DateTime TaoLuc { get; private set; }
    public string? LyDoGo { get; private set; }
    public string? LyDoTuChoi { get; private set; }
    /// <summary>Lúc Admin duyệt và Admin nào duyệt (V011). Xe duyệt trước khi có cột thì null.</summary>
    public DateTime? DuyetLuc { get; private set; }
    public int? DuyetBoiId { get; private set; }

    private Xe() { } // cho EF Core

    /// <summary>Mặc định số xe được chờ duyệt cùng lúc của 1 tài khoản (cau_hinh.so_xe_cho_duyet_toi_da).</summary>
    public const int SoXeChoDuyetToiDaMacDinh = 5;

    /// <summary>Chặn spam danh sách duyệt: chỉ đếm xe ĐANG chờ duyệt; xe đã duyệt / bị từ chối không tính.</summary>
    public static void KiemTraGioiHanChoDuyet(int soXeDangChoDuyet, int toiDa)
    {
        if (soXeDangChoDuyet >= toiDa)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "QUA_SO_XE_CHO_DUYET",
                $"Bạn đang có {soXeDangChoDuyet} xe chờ duyệt (tối đa {toiDa}). Vui lòng đợi Admin xử lý rồi đăng ký thêm.");
    }

    /// <summary>Đăng ký xe mới: luôn bắt đầu ở trạng thái chờ Admin duyệt.</summary>
    public static Xe DangKyMoi(int chuXeId, int loaiXeId, BienSo bienSo, bool chiaSeNhom, DateTime luc) => new()
    {
        ChuXeId = chuXeId,
        LoaiXeId = loaiXeId,
        BienSo = bienSo.GiaTri,
        ChiaSeNhom = chiaSeNhom,
        TrangThaiDuyet = Domain.TrangThaiDuyet.ChoDuyet,
        TaoLuc = luc
    };

    /// <summary>Dựng lại xe đã có (dùng cho test và nạp dữ liệu).</summary>
    public static Xe DungLai(int id, int chuXeId, int loaiXeId, string bienSo, string trangThaiDuyet,
                             string? anhGiayDangKy = null, string? lyDoTuChoi = null, bool chiaSeNhom = false,
                             DateTime? duyetLuc = null, int? duyetBoiId = null, DateTime? taoLuc = null) => new()
    {
        Id = id, ChuXeId = chuXeId, LoaiXeId = loaiXeId, BienSo = bienSo, TrangThaiDuyet = trangThaiDuyet,
        AnhGiayDangKy = anhGiayDangKy, ChiaSeNhom = chiaSeNhom, DuyetLuc = duyetLuc, DuyetBoiId = duyetBoiId,
        TaoLuc = taoLuc ?? default,
        LyDoTuChoi = trangThaiDuyet == Domain.TrangThaiDuyet.TuChoi ? (lyDoTuChoi ?? "Không rõ") : null
    };

    /// <summary>
    /// Ai được DÙNG xe (xem, tìm, lấy xe ra): chủ xe, hoặc người cùng nhóm khi xe bật chia sẻ nhóm.
    /// Danh sách "người cùng nhóm" do module NhomGiaDinh trả lời; quy tắc nằm ở đây nên unit test được.
    /// </summary>
    public bool DuocDungBoi(int taiKhoanId, IReadOnlyCollection<int> nguoiCungNhom) =>
        ChuXeId == taiKhoanId || (ChiaSeNhom && nguoiCungNhom.Contains(ChuXeId));

    /// <summary>Quy tắc v6: chỉ CHỦ XE được mua gói (thành viên nhóm không mua hộ), và xe phải đã được duyệt.</summary>
    public void KiemTraDuocMuaGoi(int taiKhoanId)
    {
        if (ChuXeId != taiKhoanId)
            throw new LoiNghiepVu(LoaiLoi.KhongCoQuyen, "KHONG_PHAI_CHU_XE", "Chỉ chủ xe được mua gói cho xe này.");
        if (TrangThaiDuyet == Domain.TrangThaiDuyet.BiGo)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "XE_BI_GO", $"Xe đã bị Admin gỡ. Lý do: {LyDoGo}");
        if (TrangThaiDuyet == Domain.TrangThaiDuyet.TuChoi)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "XE_BI_TU_CHOI",
                $"Xe bị từ chối duyệt nên chưa mua gói được. Lý do: {LyDoTuChoi}. Hãy tải lại ảnh giấy đăng ký để gửi duyệt lại.");
        if (TrangThaiDuyet != Domain.TrangThaiDuyet.DaDuyet)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "XE_CHUA_DUYET", "Xe chưa được Admin duyệt nên chưa mua gói được.");
    }

    /// <summary>Quy tắc gỡ xe phạm quy: chỉ xe đã duyệt, phải có lý do rõ ràng. Gỡ MỀM, không xoá xe.</summary>
    public void KiemTraGoDuoc(string? lyDo)
    {
        if (TrangThaiDuyet != Domain.TrangThaiDuyet.DaDuyet)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "KHONG_GO_DUOC", "Chỉ gỡ được xe đang ở trạng thái đã duyệt.");
        if ((lyDo ?? "").Trim().Length < 5)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "THIEU_LY_DO", "Lý do gỡ xe phải có ít nhất 5 ký tự.");
    }

    /// <summary>Duyệt: Admin phải có ảnh giấy đăng ký để đối chiếu với biển số khách nhập. Ghi lại ai duyệt, lúc nào.</summary>
    public void Duyet(int adminId, DateTime luc)
    {
        KiemTraDangChoDuyet();
        if (string.IsNullOrEmpty(AnhGiayDangKy))
            throw new LoiNghiepVu(LoaiLoi.XungDot, "THIEU_ANH_GIAY_DK",
                $"Xe {BienSo} chưa có ảnh giấy đăng ký nên chưa duyệt được.");
        TrangThaiDuyet = Domain.TrangThaiDuyet.DaDuyet;
        DuyetLuc = luc;
        DuyetBoiId = adminId;
    }

    /// <summary>Từ chối: bắt buộc có lý do (chủ xe đọc được để sửa và gửi lại).</summary>
    public void TuChoi(string? lyDo)
    {
        KiemTraDangChoDuyet();
        var ld = ChuanHoaLyDoTuChoi(lyDo); // kiểm tra TRƯỚC khi đổi trạng thái: lý do sai thì xe giữ nguyên "chờ duyệt"
        TrangThaiDuyet = Domain.TrangThaiDuyet.TuChoi;
        LyDoTuChoi = ld;
    }

    public static string ChuanHoaLyDoTuChoi(string? lyDo)
    {
        var s = (lyDo ?? "").Trim();
        if (s.Length < 5)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "THIEU_LY_DO", "Lý do từ chối phải có ít nhất 5 ký tự.");
        if (s.Length > 255)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "LY_DO_QUA_DAI", "Lý do từ chối tối đa 255 ký tự.");
        return s;
    }

    /// <summary>
    /// Chủ xe tải (lại) ảnh giấy đăng ký. Xe đang chờ duyệt: thay ảnh.
    /// Xe bị từ chối: thay ảnh và GỬI LẠI (quay về chờ duyệt, xoá lý do từ chối).
    /// Trả về true nếu đây là lần gửi lại (để use case kiểm tra giới hạn số xe chờ duyệt).
    /// </summary>
    public bool GanAnhGiayDangKy(int taiKhoanId, string khoaTep)
    {
        var guiLai = KiemTraDoiAnhDuoc(taiKhoanId);
        AnhGiayDangKy = khoaTep;
        if (guiLai) { TrangThaiDuyet = Domain.TrangThaiDuyet.ChoDuyet; LyDoTuChoi = null; }
        return guiLai;
    }

    /// <summary>Kiểm tra trước khi lưu tệp: đúng chủ xe, xe đang chờ duyệt hoặc bị từ chối. Trả về true nếu là gửi lại.</summary>
    public bool KiemTraDoiAnhDuoc(int taiKhoanId)
    {
        if (ChuXeId != taiKhoanId)
            throw new LoiNghiepVu(LoaiLoi.KhongCoQuyen, "KHONG_PHAI_CHU_XE", "Chỉ chủ xe được tải ảnh giấy đăng ký.");
        if (TrangThaiDuyet == Domain.TrangThaiDuyet.TuChoi) return true;
        if (TrangThaiDuyet != Domain.TrangThaiDuyet.ChoDuyet)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "KHONG_DOI_ANH_DUOC", "Chỉ đổi ảnh khi xe đang chờ duyệt hoặc bị từ chối.");
        return false;
    }

    /// <summary>Ai được xem ảnh giấy đăng ký: chỉ chủ xe và Admin (giấy tờ có thông tin cá nhân).</summary>
    public void KiemTraXemAnhDuoc(int taiKhoanId, bool laAdmin)
    {
        if (!laAdmin && ChuXeId != taiKhoanId)
            throw new LoiNghiepVu(LoaiLoi.KhongCoQuyen, "KHONG_CO_QUYEN", "Chỉ chủ xe và Admin được xem giấy đăng ký.");
        if (string.IsNullOrEmpty(AnhGiayDangKy))
            throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "CHUA_CO_ANH", "Xe chưa có ảnh giấy đăng ký.");
    }

    /// <summary>
    /// Sau khi gán ảnh, có cần báo Admin "có xe chờ duyệt" không: xe vừa có ảnh lần đầu (Admin cần ảnh mới duyệt được),
    /// hoặc chủ xe gửi lại sau khi bị từ chối. Thay ảnh khi xe vẫn đang chờ duyệt thì không báo lần nữa.
    /// </summary>
    public static bool CanBaoAdmin(bool guiLai, bool coAnhLanDau) => guiLai || coAnhLanDau;

    private void KiemTraDangChoDuyet()
    {
        if (TrangThaiDuyet != Domain.TrangThaiDuyet.ChoDuyet)
            throw new LoiNghiepVu(LoaiLoi.XungDot, "DA_XU_LY", $"Xe {BienSo} đã được xử lý trước đó.");
    }
}
