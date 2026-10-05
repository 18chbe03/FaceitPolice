using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace FaceitPolice.Services;

public sealed class DiscordClient
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;

    public DiscordClient(
        HttpClient httpClient,
        string webhookUrl)
    {
        _httpClient = httpClient;
        _webhookUrl = webhookUrl.TrimEnd('/');
    }

    public Task<string?> PublishBoardAsync(
        string content,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        return PublishTextMessageAsync(
            title: "🐷 CS2 POWER RANKING",
            continuationTitle:
                "🐷 CS2 POWER RANKING — FORTSÄTTNING",
            content: content,
            messageId: messageId,
            cancellationToken: cancellationToken);
    }

    public Task<string?> PublishMapStatsAsync(
        string content,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        return PublishTextMessageAsync(
            title: "🗺️ GRUPPENS KARTSTATISTIK",
            continuationTitle:
                "🗺️ KARTSTATISTIK — FORTSÄTTNING",
            content: content,
            messageId: messageId,
            cancellationToken: cancellationToken);
    }

    private async Task<string?> PublishTextMessageAsync(
        string title,
        string continuationTitle,
        string content,
        string? messageId,
        CancellationToken cancellationToken)
    {
        var embeds =
            BuildEmbeds(
                title,
                continuationTitle,
                content);

        var payload = new
        {
            username = "Faceit Pigs",
            content = "",
            embeds
        };

        if (string.IsNullOrWhiteSpace(messageId))
        {
            var url =
                $"{_webhookUrl}?wait=true";

            using var response =
                await _httpClient.PostAsJsonAsync(
                    url,
                    payload,
                    cancellationToken);

            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Discord-fel: " +
                    $"{(int)response.StatusCode} {body}");
            }

            using var json =
                JsonDocument.Parse(body);

            var createdMessageId =
                json.RootElement
                    .GetProperty("id")
                    .GetString();

            Console.WriteLine(
                $"✅ \"{title}\" skapades.");

            return createdMessageId;
        }

        var editUrl =
            $"{_webhookUrl}/messages/{messageId}";

        using var editResponse =
            await _httpClient.PatchAsJsonAsync(
                editUrl,
                payload,
                cancellationToken);

        var editBody =
            await editResponse.Content.ReadAsStringAsync(
                cancellationToken);

        if (!editResponse.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Discord-fel vid uppdatering av " +
                $"\"{title}\": " +
                $"{(int)editResponse.StatusCode} " +
                $"{editBody}");
        }

        Console.WriteLine(
            $"✅ \"{title}\" uppdaterades.");

        return messageId;
    }

    public async Task<string?> PublishAwardWithImageAsync(
        string title,
        string description,
        string imagePath,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        var fullPath =
            Path.GetFullPath(imagePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Bilden hittades inte: {fullPath}");
        }

        var fileName =
            Path.GetFileName(fullPath);

        var embed = new
        {
            title,
            description,
            image = new
            {
                url = $"attachment://{fileName}"
            }
        };

        // attachments säkerställer att bilden även
        // fungerar när befintligt meddelande uppdateras.
        var payload =
            new Dictionary<string, object>
            {
                ["content"] = "",
                ["embeds"] = new[]
                {
                    embed
                },
                ["attachments"] = new[]
                {
                    new
                    {
                        id = 0,
                        filename = fileName
                    }
                }
            };

        var creating =
            string.IsNullOrWhiteSpace(messageId);

        if (creating)
        {
            payload["username"] =
                "Faceit Pigs";
        }

        var payloadJson =
            JsonSerializer.Serialize(payload);

        using var multipart =
            new MultipartFormDataContent();

        multipart.Add(
            new StringContent(
                payloadJson,
                Encoding.UTF8,
                "application/json"),
            "payload_json");

        await using var fileStream =
            File.OpenRead(fullPath);

        using var fileContent =
            new StreamContent(fileStream);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                GetContentType(fullPath));

        multipart.Add(
            fileContent,
            "files[0]",
            fileName);

        var url =
            creating
                ? $"{_webhookUrl}?wait=true"
                : $"{_webhookUrl}/messages/{messageId}";

        using var request =
            new HttpRequestMessage(
                creating
                    ? HttpMethod.Post
                    : HttpMethod.Patch,
                url)
            {
                Content = multipart
            };

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Discord-fel för \"{title}\": " +
                $"{(int)response.StatusCode} {body}");
        }

        if (!creating)
        {
            Console.WriteLine(
                $"✅ \"{title}\" uppdaterades.");

            return messageId;
        }

        using var json =
            JsonDocument.Parse(body);

        var createdMessageId =
            json.RootElement
                .GetProperty("id")
                .GetString();

        Console.WriteLine(
            $"✅ \"{title}\" skapades.");

        return createdMessageId;
    }

    private static object[] BuildEmbeds(
        string title,
        string continuationTitle,
        string content)
    {
        const int maxDescriptionLength = 3900;

        var parts =
            SplitText(
                content,
                maxDescriptionLength);

        return parts
            .Select((part, index) => new
            {
                title = index == 0
                    ? title
                    : continuationTitle,

                description = part
            })
            .Cast<object>()
            .ToArray();
    }

    private static List<string> SplitText(
        string content,
        int maxLength)
    {
        var parts =
            new List<string>();

        var remaining =
            content.Trim();

        while (remaining.Length > maxLength)
        {
            var splitIndex =
                remaining.LastIndexOf(
                    '\n',
                    maxLength);

            if (splitIndex <= 0)
            {
                splitIndex =
                    maxLength;
            }

            parts.Add(
                remaining[..splitIndex]
                    .Trim());

            remaining =
                remaining[splitIndex..]
                    .Trim();
        }

        if (!string.IsNullOrWhiteSpace(
                remaining))
        {
            parts.Add(
                remaining);
        }

        return parts;
    }

    private static string GetContentType(
        string path)
    {
        return Path.GetExtension(path)
            .ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }
}