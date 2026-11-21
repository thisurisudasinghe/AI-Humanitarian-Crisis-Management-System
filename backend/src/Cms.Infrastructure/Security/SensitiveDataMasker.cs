using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cms.Infrastructure.Security;

/// <summary>
/// Masks sensitive values before they are written to the audit trail.
/// Works recursively over JSON objects, arrays and dictionaries.
/// </summary>
public static class SensitiveDataMasker
{
    public const string Redacted = "[REDACTED]";

    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "authorization",
        "credential",
        "email",
        "phone",
        "mobile",
        "medical",
        "health",
        "diagnos",
        "accessib",
        "disabilit",
        "filebase64",
        "filecontent",
        "storagepath",
        "filepath",
        "file_path",
        "path"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes <paramref name="details"/> and returns masked JSON.</summary>
    public static string? MaskObject(object? details)
    {
        if (details is null)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(details, details.GetType(), SerializerOptions);
        return MaskNode(node)?.ToJsonString(SerializerOptions);
    }

    /// <summary>Masks an already-serialized JSON document.</summary>
    public static string? MaskJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        var node = JsonNode.Parse(json);
        return MaskNode(node)?.ToJsonString(SerializerOptions);
    }

    /// <summary>Returns true when a property name is considered sensitive.</summary>
    public static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var lowered = key.ToLowerInvariant();
        return SensitiveKeyFragments.Any(fragment => lowered.Contains(fragment, StringComparison.Ordinal));
    }

    private static JsonNode? MaskNode(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;

            case JsonObject jsonObject:
            {
                var result = new JsonObject();
                foreach (var property in jsonObject)
                {
                    if (IsSensitiveKey(property.Key))
                    {
                        result[property.Key] = Redacted;
                    }
                    else
                    {
                        result[property.Key] = MaskNode(property.Value);
                    }
                }

                return result;
            }

            case JsonArray jsonArray:
            {
                var result = new JsonArray();
                foreach (var item in jsonArray)
                {
                    result.Add(MaskNode(item));
                }

                return result;
            }

            default:
                return node.DeepClone();
        }
    }
}
