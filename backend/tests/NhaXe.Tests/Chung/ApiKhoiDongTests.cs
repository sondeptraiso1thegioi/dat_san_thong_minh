using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NhaXe.Tests.Chung;

/// <summary>API khởi động được và /api/health trả OK (chưa cần CSDL vì chưa có module nào).</summary>
public class ApiKhoiDongTests(WebApplicationFactory<Program> f) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_tra_ok_va_co_ten_instance()
    {
        var http = f.WithWebHostBuilder(b => b.UseSetting("environment", "Development")).CreateClient();
        var res = await http.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True(res.Headers.Contains("X-Api-Instance"));
    }

    [Fact]
    public async Task Swagger_mo_duoc()
    {
        var http = f.WithWebHostBuilder(b => b.UseSetting("environment", "Development")).CreateClient();
        var res = await http.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
