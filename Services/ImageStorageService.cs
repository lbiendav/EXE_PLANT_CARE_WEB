using Google.Cloud.Firestore;

namespace HomePlant.Services;

public sealed class ImageStorageService
{
    // A base64-encoded 512 KiB chunk is about 683 KiB, safely below
    // Firestore's 1 MiB document limit after field metadata is included.
    private const int ChunkSize = 512 * 1024;
    private const int MaxChunkCount = 64;
    public const long MaxImageBytes = 32 * 1024 * 1024;

    private readonly FirestoreDb _db;
    private readonly ILogger<ImageStorageService> _logger;

    public ImageStorageService(
        FirestoreService firestore,
        ILogger<ImageStorageService> logger)
    {
        _db = firestore.Db;
        _logger = logger;
    }

    public async Task<string?> Upload(
        IFormFile? photo,
        CancellationToken cancellationToken = default)
    {
        if (photo == null || photo.Length == 0 || photo.Length > MaxImageBytes)
            return null;

        var imageId = Guid.NewGuid().ToString("N");
        var imageDocument = _db.Collection("uploaded_images").Document(imageId);
        var chunkCount = 0;

        try
        {
            await using var source = photo.OpenReadStream();
            var buffer = new byte[ChunkSize];

            while (true)
            {
                var bytesRead = 0;
                while (bytesRead < buffer.Length)
                {
                    var read = await source.ReadAsync(
                        buffer.AsMemory(bytesRead, buffer.Length - bytesRead),
                        cancellationToken);
                    if (read == 0)
                        break;
                    bytesRead += read;
                }

                if (bytesRead == 0)
                    break;
                if (chunkCount >= MaxChunkCount)
                {
                    await DeleteDocuments(imageDocument, chunkCount, CancellationToken.None);
                    return null;
                }

                await imageDocument.Collection("chunks")
                    .Document(chunkCount.ToString("D4"))
                    .SetAsync(new Dictionary<string, object>
                    {
                        ["data"] = Convert.ToBase64String(buffer, 0, bytesRead)
                    }, cancellationToken: cancellationToken);
                chunkCount++;
            }

            if (chunkCount == 0)
                return null;

            var contentType = photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? photo.ContentType
                : "application/octet-stream";
            await imageDocument.SetAsync(new Dictionary<string, object>
            {
                ["chunkCount"] = chunkCount,
                ["contentType"] = contentType,
                ["fileName"] = Path.GetFileName(photo.FileName),
                ["length"] = photo.Length,
                ["createdAt"] = Timestamp.GetCurrentTimestamp()
            }, cancellationToken: cancellationToken);

            return $"/Image/{imageId}";
        }
        catch (OperationCanceledException)
        {
            await DeleteDocuments(imageDocument, chunkCount, CancellationToken.None);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not store uploaded image {ImageId} in Firestore.", imageId);
            await DeleteDocuments(imageDocument, chunkCount, CancellationToken.None);
            return null;
        }
    }

    public async Task Delete(
        string? imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetManagedImageId(imageUrl, out var imageId))
            return;

        var imageDocument = _db.Collection("uploaded_images").Document(imageId);

        try
        {
            var chunks = await imageDocument.Collection("chunks")
                .GetSnapshotAsync(cancellationToken);
            var batch = _db.StartBatch();

            foreach (var chunk in chunks.Documents)
                batch.Delete(chunk.Reference);

            batch.Delete(imageDocument);
            await batch.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not delete uploaded image {ImageId} from Firestore.", imageId);
        }
    }

    public async Task<StoredImage?> Get(
        string imageId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(imageId, "N", out _))
            return null;

        try
        {
            var imageDocument = _db.Collection("uploaded_images").Document(imageId);
            var metadata = await imageDocument.GetSnapshotAsync(cancellationToken);
            if (!metadata.Exists)
                return null;

            var chunkCount = metadata.GetValue<long>("chunkCount");
            var length = metadata.GetValue<long>("length");
            if (chunkCount is < 1 or > MaxChunkCount || length is < 1 or > MaxImageBytes)
                return null;

            var reads = Enumerable.Range(0, (int)chunkCount)
                .Select(index => imageDocument.Collection("chunks")
                    .Document(index.ToString("D4"))
                    .GetSnapshotAsync(cancellationToken))
                .ToArray();
            var chunks = await Task.WhenAll(reads);

            using var output = new MemoryStream((int)length);
            foreach (var chunk in chunks)
            {
                if (!chunk.Exists)
                    return null;
                var bytes = Convert.FromBase64String(chunk.GetValue<string>("data"));
                await output.WriteAsync(bytes, cancellationToken);
            }

            if (output.Length != length)
                return null;

            return new StoredImage(
                output.ToArray(),
                metadata.GetValue<string>("contentType"));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not read uploaded image {ImageId} from Firestore.", imageId);
            return null;
        }
    }

    public sealed record StoredImage(byte[] Bytes, string ContentType);

    private static bool TryGetManagedImageId(string? imageUrl, out string imageId)
    {
        const string prefix = "/Image/";
        imageId = "";

        if (imageUrl == null ||
            !imageUrl.StartsWith(prefix, StringComparison.Ordinal) ||
            imageUrl.Length != prefix.Length + 32)
            return false;

        imageId = imageUrl[prefix.Length..];
        return Guid.TryParseExact(imageId, "N", out _);
    }

    private async Task DeleteDocuments(
        DocumentReference imageDocument,
        int chunkCount,
        CancellationToken cancellationToken)
    {
        try
        {
            var batch = _db.StartBatch();
            for (var index = 0; index < chunkCount; index++)
            {
                batch.Delete(imageDocument.Collection("chunks")
                    .Document(index.ToString("D4")));
            }

            batch.Delete(imageDocument);
            await batch.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not clean up incomplete image upload {ImageId}.", imageDocument.Id);
        }
    }
}
