using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.Modules.NhomGiaDinh.Contracts;
using NhaXe.Modules.TaiKhoan.Contracts;
using NhaXe.Modules.ThongBao.Contracts;
using NhaXe.Modules.GoiCho.Application;
using NhaXe.Modules.GoiCho.Domain;
using NhaXe.Modules.TaiKhoan.Application;
using NhaXe.SharedKernel;
using GoiChoEntity = NhaXe.Modules.GoiCho.Domain.GoiCho;
using TK = NhaXe.Modules.TaiKhoan.Domain.TaiKhoan;

namespace NhaXe.Tests;

public class FakeAiBienSoModule : NhaXe.Modules.AiBienSo.Contracts.IAiBienSoModule
{
    public NhaXe.Modules.AiBienSo.Contracts.KetQuaDocBienSo KetQua { get; set; } = new(null, 0, true, []);
    /// <summary>Giả lập mô hình lỗi (ném) hoặc chạy chậm (đề cương v10: lỗi / quá 3 giây thì chế độ thủ công).</summary>
    public bool Loi { get; set; }
    public TimeSpan Cham { get; set; }
    public async Task<NhaXe.Modules.AiBienSo.Contracts.KetQuaDocBienSo> DocAsync(NhaXe.Modules.AiBienSo.Contracts.AnhBienSo anh, CancellationToken ct = default)
    {
        if (Loi) throw new InvalidOperationException("ONNX lỗi");
        if (Cham > TimeSpan.Zero) await Task.Delay(Cham, CancellationToken.None);
        return KetQua;
    }
}

// Cài đặt giả trong bộ nhớ: thay được cho cài đặt EF Core thật (LSP), nên test nghiệp vụ không cần SQL Server.

public class FakeXeRepository : IXeRepository
{
    public List<Xe> Ds { get; } = new();
    public Task<Xe?> TimAsync(int xeId, CancellationToken ct) => Task.FromResult(Ds.FirstOrDefault(x => x.Id == xeId));
    public Task<bool> TonTaiBienSoAsync(string bienSo, CancellationToken ct) => Task.FromResult(Ds.Any(x => x.BienSo == bienSo));
    public Task<IReadOnlyList<Xe>> LayChoDuyetAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Xe>>(Ds.Where(x => x.TrangThaiDuyet == TrangThaiDuyet.ChoDuyet).ToList());
    public Task<int> DemChoDuyetCuaAsync(int taiKhoanId, CancellationToken ct) =>
        Task.FromResult(Ds.Count(x => x.ChuXeId == taiKhoanId && x.TrangThaiDuyet == TrangThaiDuyet.ChoDuyet));
    public void Them(Xe xe) => Ds.Add(xe);
    public Task LuuAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Cấu hình giả: trả giá trị đặt sẵn, không có thì trả mặc định.</summary>
public class FakeCauHinh : ICauHinh
{
    public Dictionary<string, int> GiaTri { get; } = new();
    public Task<int> LaySoNguyenAsync(string khoa, int macDinh, CancellationToken ct) =>
        Task.FromResult(GiaTri.TryGetValue(khoa, out var v) ? v : macDinh);
}

public class FakeGoiChoRepository : IGoiChoRepository
{
    public List<GoiChoEntity> Ds { get; } = new();
    public Task<GoiChoEntity?> TimAsync(int goiId, CancellationToken ct) => Task.FromResult(Ds.FirstOrDefault(g => g.Id == goiId));
    public Task<IReadOnlyList<GoiChoEntity>> LayDangBanAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<GoiChoEntity>>(Ds.Where(g => g.DangBan).ToList());
    public Task<bool> TonTaiLoaiXeAsync(int loaiXeId, CancellationToken ct) => Task.FromResult(loaiXeId is 1 or 2);
    public Task<string?> LayMaLoaiXeAsync(int loaiXeId, CancellationToken ct) => Task.FromResult(loaiXeId switch { 1 => "O_TO", 2 => "XE_MAY", _ => (string?)null });
    public Task<IReadOnlyList<GoiChoEntity>> LayTatCaAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<GoiChoEntity>>(Ds);
    public Task<GoiChoEntity?> TimDeSuaAsync(int goiId, CancellationToken ct) => TimAsync(goiId, ct);
    public int SoLanLuu { get; private set; }
    public Task LuuAsync(CancellationToken ct) { SoLanLuu++; return Task.CompletedTask; }
}

public class FakeThanhToan : IThanhToanGateway
{
    public int SoLanThu, SoLanHoan;
    public Task<KetQuaThanhToan> ThuTienAsync(int taiKhoanId, decimal soTien, string noiDung, CancellationToken ct)
    { SoLanThu++; return Task.FromResult(new KetQuaThanhToan(true, "T" + SoLanThu)); }
    public Task HoanTienAsync(string maGiaoDich, CancellationToken ct) { SoLanHoan++; return Task.CompletedTask; }
}

public class FakeDangKyChoStore : IDangKyChoStore
{
    public Exception? NemLoi;
    public int SoLanGoi;
    public Task<DangKyChoKetQua> MuaGoiAsync(int taiKhoanId, int xeId, int goiId, CancellationToken ct)
    {
        SoLanGoi++;
        if (NemLoi is not null) throw NemLoi;
        return Task.FromResult(new DangKyChoKetQua(99, DateTime.Today, DateTime.Today.AddDays(7), 300000, false));
    }
    public Task<GoiDaMua?> LayGoiDaMuaAsync(int id, CancellationToken ct) => Task.FromResult<GoiDaMua?>(null);
    public Task HuyGoiAsync(int t, int id, decimal tien, string? lyDo, string? gopY, CancellationToken ct) => Task.CompletedTask;
    public Task AdminHuyGoiAsync(int a, int id, string lyDo, decimal tien, CancellationToken ct) => Task.CompletedTask;
}

public class FakeClock : IClock { public DateTime BayGio { get; set; } = new(2026, 11, 5, 8, 0, 0); }

public class FakeTaiKhoanRepository : ITaiKhoanRepository
{
    public List<TK> Ds { get; } = new();
    public Task<TK?> TimTheoSdtAsync(string sdt, CancellationToken ct) => Task.FromResult(Ds.FirstOrDefault(t => t.Sdt == sdt));
    public Task<TK?> TimTheoIdAsync(int id, CancellationToken ct) => Task.FromResult(Ds.FirstOrDefault(t => t.Id == id));
    public Task<bool> TonTaiEmailAsync(string email, CancellationToken ct) => Task.FromResult(Ds.Any(t => t.Email == email));
    public void Them(TK taiKhoan) => Ds.Add(taiKhoan);
    public Task<IReadOnlyList<TK>> LayNhieuAsync(IEnumerable<int> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<TK>>(Ds.Where(t => ids.Contains(t.Id)).ToList());
    public Task<IReadOnlyList<TK>> LayTheoVaiTroAsync(string vaiTro, CancellationToken ct) => Task.FromResult<IReadOnlyList<TK>>(Ds.Where(t => t.VaiTro == vaiTro).ToList());
    public Task<IReadOnlyList<TK>> TimKiemAsync(string? tuKhoa, string? trangThai, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TK>>(Ds.Where(t => (tuKhoa == null || t.HoTen.Contains(tuKhoa) || t.Sdt.Contains(tuKhoa))
            && (trangThai == null || t.TrangThai == trangThai)).ToList());
    public Task LuuAsync(CancellationToken ct) => Task.CompletedTask;
}

public class FakeHasher : IPasswordHasher { public string Bam(string matKhau) => "hash:" + matKhau; public bool KiemTra(string matKhau, string hash) => hash == "hash:" + matKhau; }
public class FakeToken : ITokenService { public string TaoToken(TK tk, string phien) => $"token-{tk.Id}-{phien}"; }

/// <summary>Kho tệp giả trong bộ nhớ (thay cho thư mục / MinIO).</summary>
public class FakeLuuTruTep : ILuuTruTep
{
    public Dictionary<string, byte[]> Tep { get; } = new();
    public async Task<string> LuuAsync(string thuMuc, string duoi, Stream noiDung, CancellationToken ct)
    {
        var ms = new MemoryStream(); await noiDung.CopyToAsync(ms, ct);
        var khoa = $"{thuMuc}/{Tep.Count + 1}.{duoi}"; Tep[khoa] = ms.ToArray(); return khoa;
    }
    public Task<Stream?> MoAsync(string khoa, CancellationToken ct) =>
        Task.FromResult<Stream?>(Tep.TryGetValue(khoa, out var b) ? new MemoryStream(b) : null);
}

public class FakeTaiKhoanModule : ITaiKhoanModule
{
    public List<int> AdminIds { get; } = new() { 1 };
    public Task<IReadOnlyDictionary<int, ThongTinLienHe>> LayLienHeAsync(IEnumerable<int> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, ThongTinLienHe>>(ids.Distinct().ToDictionary(i => i, i => new ThongTinLienHe(i, "TK" + i, "090000000" + i, $"tk{i}@vi-du.vn", new DateTime(2026, 1, 1))));
    public Task<IReadOnlyList<int>> LayAdminIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>(AdminIds);
    public Task<IReadOnlyDictionary<int, string>> LayHoTenAsync(IEnumerable<int> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, string>>(ids.Distinct().ToDictionary(i => i, i => "TK" + i));
    public Task<bool> PhienConHieuLucAsync(int id, string phien, CancellationToken ct = default) => Task.FromResult(true);
    public Dictionary<int, string> MaDay { get; } = new();
    public Task<IReadOnlyDictionary<int, string>> LayMaDayAsync(IEnumerable<int> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, string>>(ids.Where(MaDay.ContainsKey).Distinct().ToDictionary(i => i, i => MaDay[i]));
}

// ---------- Bản giả của các module khác (GoiCho / BaiXe chỉ biết chúng qua Contracts nên thay được) ----------

/// <summary>Module CongRaVao giả: đặt sẵn xe nào đang trong bãi.
/// Cũng đóng vai bản sao xe_trong_bai của GoiCho (IXeTrongBaiStore), vì hai nơi phải khớp nhau.</summary>
public class FakeCongRaVaoModule : ICongRaVaoModule, IXeTrongBaiStore
{
    public Dictionary<int, LuotGuiDangMo> TheoXe { get; } = new();
    public int SoLanHoi { get; private set; }
    public FakeCongRaVaoModule CoXeTrongBai(int xeId, int luotGuiId, int? khuId = null, int? oId = null)
    { TheoXe[xeId] = new LuotGuiDangMo(luotGuiId, xeId, new DateTime(2026, 11, 5, 7, 0, 0), khuId, oId, null); return this; }
    public Task<IReadOnlyList<LuotGuiDangMo>> LayLuotGuiDangMoAsync(CancellationToken ct = default)
    { SoLanHoi++; return Task.FromResult<IReadOnlyList<LuotGuiDangMo>>(TheoXe.Values.ToList()); }
    public Task<IReadOnlyDictionary<int, LuotGuiDangMo>> LayLuotGuiDangMoCuaXeAsync(IEnumerable<int> xeIds, CancellationToken ct = default)
    { SoLanHoi++; return Task.FromResult<IReadOnlyDictionary<int, LuotGuiDangMo>>(xeIds.Where(TheoXe.ContainsKey).Distinct().ToDictionary(i => i, i => TheoXe[i])); }

    public List<(int LuotGuiId, int? KhuId, string? Nguon, DateTime Luc)> DaGhiViTri { get; } = new();
    public Task GhiViTriAsync(int luotGuiId, int? khuId, string? nguon, DateTime luc, CancellationToken ct = default)
    {
        DaGhiViTri.Add((luotGuiId, khuId, nguon, luc));
        foreach (var (xe, l) in TheoXe.ToList())
            if (l.LuotGuiId == luotGuiId) TheoXe[xe] = l with { ViTriKhuId = khuId, ViTriLuc = khuId is null ? null : luc };
        return Task.CompletedTask;
    }
    public ThongKeCong ThongKe { get; set; } = new(0, 0, 0, 0, 0, 0, null);
    public Task<ThongKeCong> LayThongKeAsync(DateTime tu, DateTime den, CancellationToken ct = default) => Task.FromResult(ThongKe);

    // ----- IXeTrongBaiStore (phía GoiCho) -----
    public Task<IReadOnlyDictionary<int, int>> LayAsync(IReadOnlyCollection<int> xeIds, CancellationToken ct)
    { SoLanHoi++; return Task.FromResult<IReadOnlyDictionary<int, int>>(xeIds.Where(TheoXe.ContainsKey).Distinct().ToDictionary(i => i, i => TheoXe[i].LuotGuiId)); }
    public Task GhiVaoAsync(int xeId, int luotGuiId, DateTime vaoLuc, CancellationToken ct)
    { TheoXe[xeId] = new LuotGuiDangMo(luotGuiId, xeId, vaoLuc, null, null, null); return Task.CompletedTask; }
    public Task GhiRaAsync(int xeId, int luotGuiId, CancellationToken ct)
    { if (TheoXe.TryGetValue(xeId, out var l) && l.LuotGuiId == luotGuiId) TheoXe.Remove(xeId); return Task.CompletedTask; }
}

/// <summary>Module NhomGiaDinh giả: đặt sẵn ai cùng nhóm với ai.</summary>
public class FakeNhomGiaDinhModule : INhomGiaDinhModule
{
    public Dictionary<int, List<int>> CungNhom { get; } = new();
    public Task<IReadOnlyList<int>> LayNguoiCungNhomAsync(int taiKhoanId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<int>>(CungNhom.GetValueOrDefault(taiKhoanId) ?? new List<int>());
}

/// <summary>Module ThongBao giả: ghi lại mọi thông báo được gửi để test kiểm tra.</summary>
public class FakeThongBaoModule : IThongBaoModule
{
    /// <summary>TaiKhoanId null = gửi mọi Admin. Dich: hành động + tham chiếu (bấm vào thì tới đâu).</summary>
    public List<(int? TaiKhoanId, string Loai, string NoiDung, DichThongBao? Dich)> DaGui { get; } = new();
    public Task GuiAsync(int taiKhoanId, string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default)
    { DaGui.Add((taiKhoanId, loai, noiDung, dich)); return Task.CompletedTask; }
    public Task GuiChoMoiAdminAsync(string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default)
    { DaGui.Add((null, loai, noiDung, dich)); return Task.CompletedTask; }
    public Task GuiNhieuAsync(IEnumerable<int> taiKhoanIds, string loai, string noiDung, DichThongBao? dich = null, CancellationToken ct = default)
    { foreach (var id in taiKhoanIds.Distinct()) DaGui.Add((id, loai, noiDung, dich)); return Task.CompletedTask; }
}

public class FakePhiQuaHanStore : IPhiQuaHanStore
{
    public Dictionary<int, PhiQuaHanDto> TheoLuot { get; } = new();
    public Task<IReadOnlyList<PhiQuaHanDto>> LayNoAsync(IReadOnlyCollection<int> luotGuiIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PhiQuaHanDto>>(luotGuiIds.Where(TheoLuot.ContainsKey).Select(i => TheoLuot[i]).ToList());
    public Task ThanhToanAsync(int taiKhoanId, int luotGuiId, CancellationToken ct) => Task.CompletedTask;
}

public static class TaoModule
{
    public static TraCuuTrongBai TraCuu(FakeCongRaVaoModule? cong = null, FakePhiQuaHanStore? phi = null) =>
        new(cong ?? new FakeCongRaVaoModule(), phi ?? new FakePhiQuaHanStore());
}
