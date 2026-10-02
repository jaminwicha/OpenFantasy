using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content;

/// <summary>Exposes the retained source package only after its ordinary manifest, payload and witness validation.</summary>
public static class FarmPackageCatalog
{
    public static IReadOnlyList<PackageManifest> LoadEmbedded()
    {
        FarmContentFiles files = FarmContentFiles.Embedded();
        FarmAdmissionDefinition admission = FarmAdmissionDefinition.Load(files);
        FarmSourcePacket source = FarmSourcePacket.Load(files, admission.Root);
        admission.ValidateSources(source);
        return Array.AsReadOnly(new[] { source.Manifest });
    }
}
