using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;

namespace HomePlant.Services;

public sealed class FirestoreXmlRepository(FirestoreDb firestore) : IXmlRepository
{
    private CollectionReference Keys => firestore.Collection("system_data_protection_keys");

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var snapshot = Keys.GetSnapshotAsync().GetAwaiter().GetResult();
        return snapshot.Documents
            .Select(document => XElement.Parse(document.GetValue<string>("xml"), LoadOptions.PreserveWhitespace))
            .ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(friendlyName))).ToLowerInvariant();
        Keys.Document(id).SetAsync(new Dictionary<string, object>
        {
            ["friendlyName"] = friendlyName,
            ["xml"] = element.ToString(SaveOptions.DisableFormatting),
            ["createdAt"] = Timestamp.GetCurrentTimestamp()
        }).GetAwaiter().GetResult();
    }
}

public sealed class ConfigureFirestoreDataProtection(FirestoreXmlRepository repository)
    : IConfigureOptions<KeyManagementOptions>
{
    public void Configure(KeyManagementOptions options) => options.XmlRepository = repository;
}
