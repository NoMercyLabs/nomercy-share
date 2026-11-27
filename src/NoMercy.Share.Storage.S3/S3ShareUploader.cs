using System.Globalization;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.S3;

/// <summary>What the sidecar file next to an object says, for a host script that acts on expiry.</summary>
public sealed record ShareSidecar(string Key, string UploadedAt, string? ExpiresAt, string ContentType, long Bytes);

/// <summary>Uploads through an S3-compatible API: R2 as "r2", other hosts as "s3". Credentials are read at upload time and never logged.</summary>
public sealed class S3ShareUploader(
    string providerName,
    StorageSettings settings,
    ICredentialStore credentials,
    TimeProvider? time = null,
    Func<int, int>? nextIndex = null) : IShareUploader
{
    public const string R2Name = "r2";

    public const string S3Name = "s3";

    /// <summary>The object metadata that holds the UTC expiry (header x-amz-meta-expires-at); "never" when there is none.</summary>
    public const string ExpiresAtMetadata = "expires-at";

    public const string NeverValue = "never";

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly JsonSerializerOptions SidecarJson = new() { WriteIndented = true };

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ShareKeyPlanner _planner = new(settings, nextIndex);

    public string ProviderName => providerName;

    public bool IsConfigured => settings.IsSetUp;

    public void Validate() => settings.Validate();

    public async Task<ShareLink> UploadAsync(string filePath, Expiry expiry, CancellationToken cancellationToken = default)
    {
        Validate();
        if (!File.Exists(filePath))
        {
            throw new ShareUploadException(Strings.Get("error.upload.file"));
        }

        StoredCredential credential = credentials.Read(settings.CredentialName) ?? throw new InvalidStorageSettingsException(
            "Storage.CredentialName",
            Strings.Get("error.setting.invalid", "Storage.CredentialName", Strings.Get("error.credential.missing", settings.CredentialName)));

        DateTimeOffset now = _time.GetUtcNow();
        SharePlan plan = _planner.Plan(expiry, now, filePath);
        string contentType = ContentTypes.For(Path.GetExtension(plan.Key));
        string expiresAt = plan.ExpiresAt is { } at ? at.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture) : NeverValue;

        try
        {
            using AmazonS3Client client = CreateClient(credential);
            PutObjectRequest picture = new() { BucketName = settings.Bucket, Key = plan.Key, FilePath = filePath, ContentType = contentType };
            picture.Metadata[ExpiresAtMetadata] = expiresAt;
            await client.PutObjectAsync(picture, cancellationToken);

            ShareSidecar sidecar = new(plan.Key, now.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                plan.ExpiresAt is null ? null : expiresAt, contentType, new FileInfo(filePath).Length);
            PutObjectRequest sidecarRequest = new()
            {
                BucketName = settings.Bucket,
                Key = plan.SidecarKey,
                ContentBody = JsonSerializer.Serialize(sidecar, SidecarJson),
                ContentType = ContentTypes.Json
            };
            await client.PutObjectAsync(sidecarRequest, cancellationToken);
        }
        catch (AmazonServiceException error)
        {
            throw new ShareUploadException(Strings.Get("error.upload.service", Describe(error)), error);
        }
        catch (AmazonClientException error)
        {
            throw new ShareUploadException(Strings.Get("error.upload.network"), error);
        }
        catch (HttpRequestException error)
        {
            throw new ShareUploadException(Strings.Get("error.upload.network"), error);
        }

        return new ShareLink(settings.LinkFor(plan.Key), plan.ExpiresAt);
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

internal static class ContentTypes
{
    public const string Json = "application/json";

    public static string For(string extension) => extension switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        _ => "application/octet-stream"
    };
}

public static class S3StorageExtensions
{
    /// <summary>Registers the S3-compatible uploader under the provider names "r2" and "s3"; the storage settings select one.</summary>
    public static IServiceCollection AddS3Storage(this IServiceCollection services)
    {
        services.AddSingleton<IShareUploader>(sp => new S3ShareUploader(
            S3ShareUploader.R2Name, sp.GetRequiredService<StorageSettings>(), sp.GetRequiredService<ICredentialStore>()));
        services.AddSingleton<IShareUploader>(sp => new S3ShareUploader(
            S3ShareUploader.S3Name, sp.GetRequiredService<StorageSettings>(), sp.GetRequiredService<ICredentialStore>()));
        return services;
    }
}
