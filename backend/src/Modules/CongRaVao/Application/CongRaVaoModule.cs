using NhaXe.Modules.CongRaVao.Contracts;

namespace NhaXe.Modules.CongRaVao.Application;

/// <summary>Cài đặt cổng công khai ICongRaVaoModule. Module khác chỉ thấy interface, không thấy class này.</summary>
public class CongRaVaoModule : ICongRaVaoModule
{
    private readonly ILuotGuiQuery _query;
    private readonly ICongRaVaoRepository? _repo;
    private readonly ICongQuery? _cong;
    public CongRaVaoModule(ILuotGuiQuery query, ICongRaVaoRepository? repo = null, ICongQuery? cong = null)
    { _query = query; _repo = repo; _cong = cong; }

    public async Task GhiViTriAsync(int luotGuiId, int? khuId, string? nguon, DateTime luc, CancellationToken ct = default)
    {
        if (_repo is null) return;
        var luot = await _repo.TimLuotAsync(luotGuiId, ct);
        if (luot is null) return;
        luot.GhiViTri(khuId, nguon, luc);
        await _repo.LuuAsync(ct);
    }

    public Task<ThongKeCong> LayThongKeAsync(DateTime tu, DateTime den, CancellationToken ct = default) =>
        _cong?.LayThongKeAsync(tu, den, ct) ?? Task.FromResult(new ThongKeCong(0, 0, 0, 0, 0, 0, null));

    public Task<IReadOnlyList<LuotGuiDangMo>> LayLuotGuiDangMoAsync(CancellationToken ct = default) => _query.LayDangMoAsync(null, ct);

    public async Task<IReadOnlyDictionary<int, LuotGuiDangMo>> LayLuotGuiDangMoCuaXeAsync(IEnumerable<int> xeIds, CancellationToken ct = default)
    {
        var ids = xeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, LuotGuiDangMo>();
        return (await _query.LayDangMoAsync(ids, ct)).ToDictionary(l => l.XeId);
    }
}
