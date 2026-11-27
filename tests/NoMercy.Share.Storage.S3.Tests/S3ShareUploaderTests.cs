using System.Security.Cryptography;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Share.Core;
using NoMercy.Share.Core.Settings;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Storage.S3.Tests;

internal sealed class FakeCredentialStore(StoredCredential? credential) : ICredentialStore
{
    public string? AskedFor { get; private set; }

    public StoredCredential? Read(string name)
    {
        AskedFor = name;
        return credential;
    }

    public void Write(string name, StoredCredential credential) => throw new NotSupportedException();
}

public sealed class S3PutRequestsTests
{
    [Fact]
    public void AnUploadIsSignedWithoutChunkedStreamingBecauseR2RefusesIt()
    {
        // R2 answers "STREAMING-AWS4-HMAC-SHA256-PAYLOAD not implemented" to the SDK's default chunked signing.
        PutObjectRequest request = S3PutRequests.New("bucket", "key.png", "image/png");

        Assert.False(request.UseChunkEncoding);
    }
}

public sealed class S3ShareUploaderTests(S3MockFixture mock) : IClassFixture<S3MockFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    private static readonly StoredCredential Credential = new(S3MockFixture.AccessKeyId, S3MockFixture.SecretAccessKey);

    private StorageSettings Settings() => new()
    {
        Endpoint = mock.Endpoint.AbsoluteUri,
        Bucket = S3MockFixture.Bucket,
        PublicBaseUrl = "https://share.example.test",
        CredentialName = "made-up-credential"
    };

    private S3ShareUploader Uploader(StorageSettings settings, StoredCredential? credential, string name = "r2") =>
        new(name, settings, new FakeCredentialStore(credential), new FixedTime(Now));

    private static string GeneratedPng(out byte[] bytes)
    {
        bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. RandomNumberGenerator.GetBytes(2048)];
        string path = Path.Combine(Path.GetTempPath(), $"Private Holiday Photo {Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [DockerFact]
    public async Task AnUploadRoundTripsTheBytesAndWritesTheExpiryMetadataAndTheSidecar()
    {
        string file = GeneratedPng(out byte[] bytes);
        try
        {
            ShareLink link = await Uploader(Settings(), Credential).UploadAsync(file, Expiry.InDays(7));

            string key = link.Url.AbsolutePath.TrimStart('/');
            Assert.StartsWith("exp-7d/", key);
            Assert.EndsWith(".png", key);
            Assert.DoesNotContain("Private", link.Url.AbsoluteUri);
            Assert.Equal(Now.AddDays(7), link.ExpiresAt);

            using AmazonS3Client client = mock.Client();
            using GetObjectResponse picture = await client.GetObjectAsync(S3MockFixture.Bucket, key);
            using MemoryStream copy = new();
            await picture.ResponseStream.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
            Assert.Equal("image/png", picture.Headers.ContentType);
            Assert.Equal("2026-12-04T10:00:00Z", picture.Metadata[$"x-amz-meta-{S3ShareUploader.ExpiresAtMetadata}"]);

            using GetObjectResponse sidecar = await client.GetObjectAsync(S3MockFixture.Bucket, key + ".json");
            using StreamReader reader = new(sidecar.ResponseStream);
            ShareSidecar? meta = JsonSerializer.Deserialize<ShareSidecar>(await reader.ReadToEndAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Equal(key, meta?.Key);
            Assert.Equal("2026-12-04T10:00:00Z", meta?.ExpiresAt);
            Assert.Equal(bytes.Length, meta?.Bytes);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [DockerFact]
    public async Task NeverGoesUnderTheKeepPrefixAndSaysNeverInTheMetadata()
    {
        string file = GeneratedPng(out _);
        try
        {
            ShareLink link = await Uploader(Settings(), Credential, "s3").UploadAsync(file, Expiry.Never);

            string key = link.Url.AbsolutePath.TrimStart('/');
            Assert.StartsWith("keep/", key);
            Assert.Null(link.ExpiresAt);
            using AmazonS3Client client = mock.Client();
            GetObjectMetadataResponse head = await client.GetObjectMetadataAsync(S3MockFixture.Bucket, key);
            Assert.Equal(S3ShareUploader.NeverValue, head.Metadata[$"x-amz-meta-{S3ShareUploader.ExpiresAtMetadata}"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [DockerFact]
    public async Task ABucketThatDoesNotExistFailsWithAReasonThatHoldsNoSecret()
    {
        string file = GeneratedPng(out _);
        try
        {
            ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(
                () => Uploader(Settings() with { Bucket = "no-such-bucket" }, Credential).UploadAsync(file, Expiry.Never));

            Assert.Contains("NoSuchBucket", error.Message);
            Assert.DoesNotContain(S3MockFixture.SecretAccessKey, error.ToString());
            Assert.True(File.Exists(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task AMissingCredentialNamesTheSettingAndNeverReachesTheNetwork()
    {
        string file = GeneratedPng(out _);
        try
        {
            InvalidStorageSettingsException error = await Assert.ThrowsAsync<InvalidStorageSettingsException>(
                () => Uploader(Settings(), credential: null).UploadAsync(file, Expiry.Never));

            Assert.Equal("Storage.CredentialName", error.Setting);
            Assert.Contains("made-up-credential", error.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task AnInvalidSettingIsNamedBeforeTheCredentialIsRead()
    {
        FakeCredentialStore store = new(Credential);
        S3ShareUploader uploader = new("r2", Settings() with { Bucket = string.Empty }, store, new FixedTime(Now));

        InvalidStorageSettingsException error = await Assert.ThrowsAsync<InvalidStorageSettingsException>(
            () => uploader.UploadAsync(Path.Combine(Path.GetTempPath(), "share-not-here.png"), Expiry.Never));

        Assert.Equal("Storage.Bucket", error.Setting);
        Assert.Null(store.AskedFor);
    }

    [Fact]
    public async Task AnUnreachableEndpointFailsCalmlyAndKeepsTheFile()
    {
        string file = GeneratedPng(out _);
        try
        {
            StorageSettings settings = Settings() with { Endpoint = "http://127.0.0.1:1" };

            ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(() => Uploader(settings, Credential).UploadAsync(file, Expiry.Never));

            Assert.Contains("could not be reached", error.Message);
            Assert.DoesNotContain(S3MockFixture.SecretAccessKey, error.ToString());
            Assert.True(File.Exists(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task AMissingFileIsReportedBeforeAnythingIsSent()
    {
        ShareUploadException error = await Assert.ThrowsAsync<ShareUploadException>(
            () => Uploader(Settings(), Credential).UploadAsync(Path.Combine(Path.GetTempPath(), "share-not-here.png"), Expiry.Never));

        Assert.Contains("gone", error.Message);
    }

    [Fact]
    public void BothProviderNamesAreRegistered()
    {
        ServiceCollection services = new();
        services.AddSingleton(new LiveSettings(new ShareSettings { Storage = Settings() }));
        services.AddSingleton<ICredentialStore>(new FakeCredentialStore(Credential));
        services.AddS3Storage();

        using ServiceProvider provider = services.BuildServiceProvider();
        string[] names = [.. provider.GetServices<IShareUploader>().Select(u => u.ProviderName)];

        Assert.Equal(["r2", "s3"], names);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
