namespace NoMercy.Share.Core.Recent;

public enum RecentRowStep
{
    Normal,
    Asking,
    Deleting,
    Failed
}

/// <summary>Where a row is in being deleted, and the words it shows there.</summary>
public sealed record RecentRowState
{
    private RecentRowState(RecentRowStep step, bool fromHost, string? title = null, string? detail = null, string? confirmLabel = null)
    {
        Step = step;
        FromHost = fromHost;
        Title = title;
        Detail = detail;
        ConfirmLabel = confirmLabel;
    }

    public static RecentRowState Normal { get; } = new(RecentRowStep.Normal, fromHost: false);

    public RecentRowStep Step { get; }

    /// <summary>True when confirming deletes the upload from its host, false when it only takes the link off the list.</summary>
    public bool FromHost { get; }

    public string? Title { get; }

    public string? Detail { get; }

    /// <summary>The label of the button that deletes, removes or tries again; null when there is nothing to answer.</summary>
    public string? ConfirmLabel { get; }

    public bool IsNormal => Step == RecentRowStep.Normal;

    /// <summary>True while the row shows a confirm button and Cancel.</summary>
    public bool OffersAnswer => ConfirmLabel is not null;

    public static RecentRowState Asking(bool fromHost) => new(
        RecentRowStep.Asking,
        fromHost,
        Strings.Get(fromHost ? "recent.delete.host.question" : "recent.delete.list.question"),
        Strings.Get(fromHost ? "recent.delete.host.detail" : "recent.delete.list.detail"),
        Strings.Get(fromHost ? "ask.delete" : "ask.remove"));

    public static RecentRowState Deleting(bool fromHost) => new(RecentRowStep.Deleting, fromHost, Strings.Get("recent.deleting"));

    public static RecentRowState Failed(bool fromHost, string reason) =>
        new(RecentRowStep.Failed, fromHost, Strings.Get("recent.delete.failed"), reason, Strings.Get("recent.delete.retry"));
}
