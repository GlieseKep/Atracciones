using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AtraccionesService.API.Tests.Infrastructure;

public static class HttpAssertions
{
    /// <summary>Verifica el status y que el cuerpo sea RFC 7807 <c>application/problem+json</c>.</summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"Esperado {(int)expected}, recibido {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("title", out _));
        Assert.True(problem.TryGetProperty("type", out _));
        Assert.False(body.Contains("   at ", StringComparison.Ordinal), "La respuesta no debe incluir stack traces.");
        return problem;
    }

    public static async Task<JsonElement> AssertJsonAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"Esperado {(int)expected}, recibido {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    public static HttpRequestMessage Post(string url, object body, Guid? idempotencyKey = null, string? rawKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (rawKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", rawKey);
        }
        else if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey.Value.ToString());
        }

        return request;
    }

    public static HttpRequestMessage PostJson(string url, string json, Guid? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey.Value.ToString());
        }

        return request;
    }
}
