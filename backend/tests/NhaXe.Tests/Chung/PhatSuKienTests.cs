using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NhaXe.Api.Common;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.Chung;

public class PhatSuKienTests
{
    private sealed record DaXayRa(int Id) : ISuKien;

    private sealed class NgheDem : INghe<DaXayRa>
    {
        public int SoLan;
        public Task XuLyAsync(DaXayRa s, CancellationToken ct) { SoLan++; return Task.CompletedTask; }
    }

    /// <summary>Lỗi N lần đầu rồi chạy được.</summary>
    private sealed class NgheLoi(int soLanLoi) : INghe<DaXayRa>
    {
        public int SoLanGoi;
        public Task XuLyAsync(DaXayRa s, CancellationToken ct)
        {
            SoLanGoi++;
            if (SoLanGoi <= soLanLoi) throw new InvalidOperationException("lỗi giả");
            return Task.CompletedTask;
        }
    }

    private static PhatSuKienTrongTienTrinh TaoBenPhat(params INghe<DaXayRa>[] cacBenNghe)
    {
        var dv = new ServiceCollection();
        foreach (var n in cacBenNghe) dv.AddSingleton(n);
        return new PhatSuKienTrongTienTrinh(dv.BuildServiceProvider(), NullLogger<PhatSuKienTrongTienTrinh>.Instance);
    }

    [Fact]
    public async Task Moi_ben_nghe_deu_nhan_su_kien()
    {
        var a = new NgheDem();
        var b = new NgheDem();
        await TaoBenPhat(a, b).PhatAsync(new DaXayRa(1));
        Assert.Equal(1, a.SoLan);
        Assert.Equal(1, b.SoLan);
    }

    [Fact]
    public async Task Ben_nghe_loi_tam_thoi_duoc_thu_lai()
    {
        var loi = new NgheLoi(soLanLoi: 2);
        await TaoBenPhat(loi).PhatAsync(new DaXayRa(1));
        Assert.Equal(3, loi.SoLanGoi);
    }

    [Fact]
    public async Task Ben_nghe_loi_mai_khong_lam_hong_ben_phat_va_ben_khac_van_nhan()
    {
        var loi = new NgheLoi(soLanLoi: 99);
        var tot = new NgheDem();
        await TaoBenPhat(loi, tot).PhatAsync(new DaXayRa(1)); // không ném lỗi ra ngoài
        Assert.Equal(PhatSuKienTrongTienTrinh.SoLanThu, loi.SoLanGoi);
        Assert.Equal(1, tot.SoLan);
    }
}
