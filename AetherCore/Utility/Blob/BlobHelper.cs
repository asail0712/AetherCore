using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace AetherCore.Utility.Blob
{
    public static class BlobHelper
    {
        public static string BuildBlobPath(string fileName, DateTime utcNow)
        {
            var ext     = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            var guid    = Guid.NewGuid().ToString("N");

            return $"temp/other/{utcNow:yyyy/MM}/{guid}{ext}";
        }

        public static Uri GenerateUploadSasUri(string connectionString, string containerName, string blobPath, TimeSpan expiresIn)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient                 = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient                      = containerClient.GetBlobClient(blobPath);

            if (!blobClient.CanGenerateSasUri)
                throw new InvalidOperationException("BlobClient cannot generate SAS. Check credentials (SharedKey/UserDelegation).");

            var now         = DateTimeOffset.UtcNow;
            var sasBuilder  = new BlobSasBuilder
            {
                BlobContainerName   = containerName,
                BlobName            = blobPath,
                Resource            = "b",
                StartsOn            = now.AddMinutes(-5),        // clock skew buffer
                ExpiresOn           = now.Add(expiresIn),
                Protocol            = SasProtocol.Https
            };

            // 上傳用：Create + Write 
            sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

            return blobClient.GenerateSasUri(sasBuilder);
        }

        public static DateTimeOffset GetExpiryFromSas(Uri sasUri)
        {
            // se=2026-01-23T...Z
            var query   = System.Web.HttpUtility.ParseQueryString(sasUri.Query);
            var se      = query["se"];
            if (DateTimeOffset.TryParse(se, out var dto)) return dto;
            return DateTimeOffset.UtcNow; // fallback（理論上不會走到）
        }

        public static string ToBlobUrl(string uploadUrl)
        {
            var uri = new Uri(uploadUrl);
            return uri.GetLeftPart(UriPartial.Path);
        }

        public static class BlobSasHelper
        {
            public static string CreateReadSasUrl(
                string blobUrl,                 // 乾淨 BlobUrl（DB 存的那個）
                string accountName,
                string accountKey,
                int expiresMinutes = 5)
            {
                var blobUri = new Uri(blobUrl);

                // blobUri.PathAndQuery: /container/blobname
                // 解析 container 與 blob name
                var segments = blobUri.AbsolutePath.TrimStart('/').Split('/', 2);
                if (segments.Length < 2)
                    throw new ArgumentException("Invalid blobUrl, cannot parse container/blob name.");

                string containerName    = segments[0];
                string blobName         = segments[1];

                var credential = new StorageSharedKeyCredential(accountName, accountKey);

                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName   = containerName,
                    BlobName            = blobName,
                    Resource            = "b", // b = blob
                    ExpiresOn           = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes)
                };

                // 只給「讀取」權限
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                // 組合成完整 Read SAS URL
                var sasQuery = sasBuilder.ToSasQueryParameters(credential).ToString();
                return $"{blobUrl}?{sasQuery}";
            }
        }
    }
}
