using NhaXe.Modules.AiBienSo.Contracts;
using NhaXe.Modules.AiBienSo.Domain;
using NhaXe.SharedKernel;
namespace NhaXe.Modules.AiBienSo.Application;

public class DocAnhTaiLenUseCase(IAiBienSoModule module)
{
    public async Task<KetQuaDocBienSo> ThucHienAsync(Stream stream, long kichThuoc, string loai, CancellationToken ct = default)
    {
        if (kichThuoc <= 0 || kichThuoc > QuyTacDocBienSo.KichThuocToiDa)
            throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_SAI_KICH_THUOC", "Ảnh phải có dữ liệu và không quá 5 MB.");
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (ms.Length + n > QuyTacDocBienSo.KichThuocToiDa)
                throw new LoiNghiepVu(LoaiLoi.DuLieuKhongHopLe, "ANH_SAI_KICH_THUOC", "Ảnh không được quá 5 MB.");
            ms.Write(buffer, 0, n);
        }
        return await module.DocAsync(new(ms.ToArray(), loai), ct);
    }
}
