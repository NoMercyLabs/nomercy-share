using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.S3;

namespace NoMercy.Share.Storage.S3.Tests;

/// <summary>Runs S3Mock in a Docker container on a free loopback port and removes it again, by its own name.</summary>
public sealed class S3MockFixture : IAsyncLifetime
{
    public const string Bucket = "share-test";

    public const string AccessKeyId = "made-up-access-key";

    public const string SecretAccessKey = "made-up-secret-key";

    private const string Image = "adobe/s3mock";

    private const int ContainerPort = 9090;

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    private readonly string _container = $"share-s3-test-{Guid.NewGuid():N}"[..24];

    /// <summary>S3Mock is a Linux image, so a Docker engine in Windows-container mode (the GitHub Windows runner) counts as absent.</summary>
    public static bool DockerAvailable { get; } = Execute("info", "--format", "{{.OSType}}") is (0, "linux");

    public Uri Endpoint { get; private set; } = new("http://127.0.0.1");

    public async Task InitializeAsync()
    {
        if (!DockerAvailable)
        {
            return;
        }

        int port = FreePort();
        Endpoint = new Uri($"http://127.0.0.1:{port}");
        Run("run", "-d", "--rm", "-p", $"127.0.0.1:{port}:{ContainerPort}", "-e", $"COM_ADOBE_TESTING_S3MOCK_STORE_INITIAL_BUCKETS={Bucket}", "--name", _container, Image);
        await WaitUntilReadyAsync();
    }

    public Task DisposeAsync()
    {
        if (DockerAvailable)
        {
            Run("stop", "-t", "2", _container);
        }

        return Task.CompletedTask;
    }

    public AmazonS3Client Client() => new(new BasicAWSCredentials(AccessKeyId, SecretAccessKey), new AmazonS3Config
    {
        ServiceURL = Endpoint.AbsoluteUri,
        ForcePathStyle = true,
        AuthenticationRegion = "auto"
    });

    private async Task WaitUntilReadyAsync()
    {
        using AmazonS3Client client = Client();
        DateTime deadline = DateTime.UtcNow + ReadyTimeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await client.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request { BucketName = Bucket });
                return;
            }
            catch (Exception error) when (error is AmazonServiceException or AmazonClientException or HttpRequestException or IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        throw new TimeoutException("S3Mock did not become ready.");
    }

    private static int FreePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static int Run(params string[] arguments) => Execute(arguments).ExitCode;

    private static (int ExitCode, string Output) Execute(params string[] arguments)
    {
        ProcessStartInfo start = new("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (-1, string.Empty);
        }
    }
}

/// <summary>A fact that skips itself when Docker is not available.</summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!S3MockFixture.DockerAvailable)
        {
            Skip = "Docker is not available.";
        }
    }
}
