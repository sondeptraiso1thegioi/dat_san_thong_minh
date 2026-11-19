namespace NhaXe.Modules.BaiXe.Domain;

/// <summary>Quy tắc tô màu xe trên sơ đồ bãi: còn hạn, sắp hết (dưới 72 giờ, đúng mốc nhắc hạn của đồ án), quá hạn.</summary>
public static class TinhTrangGoiTrenSoDo
{
    public const string ConHan = "CON_HAN";
    public const string SapHet = "SAP_HET";
    public const string QuaHan = "QUA_HAN";
    public const string KhongCoGoi = "KHONG_CO_GOI";
    public static readonly TimeSpan MocSapHet = TimeSpan.FromHours(72);

    public static string Tinh(DateTime? hetHan, DateTime bayGio) => hetHan switch
    {
        null => KhongCoGoi,
        var h when h <= bayGio => QuaHan,
        var h when h - bayGio <= MocSapHet => SapHet,
        _ => ConHan,
    };
}
