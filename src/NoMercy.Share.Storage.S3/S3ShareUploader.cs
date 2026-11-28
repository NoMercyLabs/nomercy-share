using System.Globalization;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Recent;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.S3;

/// <summary>Uploads through an S3-compatible API: R2 as "r2", other hosts as "s3". Credentials are read at upload time and never logged.</summary>
public sealed class S3ShareUploader(
    string providerName,
    StorageSettings settings,
    ICredentialStore credentials,
    TimeProvider? time = null,
    Func<int, int>? nextIndex = null) : IShareUploader
{
    /// <summary>The provider names this uploader answers to: one registration for each.</summary>
    public static readonly string[] ProviderNames = ["r2", "s3"];

    /// <summary>The object metadata that holds the UTC expiry (header x-amz-meta-expires-at); "never" when there is none.</summary>
    public const string ExpiresAtMetadata = "expires-at";

    public const string NeverValue = "never";

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly JsonSerializerOptions SidecarJson = new() { WriteIndented = true };

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ShareKeyPlanner _planner = new(settings, nextIndex);

    public string ProviderName => providerName;

    public bool IsConfigured => settings.IsSetUp;

    public async Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        if (!File.Exists(filePath))
        {
            throw new ShareUploadException(Strings.Get("error.upload.file"));
        }

        StoredCredential credential = ReadCredential();
        DateTimeOffset now = _time.GetUtcNow();
        SharePlan plan = _planner.Plan(expiry, now, filePath);
        string contentType = ContentTypes.For(Path.GetExtension(plan.Key));
        string? expiresAt = plan.ExpiresAt is { } at ? Utc(at) : null;

        try
        {
            using AmazonS3Client client = CreateClient(credential);
            await PutPictureAsync(client, plan.Key, filePath, contentType, expiresAt, cancellationToken);
            ShareSidecar sidecar = new(plan.Key, Utc(now), expiresAt, contentType, new FileInfo(filePath).Length);
            await PutSidecarAsync(client, plan.SidecarKey, sidecar, cancellationToken);
        }
        catch (AmazonServiceException error)
        {
            throw new ShareUploadException(Strings.Get("error.upload.service", Describe(error)), error);
        }
        catch (Exception error) when (error is AmazonClientException or HttpRequestException)
        {
            throw new ShareUploadException(Strings.Get("error.upload.network"), error);
        }

        return new ShareLink(settings.LinkFor(plan.Key), plan.ExpiresAt);
    }

    public bool CanDelete(RecentLink link) => false;

    public Task DeleteAsync(RecentLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static string Utc(DateTimeOffset time) => time.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private StoredCredential ReadCredential() => credentials.Read(settings.CredentialName) ?? throw new InvalidStorageSettingsException(
        "Storage.CredentialName",
        Strings.Get("error.setting.invalid", "Storage.CredentialName", Strings.Get("error.credential.missing", settings.CredentialName)));

    private async Task PutPictureAsync(AmazonS3Client client, string key, string filePath, string contentType, string? expiresAt, CancellationToken cancellationToken)
    {
        PutObjectRequest request = S3PutRequests.New(settings.Bucket, key, contentType);
        request.FilePath = filePath;
        request.Metadata[ExpiresAtMetadata] = expiresAt ?? NeverValue;
        await client.PutObjectAsync(request, cancellationToken);
    }

    private async Task PutSidecarAsync(AmazonS3Client client, string key, ShareSidecar sidecar, CancellationToken cancellationToken)
    {
        PutObjectRequest request = S3PutRequests.New(settings.Bucket, key, ContentTypes.Json);
        request.ContentBody = JsonSerializer.Serialize(sidecar, SidecarJson);
        await client.PutObjectAsync(request, cancellationToken);
    }

    private static string Describe(AmazonServiceException error) =>
        string.IsNullOrEmpty(error.ErrorCode) ? ((int)error.StatusCode).ToString(CultureInfo.InvariantCulture) : error.ErrorCode;

    private AmazonS3Client CreateClient(StoredCredential credential)
    {
        AmazonS3Config config = new()
        {
            ServiceURL = settings.Endpoint,
            ForcePathStyle = settings.ForcePathStyle,
            AuthenticationRegion = settings.Region,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };
        return new AmazonS3Client(new BasicAWSCredentials(credential.AccessKeyId, credential.SecretAccessKey), config);
    }
}

