using NhaXe.Modules.AiBienSo.Application;
using NhaXe.Modules.AiBienSo.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.AiBienSo;

public class FakeOcr : IMoHinhDocBienSo
{
    public List<BienSoUngVien> UngVien { get; } = [];
    public int SoLanGoi { get; private set; }
    public Task<IReadOnlyList<BienSoUngVien>> DocAsync(AnhBienSo anh, CancellationToken ct) { SoLanGoi++; return Task.FromResult<IReadOnlyList<BienSoUngVien>>(UngVien); }
}
public class DocBienSoTests
{
    private static AnhBienSo Anh => new([137, 80, 78, 71, 13, 10, 26, 10], "image/png");
    [Fact]
    public async Task MotBienRo_ChuanHoaVaTraKetQua()
    {
        var ocr = new FakeOcr(); ocr.UngVien.Add(new("30a-123.45", .92)); ocr.UngVien.Add(new("30A12345", .85));
        var kq = await new AiBienSoModule(new(ocr)).DocAsync(Anh);
        Assert.Equal("30A12345", kq.BienSo); Assert.False(kq.CanXacNhan); Assert.Single(kq.UngVien); Assert.Equal(.92, kq.DoTinCay);
    }
    [Theory]
    [InlineData(.84, true)]
    [InlineData(.85, false)]
    public async Task DoTinCay_TheoNguong(double diem, bool can)
    {
        var ocr = new FakeOcr(); ocr.UngVien.Add(new("29B1-123.45", diem));
        var kq = await new DocBienSoUseCase(ocr).ThucHienAsync(Anh); Assert.Equal(can, kq.CanXacNhan);
        Assert.Equal(can ? null : "29B112345", kq.BienSo);
    }
    [Fact]
    public async Task NhieuBien_KhongTuChon()
    {
        var ocr = new FakeOcr(); ocr.UngVien.AddRange([new("30A12345", .99), new("51G67890", .95)]);
        var kq = await new DocBienSoUseCase(ocr).ThucHienAsync(Anh); Assert.True(kq.CanXacNhan); Assert.Null(kq.BienSo); Assert.Equal(2, kq.UngVien.Count);
    }
    [Theory]
    [InlineData("ABC", .99)]
    [InlineData("30A12345", -1)]
    [InlineData("30A12345", 2)]
    [InlineData("30A12345", double.NaN)]
    public async Task KetQuaSai_BoQua_KhongDoan(string bien, double diem)
    {
        var ocr = new FakeOcr(); ocr.UngVien.Add(new(bien, diem));
        var kq = await new DocBienSoUseCase(ocr).ThucHienAsync(Anh); Assert.Empty(kq.UngVien); Assert.True(kq.CanXacNhan); Assert.Null(kq.BienSo);
    }
    [Fact]
    public async Task AnhRong_KhongGoiOcr()
    {
        var ocr = new FakeOcr(); var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DocBienSoUseCase(ocr).ThucHienAsync(new([], "image/png")));
        Assert.Equal("ANH_SAI_KICH_THUOC", ex.Ma); Assert.Equal(0, ocr.SoLanGoi);
    }
    [Fact]
    public async Task GiaDinhDang_KhongGoiOcr()
    {
        var ocr = new FakeOcr(); var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DocBienSoUseCase(ocr).ThucHienAsync(Anh with { LoaiNoiDung = "image/jpeg" }));
        Assert.Equal("ANH_SAI_DINH_DANG", ex.Ma); Assert.Equal(0, ocr.SoLanGoi);
    }
    [Fact]
    public async Task AnhQuaLon_KhongGoiOcr()
    {
        var ocr = new FakeOcr(); var ex = await Assert.ThrowsAsync<LoiNghiepVu>(() => new DocBienSoUseCase(ocr).ThucHienAsync(new(new byte[5 * 1024 * 1024 + 1], "image/png")));
        Assert.Equal("ANH_SAI_KICH_THUOC", ex.Ma); Assert.Equal(0, ocr.SoLanGoi);
    }
    [Fact]
    public async Task HuyYeuCau_TruyenCancellationToken()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DocAnhTaiLenUseCase(new FakeAiBienSoModule()).ThucHienAsync(new MemoryStream(Anh.NoiDung), Anh.NoiDung.Length, Anh.LoaiNoiDung, cts.Token));
    }

    [Theory]
    [InlineData("29B112345", "XE_MAY")]
    [InlineData("60MĐ101657", "XE_MAY")]
    [InlineData("30A1234", "O_TO")]
    [InlineData("60LD00584", "O_TO")]
    [InlineData("30A12345", "KHONG_RO")] // ô tô mới hoặc xe máy cũ: không dùng để chặn
    [InlineData(null, "KHONG_RO")]
    public void LoaiBien_SuyTuDangBien(string? bien, string loai) =>
        Assert.Equal(loai, NhaXe.Modules.AiBienSo.Domain.QuyTacDocBienSo.LoaiBien(bien));

    [Theory]
    [InlineData("30A12345", "30A12345", 0)]
    [InlineData("30A12345", "30A12346", 1)]
    [InlineData("30A12345", "30A1234", 1)]
    [InlineData("30A12345", "31B12345", 2)]
    public void Levenshtein(string a, string b, int kc) => Assert.Equal(kc, NhaXe.SharedKernel.KhopBienSo.KhoangCach(a, b));

    [Fact]
    public void KhopGanDung_ChiNhanKhiMotUngVien()
    {
        var ds = new[] { "30A12345", "30A12347", "29B112345" };
        Assert.Equal(("30A12345", false), Pick(NhaXe.SharedKernel.KhopBienSo.Tim("30A12345", ds, x => x)));
        Assert.Equal(("29B112345", true), Pick(NhaXe.SharedKernel.KhopBienSo.Tim("29B112346", ds, x => x)));
        var nhieu = NhaXe.SharedKernel.KhopBienSo.Tim("30A12346", ds, x => x);
        Assert.Null(nhieu.Khop); Assert.Equal(2, nhieu.UngVien.Count);
        static (string?, bool) Pick((string? K, bool G, IReadOnlyList<string> U) r) => (r.K, r.G);
    }
}
