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

/// <summary>
/// Profile pictures: an upload is stored as a 256x256 WebP that anyone can fetch without a session,
/// a new upload replaces the previous one straight away, and files that are not images or are
/// too large are rejected.
/// </summary>
[Collection(ApiCollection.Name)]
public class AvatarFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private static byte[] MakePng(int width, int height, Color color)
    {
        using var img = new Image<Rgba32>(width, height);
        img.Mutate(c => c.BackgroundColor(color));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static MultipartFormDataContent FilePart(byte[] bytes)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { part, "file", "photo.png" } };
    }

    private static async Task<string> UploadAsync(TestUser user, byte[] png)
    {
        var res = await user.Client.PutAsync("/api/users/me/avatar", FilePart(png));
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("avatarUrl").GetString()!;
    }

    /// <summary>Fetches the picture without a session and returns it decoded.</summary>
    private async Task<Image<Rgba32>> FetchAnonymouslyAsync(string url)
    {
        var res = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/webp", res.Content.Headers.ContentType?.MediaType);
        return Image.Load<Rgba32>(await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_IsStoredAsA256SquareWebp_ThatAnyoneCanFetch()
    {
        var user = await _factory.CreateUserAsync("avatar-ok@test.com", "avatarok");

        var url = await UploadAsync(user, MakePng(600, 400, Color.CornflowerBlue));

        var me = await user.Client.GetFromJsonAsync<JsonElement>("/api/users/me");
        Assert.Equal(url, me.GetProperty("data").GetProperty("avatarUrl").GetString());
        using var image = await FetchAnonymouslyAsync(url);
        Assert.Equal((256, 256), (image.Width, image.Height));
    }

    [Fact]
    public async Task Upload_ReplacesThePreviousPictureStraightAway()
    {
        var user = await _factory.CreateUserAsync("avatar-replace@test.com", "avatarreplace");
        var firstUrl = await UploadAsync(user, MakePng(300, 300, Color.Red));

        var secondUrl = await UploadAsync(user, MakePng(300, 300, Color.Blue));

        Assert.NotEqual(firstUrl, secondUrl); // a new URL, so no cache keeps showing the old one
        using var image = await FetchAnonymouslyAsync(secondUrl);
        var centre = image[128, 128];
        Assert.True(centre.B > centre.R, "the picture served should be the blue one");
    }

    [Theory]
    [InlineData("not an image")]
    [InlineData("over 5 MB")]
    public async Task Upload_OfAnUnacceptableFile_IsRejected(string problem)
    {
        var user = await _factory.CreateUserAsync($"avatar-bad-{problem.Replace(' ', '-')}@test.com", $"avatarbad{problem.Replace(" ", "")}");
        var bytes = problem == "not an image"
            ? Encoding.UTF8.GetBytes("definitely not an image")
            : new byte[6 * 1024 * 1024];

        var res = await user.Client.PutAsync("/api/users/me/avatar", FilePart(bytes));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
