namespace NhaXe.SharedKernel;

/// <summary>
/// So khớp biển số (đề cương v10): khớp chính xác trước, rồi khớp gần đúng với khoảng cách chỉnh sửa
/// Levenshtein tối đa 1 ký tự, CHỈ nhận khi có đúng một ứng viên. Dùng chung cho cổng ra (CongRaVao)
/// và camera lối vào khu (TimXe).
/// </summary>
public static class KhopBienSo
{
    public const int LechToiDa = 1;

    /// <summary>Khoảng cách Levenshtein (thêm / xoá / thay một ký tự tính là 1).</summary>
    public static int KhoangCach(string a, string b)
    {
        if (a == b) return 0;
        var truoc = new int[b.Length + 1];
        var nay = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) truoc[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            nay[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var thay = truoc[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                nay[j] = Math.Min(thay, Math.Min(truoc[j] + 1, nay[j - 1] + 1));
            }
            (truoc, nay) = (nay, truoc);
        }
        return truoc[b.Length];
    }

    /// <summary>
    /// Tìm trong danh sách: có biển trùng hẳn thì trả biển đó (GanDung = false); không thì trả biển DUY NHẤT lệch ≤ 1
    /// (GanDung = true); nhiều hơn một biển gần đúng thì trả null kèm danh sách để Admin chọn.
    /// </summary>
    public static (T? Khop, bool GanDung, IReadOnlyList<T> UngVien) Tim<T>(string bienSo, IEnumerable<T> ds, Func<T, string> layBien)
        where T : class
    {
        var list = ds as IReadOnlyList<T> ?? ds.ToList();
        var dung = list.FirstOrDefault(x => layBien(x) == bienSo);
        if (dung is not null) return (dung, false, new[] { dung });
        var gan = list.Where(x => Math.Abs(layBien(x).Length - bienSo.Length) <= LechToiDa
                                  && KhoangCach(layBien(x), bienSo) <= LechToiDa).ToList();
        return gan.Count == 1 ? (gan[0], true, gan) : (null, false, gan);
    }
}
