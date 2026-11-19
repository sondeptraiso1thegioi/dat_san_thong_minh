using NhaXe.Modules.CongRaVao.Contracts;
using NhaXe.SharedKernel;

namespace NhaXe.Modules.CongRaVao.Application;

/// <summary>Xe vào được cho qua (lượt gửi đã lưu) → mở barie cổng vào.</summary>
public class MoBarieKhiXeVao(IBarie barie) : INghe<XeDaVao>
{
    public Task XuLyAsync(XeDaVao e, CancellationToken ct) => barie.MoAsync("VAO", e.LuotGuiId, ct);
}

/// <summary>Lượt gửi đã đóng (người dùng xác nhận / trả phí xong / Admin mở tay) → mở barie cổng ra.</summary>
public class MoBarieKhiXeRa(IBarie barie) : INghe<XeDaRa>
{
    public Task XuLyAsync(XeDaRa e, CancellationToken ct) => barie.MoAsync("RA", e.LuotGuiId, ct);
}

/// <summary>Màn hình camera cổng ra hỏi trạng thái lượt gửi (đang chờ app / đã ra / hết giờ / nghi mất).</summary>
public class LayLuotCongUseCase(ICongRaVaoRepository repo)
{
    public async Task<LuotCongDto> ThucHienAsync(int luotGuiId, CancellationToken ct = default)
    {
        var l = await repo.TimLuotAsync(luotGuiId, ct)
                ?? throw new LoiNghiepVu(LoaiLoi.KhongTimThay, "KHONG_THAY_LUOT", "Không tìm thấy lượt gửi.");
        return new LuotCongDto(l.Id, l.XeId, l.TrangThai, l.ThoiGianVao, l.ThoiGianRa, l.AnhVao is not null, l.AnhRa is not null);
    }
}

/// <summary>Mở ảnh đối chứng của một lượt gửi (loai = "vao" / "ra").</summary>
public class LayAnhDoiChungUseCase(ICongRaVaoRepository repo, IKhoAnhCong kho)
{
    public async Task<Stream?> ThucHienAsync(int luotGuiId, string loai, CancellationToken ct = default)
    {
        var l = await repo.TimLuotAsync(luotGuiId, ct);
        var khoa = loai == "vao" ? l?.AnhVao : loai == "ra" ? l?.AnhRa : null;
        return khoa is null ? null : await kho.MoAsync(khoa, ct);
    }
}
