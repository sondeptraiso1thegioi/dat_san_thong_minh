using NhaXe.Modules.CongRaVao.Application;
using NhaXe.Modules.CongRaVao.Domain;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.CongRaVao;

/// <summary>Kho giả trong bộ nhớ. Id do "DB" cấp lúc lưu, giống IDENTITY.</summary>
public class FakeCongRaVaoRepository : ICongRaVaoRepository, ILuotGuiQuery
{
    public Task<IReadOnlyList<NhaXe.Modules.CongRaVao.Contracts.LuotGuiDangMo>> LayDangMoAsync(IReadOnlyCollection<int>? xeIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NhaXe.Modules.CongRaVao.Contracts.LuotGuiDangMo>>(Luot.Where(l => l.DangMo && (xeIds is null || xeIds.Contains(l.XeId)))
            .Select(l => new NhaXe.Modules.CongRaVao.Contracts.LuotGuiDangMo(l.Id, l.XeId, l.ThoiGianVao, l.ViTriKhuId, null, l.ViTriLuc)).ToList());

    public List<LuotGui> Luot { get; } = new();
    /// <summary>Yêu cầu "đã lưu trong DB". Đọc ra là một BẢN SAO (như EF đọc từ DB), để thử lại sau xung đột thấy dữ liệu gốc.</summary>
    public List<YeuCauXacNhan> YeuCau { get; } = new();
    private readonly Dictionary<int, YeuCauXacNhan> _dangTheoDoi = new();
    private static YeuCauXacNhan Sao(YeuCauXacNhan y) =>
        (YeuCauXacNhan)typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(y, null)!;
    private YeuCauXacNhan? TheoDoi(YeuCauXacNhan? goc)
    {
        if (goc is null) return null;
        if (!_dangTheoDoi.TryGetValue(goc.Id, out var y)) _dangTheoDoi[goc.Id] = y = Sao(goc);
        return y;
    }
    public List<SuKienCong> SuKien { get; } = new();
    private readonly List<object> _cho = new();
    private int _id = 100;
    /// <summary>Số lần LuuAsync tiếp theo sẽ báo "người khác vừa sửa" (giả lập ROWVERSION lệch).</summary>
    public int SoLanXungDot { get; set; }
    public int SoLanLuu { get; private set; }
    public int SoLanBoTheoDoi { get; private set; }

    public static void DatId(object o, int id) => o.GetType().GetProperty("Id")!.SetValue(o, id);

    public LuotGui CoLuot(int xeId, DateTime vao, string trangThai = TrangThaiLuot.DangGui)
    {
        var l = LuotGui.Mo(xeId, vao, 0.98m);
        DatId(l, ++_id);
        if (trangThai == TrangThaiLuot.NghiMat) { l.BatDauChoXacNhan(null); l.HuyChoRa(true); }
        Luot.Add(l);
        return l;
    }

    public Task<LuotGui?> TimLuotAsync(int id, CancellationToken ct) => Task.FromResult(Luot.FirstOrDefault(x => x.Id == id));
    public Task<LuotGui?> TimLuotMoCuaXeAsync(int xeId, CancellationToken ct) => Task.FromResult(Luot.FirstOrDefault(x => x.XeId == xeId && x.DangMo));
    public Task<YeuCauXacNhan?> TimYeuCauAsync(int id, CancellationToken ct) => Task.FromResult(TheoDoi(YeuCau.FirstOrDefault(x => x.Id == id)));
    public Task<YeuCauXacNhan?> TimYeuCauDangChoAsync(int luotGuiId, CancellationToken ct) =>
        Task.FromResult(TheoDoi(YeuCau.FirstOrDefault(x => x.LuotGuiId == luotGuiId && x.DangCho)));
    public Task<IReadOnlyList<int>> LayIdYeuCauQuaHanAsync(DateTime bayGio, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<int>>(YeuCau.Where(x => x.DangCho && x.HetHanLuc <= bayGio).Select(x => x.Id).ToList());

    public void Them(LuotGui luot) => _cho.Add(luot);
    public void Them(YeuCauXacNhan yeuCau) => _cho.Add(yeuCau);
    public void Them(SuKienCong suKien) => _cho.Add(suKien);
    public Task<bool> CoSuKienTuAsync(string loai, string bienSo, DateTime tu, CancellationToken ct) =>
        Task.FromResult(SuKien.Any(x => x.Loai == loai && x.BienSo == bienSo && x.ThoiGian >= tu));
    public void BoTheoDoi() { SoLanBoTheoDoi++; _cho.Clear(); _dangTheoDoi.Clear(); }

    public Task LuuAsync(CancellationToken ct)
    {
        if (SoLanXungDot > 0)
        {
            SoLanXungDot--;
            throw new LoiNghiepVu(LoaiLoi.XungDot, "YEU_CAU_DA_THAY_DOI", "giả lập");
        }
        SoLanLuu++;
        foreach (var o in _cho.OrderBy(o => o is SuKienCong)) // lượt gửi trước, nhật ký sau (như EF)
        {
            DatId(o, ++_id);
            switch (o)
            {
                case LuotGui l: Luot.Add(l); break;
                case YeuCauXacNhan y: YeuCau.Add(y); break;
                case SuKienCong s:
                    if (s.Luot is not null) s.GetType().GetProperty("LuotGuiId")!.SetValue(s, s.Luot.Id);
                    SuKien.Add(s); break;
            }
        }
        _cho.Clear();
        foreach (var y in _dangTheoDoi.Values) YeuCau[YeuCau.FindIndex(g => g.Id == y.Id)] = y; // ghi thay đổi xuống "DB"
        _dangTheoDoi.Clear();
        return Task.CompletedTask;
    }
}

public class FakePhatSuKien : IPhatSuKien
{
    public List<ISuKien> DaPhat { get; } = new();
    public Task PhatAsync<T>(T suKien, CancellationToken ct = default) where T : ISuKien { DaPhat.Add(suKien); return Task.CompletedTask; }
}

public class FakeCauHinhCong : ICauHinhCong
{
    public int SoGiay { get; set; } = 60;
    public int GiayAi { get; set; } = 3;
    public Task<int> LaySoGiayChoXacNhanAsync(CancellationToken ct) => Task.FromResult(SoGiay);
    public Task<int> LayGiayGioiHanAiAsync(CancellationToken ct) => Task.FromResult(GiayAi);
}

public class FakeKhoAnhCong : NhaXe.Modules.CongRaVao.Application.IKhoAnhCong
{
    public Dictionary<string, byte[]> Anh { get; } = new();
    public Task<string> LuuAsync(byte[] anh, string loaiAnh, DateTime luc, CancellationToken ct)
    {
        var khoa = $"cong/{luc:yyyyMMdd}/{Guid.NewGuid():N}.jpg";
        Anh[khoa] = anh;
        return Task.FromResult(khoa);
    }
    public Task<Stream?> MoAsync(string khoa, CancellationToken ct) =>
        Task.FromResult<Stream?>(Anh.TryGetValue(khoa, out var b) ? new MemoryStream(b) : null);
}

public class FakeBarie : NhaXe.Modules.CongRaVao.Application.IBarie
{
    public List<(string Cong, int LuotGuiId)> DaMo { get; } = new();
    public Task MoAsync(string cong, int luotGuiId, CancellationToken ct) { DaMo.Add((cong, luotGuiId)); return Task.CompletedTask; }
}
