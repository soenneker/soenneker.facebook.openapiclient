using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Soenneker.Facebook.OpenApiClient.Item.Feed;
using Soenneker.Facebook.OpenApiClient.Item.Photos;

namespace Soenneker.Facebook.OpenApiClient.Tests;

public sealed class PublishingTests
{
    [Test]
    public async Task PostsTextLinksAndAttachedMediaWithExactEncoding()
    {
        using var http = new HttpClient(new Handler(async (request, _) =>
        {
            Check(request.RequestUri!.ToString() == "https://graph.facebook.com/v26.0/123/feed", "Feed URL");
            var form = await ReadForm(request);
            Check(form["message"] == "Hello & welcome + café", "Message encoding");
            Check(form["link"] == "https://example.com/?a=1&b=2", "Link encoding");
            Check(form["attached_media"] == """[{"media_fbid":"456"}]""", "Attached media JSON must stay a string");
            return Json("""{"id":"123_789"}""");
        }));
        var result = await Create(http)["123"].Feed.PostAsync(new FeedPostRequestBody
        {
            Message = "Hello & welcome + café", Link = "https://example.com/?a=1&b=2",
            AttachedMedia = """[{"media_fbid":"456"}]"""
        });
        Check(result?.Id == "123_789", "Post ID deserialized");
    }

    [Test]
    public async Task PostsPhotoByUrlAndReadsBothIds()
    {
        using var http = new HttpClient(new Handler(async (request, _) =>
        {
            Check(request.RequestUri!.AbsolutePath == "/v26.0/123/photos", "Photo URL");
            var form = await ReadForm(request);
            Check(form["url"] == "https://example.com/photo.jpg", "Photo URL field");
            Check(form["caption"] == "A photo", "Caption");
            return Json("""{"id":"456","post_id":"123_789"}""");
        }));
        var result = await Create(http)["123"].Photos.PostAsync(new PhotosPostRequestBody { Url = "https://example.com/photo.jpg", Caption = "A photo" });
        Check(result?.Id == "456" && result.PostId == "123_789", "Photo/post IDs deserialized");
    }

    [Test]
    public async Task ReadsPagedPostsAndPropagatesApiErrors()
    {
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(
            request.Method == HttpMethod.Get ? Json("""{"data":[{"id":"123_789","message":"hello"}],"paging":{"next":"https://graph.facebook.com/next"}}""")
            : Json("""{"error":{"message":"Permission denied","type":"OAuthException","code":200}}""", HttpStatusCode.Forbidden))));
        var client = Create(http);
        var page = await client["123"].Feed.GetAsync(config => config.QueryParameters.Fields = "id,message");
        Check(page?.Data?.Single().Id == "123_789", "Paged models");
        try { await client["123"].Feed.PostAsync(new FeedPostRequestBody { Message = "Denied" }); }
        catch (ApiException error) { Check(error.ResponseStatusCode == 403, "HTTP error status"); return; }
        throw new InvalidOperationException("API error was swallowed");
    }
    private static FacebookOpenApiClient Create(HttpClient http)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return new FacebookOpenApiClient(new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http));
    }

    private static async Task<Dictionary<string, string>> ReadForm(HttpRequestMessage request)
    {
        Check(request.Method == HttpMethod.Post, "POST request expected");
        Check(request.Headers.Authorization?.ToString() == "Bearer test-token", "Bearer token missing");
        Check(request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded", "Expected URL-encoded form");
        return (await request.Content!.ReadAsStringAsync()).Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2)).ToDictionary(x => WebUtility.UrlDecode(x[0]), x => WebUtility.UrlDecode(x.Length > 1 ? x[1] : ""));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
