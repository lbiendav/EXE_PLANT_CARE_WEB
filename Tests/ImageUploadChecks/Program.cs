using HomePlant.Services;

Check("image/png", new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
Check("image/jpeg", new byte[] { 255, 216, 255, 224 });
Check("image/gif", "GIF89a"u8.ToArray());
Check("image/webp", new byte[] { 82, 73, 70, 70, 0, 0, 0, 0, 87, 69, 66, 80 });
Check(null, "<script>alert(1)</script>"u8.ToArray());
Check(null, "%PDF-1.7"u8.ToArray());
Check(null, new byte[] { 255, 216 });
CheckDocumentId("document-id", true);
CheckDocumentId("  ", false);
CheckDocumentId("nested/document", false);
CheckDocumentId(".", false);
CheckDocumentId("..", false);
CheckDocumentId(new string('ế', 751), false);
Console.WriteLine("Image upload signature checks passed.");

static void Check(string? expected, byte[] bytes)
{
    var actual = ImageStorageService.DetectContentType(bytes);
    if (actual != expected)
        throw new Exception($"Expected {expected ?? "rejection"}, got {actual ?? "rejection"}.");
}

static void CheckDocumentId(string value, bool expected)
{
    var actual = FirestoreDocumentId.IsValid(value);
    if (actual != expected)
        throw new Exception($"Expected document ID validity {expected}, got {actual}.");
}
