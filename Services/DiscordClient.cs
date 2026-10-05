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

    // --------------------------------------------------
    // HUVUDTAVLA
    // --------------------------------------------------

    public async Task<string?> PublishBoardAsync(
        string content,
        string? messageId,
        CancellationToken cancellationToken = default)
    {
        var embeds =
            BuildBoardEmbeds(content);

        var payload = new
        {
            username = "Faceit Pigs",
            content = "",
            embeds
        };

        // Skapa tavlan första gången
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
                "✅ Ny FACEIT-tavla skapades.");

            return createdMessageId;
        }

        // Uppdatera befintlig tavla
        var editUrl =
            $"{_webhookUrl}/messages/{messageId}";

        using var responseUpdate =
            await _httpClient.PatchAsJsonAsync(
                editUrl,
                payload,
                cancellationToken);

        var updateBody =
            await responseUpdate.Content.ReadAsStringAsync(
                cancellationToken);

        if (!responseUpdate.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Discord-fel vid uppdatering av tavlan: " +
                $"{(int)responseUpdate.StatusCode} {updateBody}");
        }

        Console.WriteLine(
            "✅ FACEIT-tavlan uppdaterades.");

        return messageId;
    }

    // --------------------------------------------------
    // BILDUTMÄRKELSE
    // --------------------------------------------------

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

        // Ingen Message ID ännu:
        // skapa meddelandet och ladda upp bilden.
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return await CreateAwardWithImageAsync(
                title,
                description,
                fullPath,
                fileName,
                cancellationToken);
        }

        // Message ID finns:
        // uppdatera samma meddelande.
        //
        // Bilden laddades upp när meddelandet skapades,
        // så vi behöver inte skicka samma fil varje dag.
        var embed = new
        {
            title,
            description,
            image = new
            {
                url = $"attachment://{fileName}"
            }
        };

        var payload = new
        {
            content = "",
            embeds = new[]
            {
                embed
            }
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

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Discord-fel vid uppdatering av {title}: " +
                $"{(int)response.StatusCode} {body}");
        }

        Console.WriteLine(
            $"✅ {title} uppdaterades.");

        return messageId;
    }

    private async Task<string?> CreateAwardWithImageAsync(
        string title,
        string description,
        string imagePath,
        string fileName,
        CancellationToken cancellationToken)
    {
        var embed = new
        {
            title,
            description,
            image = new
            {
                url = $"attachment://{fileName}"
            }
        };

        var payload = new
        {
            username = "Faceit Pigs",
            content = "",
            embeds = new[]
            {
                embed
            }
        };

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
            File.OpenRead(imagePath);

        using var fileContent =
            new StreamContent(fileStream);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                GetContentType(imagePath));

        multipart.Add(
            fileContent,
            "files[0]",
            fileName);

        var url =
            $"{_webhookUrl}?wait=true";

        using var response =
            await _httpClient.PostAsync(
                url,
                multipart,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"Discord-fel vid skapande av {title}: " +
                $"{(int)response.StatusCode} {body}");
        }

        using var json =
            JsonDocument.Parse(body);

        var createdMessageId =
            json.RootElement
                .GetProperty("id")
                .GetString();

        Console.WriteLine(
            $"✅ {title} skapades.");

        return createdMessageId;
    }

    // --------------------------------------------------
    // EMBEDS
    // --------------------------------------------------

    private static object[] BuildBoardEmbeds(
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
                    ? "🐷 CS2 POWER RANKING"
                    : "🐷 CS2 POWER RANKING — FORTSÄTTNING",

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
                remaining[..splitIndex].Trim());

            remaining =
                remaining[splitIndex..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
        {
            parts.Add(remaining);
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