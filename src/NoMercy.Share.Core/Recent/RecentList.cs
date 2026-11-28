using System.Collections.ObjectModel;
using System.Diagnostics;
using NoMercy.Share.Core.Sharing;

namespace NoMercy.Share.Core.Recent;

/// <summary>The rows of the recent list, and deleting one of them from inside its row.</summary>
public sealed class RecentList(RecentLinks? links, ShareDeletion? deletion) : ObservableObject
{
    private readonly ObservableCollection<RecentRow> _rows = [];

    /// <summary>Newest first. A delete takes out only its own row, so the others stay where they are.</summary>
    public IReadOnlyList<RecentRow> Rows => _rows;

    public bool IsEmpty => _rows.Count == 0;

    /// <summary>Reads the stored links again. Returns false when there is nowhere to read them from.</summary>
    public bool TryLoad(DateTimeOffset now)
    {
        if (links is null)
        {
            return false;
        }

        // A row that is deleting or has failed is kept, because the running delete reports to that object.
        Dictionary<RecentLink, RecentRow> busy = _rows.Where(row => !row.State.IsNormal).ToDictionary(row => row.Link);
        _rows.Clear();
        foreach (RecentLink link in links.Load())
        {
            _rows.Add(busy.GetValueOrDefault(link) ?? new RecentRow(link, now));
        }

        OnChanged(nameof(IsEmpty));
        return true;
    }

    /// <summary>Turns the row into the question. Only one row asks at a time.</summary>
    public void AskToDelete(RecentRow row)
    {
        if (row.State.Step == RecentRowStep.Deleting)
        {
            return;
        }

        ResetQuestions();
        row.State = RecentRowState.Asking(deletion?.CanDeleteFromHost(row.Link) == true);
    }

    /// <summary>Returns every row that waits for an answer to normal; a running delete goes on. Returns false when no row waited.</summary>
    public bool ResetQuestions()
    {
        RecentRow[] waiting = [.. _rows.Where(row => row.State.OffersAnswer)];
        foreach (RecentRow row in waiting)
        {
            row.State = RecentRowState.Normal;
        }

        return waiting.Length > 0;
    }

    /// <summary>
    /// The yes of a row that asks or has failed. The link leaves the list only after the host, when there is one to
    /// ask, has deleted the upload. Never throws: a failure shows in the row.
    /// </summary>
    public async Task DeleteAsync(RecentRow row)
    {
        if (links is null || !row.State.OffersAnswer)
        {
            return;
        }

        bool fromHost = row.State.FromHost;
        row.State = RecentRowState.Deleting(fromHost);
        try
        {
            if (fromHost)
            {
                await deletion!.DeleteFromHostAsync(row.Link);
            }

            links.Remove(row.Link);
            _rows.Remove(row);
            OnChanged(nameof(IsEmpty));
        }
        catch (ShareUploadException error)
        {
            row.State = RecentRowState.Failed(fromHost, error.Message);
        }
        catch (Exception error)
        {
            Trace.TraceError("Deleting a recent link failed: {0}", error);
            row.State = RecentRowState.Failed(fromHost, Strings.Get("recent.delete.failed.unknown"));
        }
    }
}
