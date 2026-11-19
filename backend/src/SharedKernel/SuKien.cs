namespace NhaXe.SharedKernel;

// Sự kiện giữa các module (Buổi 12). Module A báo "một việc đã xảy ra", module nào quan tâm thì nghe.
// Bên phát không biết ai nghe, nên không phụ thuộc vào bên nghe. Dùng khi gọi trực tiếp sẽ tạo vòng phụ thuộc
// (vd. CongRaVao phát XeDaVao, TimXe nghe để chỉ dẫn khu; CongRaVao không gọi TimXe).
// Hiện chạy trong cùng tiến trình (Api/Common/PhatSuKienTrongTienTrinh). Đổi sang RabbitMQ chỉ cần cài lại IPhatSuKien.

/// <summary>Đánh dấu một record là sự kiện. Đặt tên ở thì quá khứ: XeDaVao, XeDaRa.</summary>
public interface ISuKien { }

/// <summary>Phát sự kiện SAU KHI đã lưu xong dữ liệu của mình.</summary>
public interface IPhatSuKien
{
    Task PhatAsync<T>(T suKien, CancellationToken ct = default) where T : ISuKien;
}

/// <summary>Bên nghe một loại sự kiện. Phải chạy lại nhiều lần vẫn đúng (idempotent), vì sự kiện có thể đến hai lần.</summary>
public interface INghe<in T> where T : ISuKien
{
    Task XuLyAsync(T suKien, CancellationToken ct);
}
