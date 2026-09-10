namespace OpenFantasy.Hosting.Worlds;

/// <summary>Internal interruption seams for the bounded copy-and-publish operation.</summary>
internal enum WorldProductionUpgradeBoundary
{
    AfterBackup,
    AfterSchemaChange,
    BeforeCommit,
    AfterCommit,
    BeforePublish,
    AfterPublish
}
