using Amazon.S3.Model;

namespace NoMercy.Share.Storage.S3;

/// <summary>Every upload request starts here, so each one works on R2 as well as on other S3 hosts.</summary>
public static class S3PutRequests
{
    public static PutObjectRequest New(string bucket, string key, string contentType) =>
        new() { BucketName = bucket, Key = key, ContentType = contentType, UseChunkEncoding = false };
}
