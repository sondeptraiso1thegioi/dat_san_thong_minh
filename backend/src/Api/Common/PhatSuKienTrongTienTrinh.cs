using NhaXe.SharedKernel;

namespace NhaXe.Api.Common;

/// <summary>
/// Cài đặt IPhatSuKien chạy trong cùng tiến trình: gọi lần lượt mọi INghe&lt;T&gt; đã đăng ký trong DI.
/// Bên phát đã lưu xong, nên một bên nghe lỗi KHÔNG được làm hỏng yêu cầu của người dùng:
/// mỗi bên nghe được thử tối đa 3 lần, vẫn lỗi thì ghi log rồi chạy tiếp bên khác (luật 5 trong kiến trúc module).
/// Hạn chế: tiến trình chết đúng lúc này thì sự kiện mất. Cách chuẩn là Outbox + RabbitMQ (phần nâng cao).
/// </summary>
public sealed class PhatSuKienTrongTienTrinh(IServiceProvider sp, ILogger<PhatSuKienTrongTienTrinh> log) : IPhatSuKien
{
    public const int SoLanThu = 3;

    public async Task PhatAsync<T>(T suKien, CancellationToken ct = default) where T : ISuKien
    {
        foreach (var nghe in sp.GetServices<INghe<T>>())
        {
            for (var lan = 1; lan <= SoLanThu; lan++)
            {
                try
                {
                    await nghe.XuLyAsync(suKien, ct);
                    break;
                }
                catch (Exception ex) when (lan < SoLanThu && !ct.IsCancellationRequested)
                {
                    log.LogWarning(ex, "Bên nghe {Nghe} lỗi lần {Lan}, thử lại", nghe.GetType().Name, lan);
                    await Task.Delay(100 * lan, ct);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Bên nghe {Nghe} lỗi khi xử lý {SuKien}", nghe.GetType().Name, suKien);
                }
            }
        }
    }
}
