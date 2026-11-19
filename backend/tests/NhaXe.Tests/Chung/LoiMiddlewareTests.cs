using NhaXe.Api.Common;
using NhaXe.SharedKernel;

namespace NhaXe.Tests.Chung;

public class LoiMiddlewareTests
{
    [Theory]
    [InlineData(LoaiLoi.DuLieuKhongHopLe, 400)]
    [InlineData(LoaiLoi.ChuaDangNhap, 401)]
    [InlineData(LoaiLoi.KhongCoQuyen, 403)]
    [InlineData(LoaiLoi.KhongTimThay, 404)]
    [InlineData(LoaiLoi.XungDot, 409)]
    [InlineData(LoaiLoi.QuaNhieuLan, 429)]
    [InlineData(LoaiLoi.DichVuTamNgung, 503)]
    public void Loai_loi_doi_dung_ma_http(LoaiLoi loai, int ma) => Assert.Equal(ma, LoiMiddleware.MaHttp(loai));
}
