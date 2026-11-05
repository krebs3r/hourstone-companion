using Xunit;

namespace Hourstone.Companion.Core.Tests;

/// <summary>
/// Test classes that open Companion databases run one at a time. Each one calls the process-wide
/// SqliteConnection.ClearAllPools when it is disposed, which would close another class's open connection.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection { public const string Name = "Companion database"; }
