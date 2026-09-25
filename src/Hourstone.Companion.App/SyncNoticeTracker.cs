using System;
using System.Collections.Generic;
using System.Linq;
using Hourstone.Companion.Core;

namespace Hourstone.Companion.App;

/// <summary>Announces a changed sync problem once, and rearms after recovery.</summary>
public sealed class SyncNoticeTracker
{
    private string previous = "";
    public bool Update(IEnumerable<SyncIssue> issues)
    {
        var current = string.Join("\n", issues.Select(issue => issue.Code + "\0" + issue.SourceId + "\0" + issue.FilePath + "\0" + issue.Message).Order(StringComparer.Ordinal));
        var announce = current.Length > 0 && current != previous;
        previous = current;
        return announce;
    }
}
