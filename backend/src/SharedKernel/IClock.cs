namespace NhaXe.SharedKernel;

/// <summary>
/// Đồng hồ trừu tượng (DIP, Buổi 3): use case không gọi DateTime.Now trực tiếp,
/// nên unit test cho được "bây giờ" là một thời điểm cố định.
/// </summary>
public interface IClock
{
    DateTime BayGio { get; }
}

public sealed class DongHoHeThong : IClock
{
    public DateTime BayGio => DateTime.Now;
}
