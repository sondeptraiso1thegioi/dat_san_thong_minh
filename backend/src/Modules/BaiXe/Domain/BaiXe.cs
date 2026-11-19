using System.Text.RegularExpressions;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.BaiXe.Domain;

// Thực thể của module BaiXe: khu, ô, camera, trạng thái ô. Quy tắc nằm trong chính thực thể (không nằm ở controller hay SQL),
// không phụ thuộc EF Core: EF chỉ cần constructor rỗng private để đọc dữ liệu lên.

public class KhuVuc
{
    public const int DoDaiTenToiDa = 50;
    public int Id { get; private set; }
    public string Ten { get; private set; } = "";
    public int LoaiXeId { get; private set; }
    /// <summary>Số xe tối đa của khu (đề cương v10). Tổng các khu cùng loại nên bằng sức chứa của loại xe.</summary>
    public int SucChua { get; private set; }
    /// <summary>Mã chấm trên ảnh sơ đồ (R, S, HAM; V009). null: khu không có chấm trên ảnh.</summary>
    public string? MaSoDo { get; private set; }
    /// <summary>false: khu tạm tắt, không được chọn khi chỉ dẫn và không hiện cho người dùng (dữ liệu vẫn giữ).</summary>
    public bool DangDung { get; private set; } = true;

    public const int SucChuaToiDa = 10000;
    /// <summary>Mỗi khu xe máy chứa tối đa 80 xe (quy định của bãi).</summary>
    public const int SucChuaToiDaXeMay = 80;

    private KhuVuc() { }

    /// <param name="laXeMay">Khu xe máy thì tối đa <see cref="SucChuaToiDaXeMay"/>.</param>
    public void DatSucChua(int sucChua, bool laXeMay = false)
    {
        var toiDa = laXeMay ? SucChuaToiDaXeMay : SucChuaToiDa;
        if (sucChua < 0 || sucChua > toiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "SUC_CHUA_KHU_SAI",
                laXeMay ? $"Sức chứa khu xe máy phải từ 0 đến {toiDa} xe." : $"Sức chứa khu phải từ 0 đến {toiDa}.");
        SucChua = sucChua;
    }

    public void DatDangDung(bool dangDung) => DangDung = dangDung;

    /// <param name="loaiXeHopLe">Các loại xe đang có (hỏi GoiCho qua IGoiChoModule, vì loai_xe là bảng của GoiCho).</param>
    public static KhuVuc Tao(string? ten, int loaiXeId, IReadOnlyCollection<int> loaiXeHopLe)
    {
        var k = new KhuVuc();
        k.Sua(ten, loaiXeId, loaiXeHopLe);
        return k;
    }

    public void Sua(string? ten, int loaiXeId, IReadOnlyCollection<int> loaiXeHopLe)
    {
        var t = (ten ?? "").Trim();
        if (t.Length == 0 || t.Length > DoDaiTenToiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "TEN_KHU_SAI", $"Tên khu phải có từ 1 đến {DoDaiTenToiDa} ký tự.");
        if (!loaiXeHopLe.Contains(loaiXeId))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "LOAI_XE_SAI", "Loại xe không hợp lệ.");
        Ten = t;
        LoaiXeId = loaiXeId;
    }
}

public class Camera
{
    public int Id { get; private set; }
    public int KhuId { get; private set; }
    public string Ten { get; private set; } = "";
    public string NguonAnh { get; private set; } = "";
    public bool DangBat { get; private set; }
    /// <summary>
    /// KHU_VAO: camera soi hướng xe đi VÀO khu. KHU_RA: camera soi hướng xe đi RA khỏi khu (đọc biển bằng Model A + PaddleOCR).
    /// LOI_VAO_KHU: kiểu cũ, một camera dùng chung hai hướng. GIAM_SAT: nhìn ô đỗ khu ô tô (PKLot).
    /// </summary>
    public string VaiTro { get; private set; } = VaiTroCamera.GiamSat;

    private Camera() { }

    public void DatVaiTro(string? vaiTro)
    {
        var v = (vaiTro ?? VaiTroCamera.GiamSat).Trim().ToUpperInvariant();
        if (v is not (VaiTroCamera.KhuVao or VaiTroCamera.KhuRa or VaiTroCamera.LoiVaoKhu or VaiTroCamera.GiamSat))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "VAI_TRO_CAMERA_SAI", "Vai trò camera là KHU_VAO, KHU_RA, LOI_VAO_KHU hoặc GIAM_SAT.");
        VaiTro = v;
    }

    public static Camera Tao(int khuId, string? ten, string? nguonAnh, bool dangBat)
    {
        var c = new Camera { KhuId = khuId };
        c.Sua(ten, nguonAnh, dangBat);
        return c;
    }

    public void Sua(string? ten, string? nguonAnh, bool dangBat)
    {
        var t = (ten ?? "").Trim();
        var n = (nguonAnh ?? "").Trim();
        if (t.Length == 0 || t.Length > 50)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "TEN_CAMERA_SAI", "Tên camera phải có từ 1 đến 50 ký tự.");
        if (n.Length == 0 || n.Length > 500)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "NGUON_ANH_SAI", "Nguồn ảnh (URL RTSP hoặc đường dẫn ảnh mẫu) không được rỗng.");
        Ten = t;
        NguonAnh = n;
        DangBat = dangBat;
    }
}

public static class VaiTroCamera
{
    public const string KhuVao = "KHU_VAO";
    public const string KhuRa = "KHU_RA";
    public const string LoiVaoKhu = "LOI_VAO_KHU";
    public const string GiamSat = "GIAM_SAT";
}

public class ODo
{
    private static readonly Regex MauMaO = new(@"^[A-Z][A-Z0-9]{1,9}$", RegexOptions.Compiled);

    public int Id { get; private set; }
    public int KhuId { get; private set; }
    public int? CameraId { get; private set; }
    public string MaO { get; private set; } = "";
    public string MaQr { get; private set; } = "";

    private ODo() { }

    public static ODo Tao(KhuVuc khu, string? maO, Camera? camera)
    {
        var o = new ODo { KhuId = khu.Id };
        o.Sua(maO, camera);
        return o;
    }

    /// <summary>Mã ô viết hoa, bắt đầu bằng chữ (A01, B12). Mã QR dán tại ô suy ra từ mã ô. Camera phải cùng khu với ô.</summary>
    public void Sua(string? maO, Camera? camera)
    {
        var ma = (maO ?? "").Trim().ToUpperInvariant();
        if (!MauMaO.IsMatch(ma))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "MA_O_SAI", "Mã ô gồm 2 đến 10 ký tự chữ / số, bắt đầu bằng chữ, ví dụ A01.");
        if (camera is not null && camera.KhuId != KhuId)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "CAMERA_KHAC_KHU", "Camera phải thuộc cùng khu với ô.");
        MaO = ma;
        MaQr = "QR-" + ma;
        CameraId = camera?.Id;
    }
}

public static class TrangThaiOMa
{
    public const string Trong = "TRONG";
    public const string CoXe = "CO_XE";
    public const string KhongRo = "KHONG_RO";
    public static readonly IReadOnlyCollection<string> HopLe = new[] { Trong, CoXe, KhongRo };
}

public static class NguonTrangThai
{
    public const string Admin = "ADMIN";
    public const string Ai = "AI";
}

/// <summary>Trạng thái mới nhất của một ô và ai báo (Admin đặt tay, hoặc AI ô trống).</summary>
public class TrangThaiO
{
    /// <summary>AI báo quá lâu không cập nhật thì không tin nữa: camera có thể đã hỏng.</summary>
    public static readonly TimeSpan HanTinAi = TimeSpan.FromMinutes(15);

    public int OId { get; private set; }
    public string TrangThai { get; private set; } = TrangThaiOMa.KhongRo;
    public string Nguon { get; private set; } = NguonTrangThai.Admin;
    public DateTime CapNhatLuc { get; private set; }

    private TrangThaiO() { }

    public static TrangThaiO Tao(int oId, string trangThai, string nguon, DateTime bayGio)
    {
        var t = new TrangThaiO { OId = oId };
        t.CapNhat(trangThai, nguon, bayGio);
        return t;
    }

    public void CapNhat(string? trangThai, string nguon, DateTime bayGio)
    {
        if (trangThai is null || !TrangThaiOMa.HopLe.Contains(trangThai))
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "TRANG_THAI_O_SAI", "Trạng thái ô phải là TRONG, CO_XE hoặc KHONG_RO.");
        TrangThai = trangThai;
        Nguon = nguon;
        CapNhatLuc = bayGio;
    }

    /// <summary>Trạng thái đem ra hiển thị: AI báo đã quá 15 phút thì coi là "không rõ". Admin đặt tay thì giữ nguyên.</summary>
    public string HienThi(DateTime bayGio) =>
        Nguon == NguonTrangThai.Ai && bayGio - CapNhatLuc > HanTinAi ? TrangThaiOMa.KhongRo : TrangThai;

    /// <summary>Ô chưa từng có trạng thái thì coi là không rõ.</summary>
    public static string HienThi(TrangThaiO? t, DateTime bayGio) => t?.HienThi(bayGio) ?? TrangThaiOMa.KhongRo;
}

/// <summary>
/// Đường ngắn nhất trên đồ thị lối đi (Dijkstra, cạnh vô hướng, trọng số = mét). Dùng để chọn khu gần cổng nhất
/// và vẽ đường dẫn từ cổng tới khu (đề cương v10, UC-U18, UC-U22). Không phụ thuộc DB: test được bằng dữ liệu giả.
/// </summary>
public static class DuongNganNhat
{
    public record Canh(int A, int B, double DoDai);

    /// <summary>Khoảng cách từ nút nguồn tới mọi nút đi tới được, và nút đi trước trên đường ngắn nhất.</summary>
    public static (IReadOnlyDictionary<int, double> KhoangCach, IReadOnlyDictionary<int, int> Truoc) Tinh(int nguon, IEnumerable<Canh> canh)
    {
        var ke = new Dictionary<int, List<(int, double)>>();
        foreach (var c in canh)
        {
            if (c.DoDai <= 0) continue;
            (ke.TryGetValue(c.A, out var a) ? a : ke[c.A] = new()).Add((c.B, c.DoDai));
            (ke.TryGetValue(c.B, out var b) ? b : ke[c.B] = new()).Add((c.A, c.DoDai));
        }
        var kc = new Dictionary<int, double> { [nguon] = 0 };
        var truoc = new Dictionary<int, int>();
        var hang = new PriorityQueue<int, double>();
        hang.Enqueue(nguon, 0);
        while (hang.TryDequeue(out var u, out var d))
        {
            if (d > kc[u]) continue; // bản cũ trong hàng đợi
            foreach (var (v, w) in ke.GetValueOrDefault(u) ?? new())
            {
                var moi = d + w;
                if (kc.TryGetValue(v, out var cu) && cu <= moi) continue;
                kc[v] = moi; truoc[v] = u; hang.Enqueue(v, moi);
            }
        }
        return (kc, truoc);
    }

    /// <summary>Danh sách nút từ nguồn tới đích; null khi không đi tới được.</summary>
    public static IReadOnlyList<int>? Duong(int nguon, int dich, IReadOnlyDictionary<int, int> truoc)
    {
        if (nguon == dich) return new[] { nguon };
        if (!truoc.ContainsKey(dich)) return null;
        var ds = new List<int> { dich };
        while (ds[^1] != nguon) ds.Add(truoc[ds[^1]]);
        ds.Reverse();
        return ds;
    }
}
