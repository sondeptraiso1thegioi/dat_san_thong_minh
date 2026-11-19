using NhaXe.Modules.BaiXe.Application;
using NhaXe.Modules.BaiXe.Domain;

namespace NhaXe.Tests.BaiXe;

/// <summary>Dijkstra trên đồ thị lối đi giống dữ liệu mẫu (nha-xe.sql + V006).</summary>
public class DuongDiTests
{
    private static readonly DuongNganNhat.Canh[] Canh =
    [
        new(1, 3, 15), new(1, 4, 15), new(2, 3, 15), new(2, 4, 15), new(2, 5, 5), new(2, 6, 14), new(1, 7, 8)
    ];

    [Fact]
    public void KhoangCach_DiQuaNutTrungGian()
    {
        var (kc, truoc) = DuongNganNhat.Tinh(1, Canh);
        Assert.Equal(35, kc[5]);                                  // 1 → 3 → 2 → 5 = 15 + 15 + 5
        Assert.Equal([1, 3, 2, 5], DuongNganNhat.Duong(1, 5, truoc)!);
        Assert.Equal(8, kc[7]);
        Assert.Null(DuongNganNhat.Duong(1, 99, truoc));
    }

    [Fact]
    public async Task BaiXeModule_TraKhoangCachTuCong_VaDuongToiKhu()
    {
        var q = new FakeCauHinhBaiQuery();
        q.Nut.AddRange([new(1, "Lối vào", 20, 160, null), new(2, "Ngã ba giữa", 300, 160, null), new(3, "Đầu khu A", 160, 150, 1),
                        new(4, "Đầu khu B", 160, 170, 2), new(5, "Đầu khu C", 320, 160, 3), new(7, "Đầu khu D", 30, 300, 4)]);
        q.Canh.AddRange(Canh);
        var m = new BaiXeModule(null!, q, new FakeClock());
        var khu = typeof(KhuVuc).GetMethod("Tao")!;
        foreach (var (id, ten, sc) in new[] { (3, "Khu C", 12), (4, "Khu D", 8) })
        {
            var k = KhuVuc.Tao(ten, 2, [2]); k.DatSucChua(sc);
            typeof(KhuVuc).GetProperty("Id")!.SetValue(k, id); q.Khu.Add(k);
        }
        var ds = await m.LayKhuAsync();
        Assert.Equal((35d, 8d), (ds.Single(k => k.Id == 3).KhoangCachTuCong!.Value, ds.Single(k => k.Id == 4).KhoangCachTuCong!.Value));
        var duong = await m.TimDuongAsync(3);
        Assert.Equal(["Lối vào", "Đầu khu A", "Ngã ba giữa", "Đầu khu C"], duong!.Diem.Select(d => d.Ten));
    }

    [Fact]
    public void Camera_VaiTroSai_BiChan()
    {
        var c = Camera.Tao(1, "CAM", "/a.jpg", true);
        c.DatVaiTro("loi_vao_khu");
        Assert.Equal(VaiTroCamera.LoiVaoKhu, c.VaiTro);
        Assert.Equal("VAI_TRO_CAMERA_SAI", Assert.Throws<NhaXe.SharedKernel.LoiNghiepVu>(() => c.DatVaiTro("XYZ")).Ma);
    }
}
