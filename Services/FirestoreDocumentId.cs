using System.Text;

namespace HomePlant.Services;

/// <summary>
/// Validates IDs before they are passed to Firestore's Document(...) API.
/// Firestore treats slashes as path separators and throws for empty paths, so
/// request-controlled IDs must be a single, non-empty document segment.
/// </summary>
public static class FirestoreDocumentId
{
    private const int MaxUtf8Bytes = 1_500;

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains('/') &&
        value is not "." and not ".." &&
        Encoding.UTF8.GetByteCount(value) <= MaxUtf8Bytes;
}
