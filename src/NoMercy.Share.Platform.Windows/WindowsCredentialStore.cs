using System.Runtime.InteropServices;
using System.Text;
using NoMercy.Share.Core;

namespace NoMercy.Share.Platform.Windows;

/// <summary>
/// Reads a generic credential from the Windows Credential Manager: the user name is the access key id and the
/// password is the secret. Add one with: cmdkey /generic:NAME /user:ACCESS_KEY_ID /pass:SECRET
/// </summary>
public sealed unsafe partial class WindowsCredentialStore : ICredentialStore
{
    private const uint GenericCredential = 1;

    public StoredCredential? Read(string name)
    {
        if (!CredRead(name, GenericCredential, 0, out nint handle))
        {
            return null;
        }

        try
        {
            CredentialW* credential = (CredentialW*)handle;
            string accessKeyId = Marshal.PtrToStringUni(credential->UserName) ?? string.Empty;
            string secret = Encoding.Unicode.GetString((byte*)credential->CredentialBlob, (int)credential->CredentialBlobSize);
            return new StoredCredential(accessKeyId, secret);
        }
        finally
        {
            CredFree(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CredentialW
    {
        public uint Flags;
        public uint Type;
        public nint TargetName;
        public nint Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public nint TargetAlias;
        public nint UserName;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredRead(string target, uint type, uint flags, out nint credential);

    [LibraryImport("advapi32.dll")]
    private static partial void CredFree(nint buffer);
}
