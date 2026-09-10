using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace TaleTrackApp.Tests;

[Collection(ApiCollection.Name)]
public class AvatarFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private HttpClient NewClient()
    {
        var client = _factory.CreateClient();
        return client;
    }

    private async Task<(HttpClient Client, int UserId)> AuthedAsync(string email, string username)
    {
        var client = NewClient();
        var reg = await client.PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = "Password1!" });
        Assert.True(reg.IsSuccessStatusCode, await reg.Content.ReadAsStringAsync());

        var login = await client.PostAsJsonAsync("/api/login", new { Email = email, Password = "Password1!" });
        var jwt = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var me = await (await client.GetAsync("/api/user/me")).Content.ReadFromJsonAsync<JsonElement>();
        return (client, me.GetProperty("data").GetProperty("id").GetInt32());
    }

    private static byte[] MakePng(int w, int h)
    {
        using var img = new Image<Rgba32>(w, h);
        img.Mutate(c => c.BackgroundColor(Color.CornflowerBlue));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static MultipartFormDataContent FilePart(byte[] bytes, string contentType, string name = "photo.png")
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { part, "file", name } };
    }

    [Fact]
    public async Task Upload_ThenGet_ReturnsA256SquareWebp()
    {
        var (client, userId) = await AuthedAsync("avatar-ok@test.com", "avatarok");

        var upload = await client.PostAsync("/api/user/avatar", FilePart(MakePng(600, 400), "image/png"));
        Assert.True(upload.IsSuccessStatusCode, await upload.Content.ReadAsStringAsync());
        var body = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("avatarUrl").GetString()!;
        Assert.StartsWith($"/api/users/{userId}/avatar?v=", url);

        // it's also reflected on the profile
        var me = await (await client.GetAsync("/api/user/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(url, me.GetProperty("data").GetProperty("avatarUrl").GetString());

        var getRes = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        Assert.Equal("image/webp", getRes.Content.Headers.ContentType?.MediaType);

        using var decoded = Image.Load(await getRes.Content.ReadAsByteArrayAsync());
        Assert.Equal(256, decoded.Width);
        Assert.Equal(256, decoded.Height);
    }

    [Fact]
    public async Task Upload_NonImage_Returns400()
    {
        var (client, _) = await AuthedAsync("avatar-junk@test.com", "avatarjunk");
        var res = await client.PostAsync("/api/user/avatar",
            FilePart(Encoding.UTF8.GetBytes("definitely not an image"), "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_TooLarge_Returns400()
    {
        var (client, _) = await AuthedAsync("avatar-big@test.com", "avatarbig");
        var res = await client.PostAsync("/api/user/avatar",
            FilePart(new byte[6 * 1024 * 1024], "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutAuth_Rejected()
    {
        var res = await _factory.CreateClient().PostAsync("/api/user/avatar",
            FilePart(MakePng(100, 100), "image/png"));
        Assert.True(res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAvatar_WhenNoneUploaded_Returns404()
    {
        var (_, userId) = await AuthedAsync("avatar-none@test.com", "avatarnone");
        var res = await _factory.CreateClient().GetAsync($"/api/users/{userId}/avatar");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTheImageAndClearsTheUrl()
    {
        var (client, userId) = await AuthedAsync("avatar-del@test.com", "avatardel");

        await client.PostAsync("/api/user/avatar", FilePart(MakePng(300, 300), "image/png"));
        Assert.Equal(HttpStatusCode.OK,
            (await _factory.CreateClient().GetAsync($"/api/users/{userId}/avatar")).StatusCode);

        var del = await client.DeleteAsync("/api/user/avatar");
        Assert.True(del.IsSuccessStatusCode, await del.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound,
            (await _factory.CreateClient().GetAsync($"/api/users/{userId}/avatar")).StatusCode);

        var me = await (await client.GetAsync("/api/user/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, me.GetProperty("data").GetProperty("avatarUrl").ValueKind);
    }
}
