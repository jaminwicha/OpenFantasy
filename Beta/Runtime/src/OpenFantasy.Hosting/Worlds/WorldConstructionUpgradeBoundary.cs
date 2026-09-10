namespace OpenFantasy.Hosting.Worlds;

internal enum WorldConstructionUpgradeBoundary
{
    AfterBackup,
    BeforeCommit,
    AfterCommit,
    BeforePublish,
    AfterPublish
}
