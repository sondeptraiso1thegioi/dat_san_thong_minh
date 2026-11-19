using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NhaXe.Api.Common;

/// <summary>
/// Cả hệ thống dùng giờ địa phương của bãi (giờ Việt Nam: SYSDATETIME() của SQL Server, DateTime.Now của API).
/// Giá trị đọc từ DB có Kind = Unspecified, còn DateTime.Now có Kind = Local; mặc định JSON viết hai kiểu khác nhau
/// ("...T12:59:00" và "...T13:40:19+07:00"), trình duyệt hiểu lệch nhau. Viết mọi DateTime cùng một dạng, không kèm múi giờ.
/// </summary>
public sealed class GioDiaPhuongJson : JsonConverter<DateTime>
{
    private const string DinhDang = "yyyy-MM-dd'T'HH:mm:ss.fff";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("Thiếu thời gian.");
        var d = DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        // Client gửi giờ UTC ("...Z") thì đổi về giờ địa phương; còn lại giữ nguyên là giờ địa phương
        return d.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(d.ToLocalTime(), DateTimeKind.Unspecified) : DateTime.SpecifyKind(d, DateTimeKind.Unspecified);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue((value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value).ToString(DinhDang, CultureInfo.InvariantCulture));
}
