namespace NhaXe.Modules.GoiCho.Domain;

/// <summary>Ai được dùng một xe: chủ xe, cộng người cùng nhóm khi xe bật chia sẻ (cùng quy tắc với Xe.DuocDungBoi).</summary>
public static class NguoiDungXe
{
    public static IReadOnlyList<int> Tinh(int chuXeId, bool chiaSeNhom, IEnumerable<int> nguoiCungNhom) =>
        chiaSeNhom
            ? new[] { chuXeId }.Concat(nguoiCungNhom.Where(id => id != chuXeId)).Distinct().ToList()
            : new[] { chuXeId };
}
