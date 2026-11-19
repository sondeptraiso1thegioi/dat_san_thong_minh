using Microsoft.Extensions.DependencyInjection;
using NhaXe.Modules.AiBienSo.Application;
namespace NhaXe.Modules.AiBienSo.Infrastructure;

public static class DependencyInjection
{
    /// <param name="thuMucMoHinh">Thư mục chứa model_a_yolov8n.onnx, paddleocr_rec_bien_so.onnx, dict_bien_so.txt.</param>
    public static IServiceCollection AddAiBienSoModule(this IServiceCollection s, string thuMucMoHinh)
    {
        // Singleton: hai mô hình chỉ nạp một lần cho cả tiến trình (mỗi lần nạp tốn ~0,5 giây và ~100 MB RAM)
        s.AddSingleton(new TuyChonMoHinh(thuMucMoHinh));
        s.AddSingleton<MoHinhDocBienSoOnnx>();
        s.AddSingleton<IMoHinhDocBienSo>(sp => sp.GetRequiredService<MoHinhDocBienSoOnnx>());
        s.AddAiBienSoApplication();
        return s;
    }
}
