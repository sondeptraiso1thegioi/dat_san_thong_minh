using System.Xml.Linq;

namespace NhaXe.Tests.Chung;

/// <summary>
/// Kiểm luật phụ thuộc của Modular Monolith bằng cách đọc file .csproj (kien-truc-module-v10.md, mục "Luật"):
/// 1. Domain chỉ tham chiếu SharedKernel.
/// 2. Application của module A chỉ tham chiếu Domain/Contracts của chính A và Contracts của module khác.
/// 3. Contracts không tham chiếu tầng trong của module nào.
/// Module mới thêm vào sẽ tự được kiểm, không phải sửa test.
/// </summary>
public class LuatKienTrucTests
{
    private static readonly string ThuMucModules = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Modules"));

    private static IEnumerable<(string Module, string Tang, string[] ThamChieu)> CacProject()
    {
        if (!Directory.Exists(ThuMucModules)) yield break;
        foreach (var csproj in Directory.GetFiles(ThuMucModules, "*.csproj", SearchOption.AllDirectories))
        {
            var tang = Path.GetFileName(Path.GetDirectoryName(csproj)!);
            var module = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(csproj))!);
            var thamChieu = XDocument.Load(csproj).Descendants("ProjectReference")
                .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))
                .ToArray();
            yield return (module, tang, thamChieu);
        }
    }

    [Fact]
    public void Domain_chi_tham_chieu_SharedKernel()
    {
        foreach (var (module, tang, ts) in CacProject().Where(p => p.Tang == "Domain"))
            foreach (var t in ts)
                Assert.True(t == "NhaXe.SharedKernel", $"{module}.Domain không được tham chiếu {t}");
    }

    [Fact]
    public void Application_chi_goi_module_khac_qua_Contracts()
    {
        foreach (var (module, tang, ts) in CacProject().Where(p => p.Tang == "Application"))
            foreach (var t in ts)
            {
                var hopLe = t == "NhaXe.SharedKernel"
                    || t == $"NhaXe.Modules.{module}.Domain"
                    || t.EndsWith(".Contracts");
                Assert.True(hopLe, $"{module}.Application không được tham chiếu {t} (chỉ được dùng Contracts của module khác)");
            }
    }

    [Fact]
    public void Contracts_khong_tham_chieu_tang_trong()
    {
        foreach (var (module, tang, ts) in CacProject().Where(p => p.Tang == "Contracts"))
            foreach (var t in ts)
                Assert.True(t == "NhaXe.SharedKernel" || t.EndsWith(".Contracts"),
                    $"{module}.Contracts không được tham chiếu {t}");
    }
}
