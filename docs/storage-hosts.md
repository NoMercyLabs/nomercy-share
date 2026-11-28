# Storage hosts

NoMercy Share uploads each capture to a host you control and puts the link on the clipboard. Choose the host in Settings under Sharing, or set `Storage.Provider` in [settings.json](settings-file.md) to `r2`, `s3` or `discord`.

## R2 and S3

`r2` and `s3` use the same S3 client; R2 is the default. It uses the AWS SDK for .NET with a custom `Storage.Endpoint` and path-style addressing: the SDK already signs, retries and reads S3 errors, so a hand-written SigV4 client would be more code to keep correct. R2 needs `Storage.Region` = `auto`, which is the default.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Storage.Endpoint`, `Storage.Bucket`, `Storage.PublicBaseUrl`, `Storage.CredentialName` | empty | Required. A missing one fails the upload with a notice that names it, for example `Storage.Bucket`. |
| `Storage.KeyLength` | 10 | Length of the random id in the object key (letters and digits). The key never holds the local file name. |
| `Storage.ExpiryBucketDays` | `[1, 7, 30, 90]` | The lifetimes that have a prefix on the host. |
| `Storage.ExpiryPrefixPattern`, `Storage.KeepPrefix`, `Storage.SidecarSuffix` | `exp-{0}d`, `keep`, `.json` | Folder names and the sidecar suffix. |
| `Expiry.DefaultDays` | 30 (null = never) | The lifetime of an upload that names none. |
| `Clipboard.Content` | `Link` | `Link`, `Image` or `Both` after a capture is shared. |

The link is `Storage.PublicBaseUrl` + `/` + the key, for example `https://share.example.test/exp-7d/aB3dE5gH7j.png`.

### Credentials

`Storage.CredentialName` names a generic credential in the Windows Credential Manager. The user name is the access key id and the password is the secret. The Settings window writes it for you (default name `nomercy-share`). To write it by hand:

```
cmdkey /generic:NAME /user:ACCESS_KEY_ID /pass:SECRET
```

### Expiry

The host deletes expired files; the app deletes a file only when you [delete its link](#deleting-a-shared-link). An upload goes under the smallest prefix that covers its lifetime (`exp-7d/` for 2 to 7 days; longer than the largest goes under the largest), and `never` goes under `keep/`. The object carries the metadata `x-amz-meta-expires-at` (UTC, for example `2026-12-04T10:00:00Z`, or `never`) and a sidecar `<key>.json` with the key, upload time, expiry, content type and size.

The link's expiry is the prefix's deletion time, so it is never earlier than asked. The host needs one lifecycle rule per prefix. On R2, add "delete objects after N days" with the prefix `exp-1d/`, `exp-7d/`, `exp-30d/` and `exp-90d/` (N matching the days in the name). Nothing deletes `keep/`.

### When an upload fails

A failed upload keeps the PNG, shows `Upload failed: <reason>. The file is kept.` on the bar and leaves the clipboard as it was. The reason is the service's error code or "could not be reached". No secret is in any message.

## Discord

`Storage.Provider` = `discord` uploads the file itself to a channel through a webhook and puts the Discord file link on the clipboard. The webhook address is a secret in the Credential Manager (`Discord.CredentialName`, default `nomercy-share-discord`), never in settings.json.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Discord.CredentialName` | `nomercy-share-discord` | The stored credential that holds the webhook address. |
| `Discord.MaxUploadMegabytes` | 10 | The file size cap. A bigger file fails before the upload; recordings are squeezed to the smaller of this and `Recording.MaxSizeMegabytes`. |
| `Discord.TimeoutSeconds` | 10 | How long one upload may take. |

Discord links are signed and expire (the `ex` value in the link, shown in the recent list), so `Expiry.DefaultDays` does not apply.

## Deleting a shared link

The trash button in the recent list asks first, inside the row. Every provider is asked whether it made the link, not only the selected one, so a link stays deletable after you switch providers. The local capture file is never deleted.

| Link | What a delete does | What it needs |
| --- | --- | --- |
| R2 or S3, under the current `PublicBaseUrl` | Deletes the object and then its sidecar. | A key that may delete objects, and the storage settings and credential the app has now. |
| Discord, uploaded with its message id kept | Deletes the webhook message that holds the file. | The same webhook that uploaded it. |
| Any other link, such as an older Discord upload | Removes the link from the list only. The upload stays online. | Nothing. |

If the host refuses or cannot be reached, the link stays in the list and its row says why. For Discord that includes "not found", because it can also mean the webhook changed. A browser can show its cached copy for up to one hour, and Discord can keep its preview.

## The share page

Links on a host behind the [share page Worker](../host/README.md) open a viewer page. The Worker only serves keys under `exp-<N>d/` or `keep/`, and never a sidecar `.json`.
