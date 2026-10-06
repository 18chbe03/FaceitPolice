using System.Net;
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
        _webhookUrl =
            webhookUrl.TrimEnd('/');
    }

    // --------------------------------------------------
    // POWER RANKING
    // --------------------------------------------------

    public Task<string?> PublishBoardAsync(
        string content,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        return PublishTextMessageAsync(
            title:
                "🐷 CS2 POWER RANKING",
            continuationTitle:
                "🐷 CS2 POWER RANKING — FORTSÄTTNING",
            content:
                content,
            messageId:
                messageId,
            cancellationToken:
                cancellationToken);
    }

    // --------------------------------------------------
    // MAP ANALYTICS
    // --------------------------------------------------

    public Task<string?> PublishMapStatsAsync(
        string content,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        return PublishTextMessageAsync(
            title:
                "🗺️ MAP ANALYTICS",
            continuationTitle:
                "🗺️ MAP ANALYTICS — FORTSÄTTNING",
            content:
                content,
            messageId:
                messageId,
            cancellationToken:
                cancellationToken);
    }

    // --------------------------------------------------
    // PLAYER ANALYTICS
    // --------------------------------------------------

    public Task<string?> PublishAdvancedStatsAsync(
        string content,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        return PublishTextMessageAsync(
            title:
                "🧠 PLAYER ANALYTICS",
            continuationTitle:
                "🧠 PLAYER ANALYTICS — FORTSÄTTNING",
            content:
                content,
            messageId:
                messageId,
            cancellationToken:
                cancellationToken);
    }

    // --------------------------------------------------
    // TEXTMEDDELANDE
    // --------------------------------------------------

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

        if (string.IsNullOrWhiteSpace(
                messageId))
        {
            return await CreateTextMessageAsync(
                title,
                embeds,
                cancellationToken);
        }

        var payload = new
        {
            content = "",
            embeds
        };

        var editUrl =
            $"{_webhookUrl}/messages/{messageId}";

        using var response =
            await _httpClient.PatchAsJsonAsync(
                editUrl,
                payload,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            Console.WriteLine(
                $"⚠️ \"{title}\" finns inte längre.");

            Console.WriteLine(
                "Skapar ett nytt Discord-meddelande...");

            return await CreateTextMessageAsync(
                title,
                embeds,
                cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Discord-fel vid uppdatering av " +
                $"\"{title}\": " +
                $"{(int)response.StatusCode} {body}");
        }

        Console.WriteLine(
            $"✅ \"{title}\" uppdaterades.");

        return messageId;
    }

    private async Task<string?> CreateTextMessageAsync(
        string title,
        object[] embeds,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            username = "Faceit Pigs",
            content = "",
            embeds
        };

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
                $"Discord-fel vid skapande av " +
                $"\"{title}\": " +
                $"{(int)response.StatusCode} {body}");
        }

        using var json =
            JsonDocument.Parse(
                body);

        var messageId =
            json.RootElement
                .GetProperty("id")
                .GetString();

        Console.WriteLine(
            $"✅ \"{title}\" skapades.");

        Console.WriteLine(
            $"Nytt Discord-ID: {messageId}");

        return messageId;
    }

    // --------------------------------------------------
    // UTMÄRKELSE MED BILD
    // --------------------------------------------------

    public async Task<string?> PublishAwardWithImageAsync(
        string title,
        string description,
        string imagePath,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        var fullPath =
            Path.GetFullPath(
                imagePath);

        if (!File.Exists(
                fullPath))
        {
            throw new FileNotFoundException(
                $"Bilden hittades inte: {fullPath}");
        }

        if (string.IsNullOrWhiteSpace(
                messageId))
        {
            return await CreateAwardWithImageAsync(
                title,
                description,
                fullPath,
                cancellationToken);
        }

        var fileName =
            Path.GetFileName(
                fullPath);

        var embed = new
        {
            title,
            description,
            image = new
            {
                url =
                    $"attachment://{fileName}"
            }
        };

        var payload = new
        {
            content = "",
            embeds = new[]
            {
                embed
            },
            attachments = new[]
            {
                new
                {
                    id = 0,
                    filename = fileName
                }
            }
        };

        var payloadJson =
            JsonSerializer.Serialize(
                payload);

        using var multipart =
            CreateImageMultipart(
                payloadJson,
                fullPath,
                fileName);

        var editUrl =
            $"{_webhookUrl}/messages/{messageId}";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Patch,
                editUrl)
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

        if (response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            Console.WriteLine(
                $"⚠️ \"{title}\" finns inte längre.");

            Console.WriteLine(
                "Skapar ett nytt Discord-meddelande...");

            return await CreateAwardWithImageAsync(
                title,
                description,
                fullPath,
                cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Discord-fel för \"{title}\": " +
                $"{(int)response.StatusCode} {body}");
        }

        Console.WriteLine(
            $"✅ \"{title}\" uppdaterades.");

        return messageId;
    }

    private async Task<string?> CreateAwardWithImageAsync(
        string title,
        string description,
        string imagePath,
        CancellationToken cancellationToken)
    {
        var fileName =
            Path.GetFileName(
                imagePath);

        var embed = new
        {
            title,
            description,
            image = new
            {
                url =
                    $"attachment://{fileName}"
            }
        };

        var payload = new
        {
            username = "Faceit Pigs",
            content = "",
            embeds = new[]
            {
                embed
            },
            attachments = new[]
            {
                new
                {
                    id = 0,
                    filename = fileName
                }
            }
        };

        var payloadJson =
            JsonSerializer.Serialize(
                payload);

        using var multipart =
            CreateImageMultipart(
                payloadJson,
                imagePath,
                fileName);

        var url =
            $"{_webhookUrl}?wait=true";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
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
                $"Discord-fel vid skapande av " +
                $"\"{title}\": " +
                $"{(int)response.StatusCode} {body}");
        }

        using var json =
            JsonDocument.Parse(
                body);

        var messageId =
            json.RootElement
                .GetProperty("id")
                .GetString();

        Console.WriteLine(
            $"✅ \"{title}\" skapades.");

        Console.WriteLine(
            $"Nytt Discord-ID: {messageId}");

        return messageId;
    }

    private static MultipartFormDataContent CreateImageMultipart(
        string payloadJson,
        string imagePath,
        string fileName)
    {
        var multipart =
            new MultipartFormDataContent();

        multipart.Add(
            new StringContent(
                payloadJson,
                Encoding.UTF8,
                "application/json"),
            "payload_json");

        var bytes =
            File.ReadAllBytes(
                imagePath);

        var fileContent =
            new ByteArrayContent(
                bytes);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                GetContentType(
                    imagePath));

        multipart.Add(
            fileContent,
            "files[0]",
            fileName);

        return multipart;
    }

    // --------------------------------------------------
    // EMBEDS
    // --------------------------------------------------

    private static object[] BuildEmbeds(
        string title,
        string continuationTitle,
        string content)
    {
        const int maxDescriptionLength =
            3900;

        var parts =
            SplitText(
                content,
                maxDescriptionLength);

        return parts
            .Select((part, index) => new
            {
                title =
                    index == 0
                        ? title
                        : continuationTitle,

                description =
                    part
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

        while (remaining.Length >
               maxLength)
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
        return Path.GetExtension(
                path)
            .ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }
}