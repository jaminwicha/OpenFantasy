using System.Text.Json;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Host;

/// <summary>Inspects portable file contracts without admitting commands into a world.</summary>
internal sealed class ContractInspector
{
    // A diagnostic file budget, independent of future streaming content or save formats.
    private const int MaximumPayloadBytes = 16 * 1024 * 1024;

    public string InspectCommand(string path)
    {
        byte[] bytes = ReadBoundedFile(path, CommandCodecV1.MaximumEnvelopeByteLength);
        CommandEnvelope command = CommandCodecV1.Decode(bytes);
        return JsonSerializer.Serialize(new
        {
            Contract = "command-v1",
            Scope = "structural-validation",
            ContentHash = command.ComputeContentHash().ToString(),
            CommandId = command.CommandId.ToString(),
            PrincipalId = command.PrincipalId.ToString(),
            OriginSessionId = command.OriginSessionId.ToString(),
            WorldId = command.WorldId.ToString(),
            BranchId = command.BranchId.ToString(),
            ControlledActorId = command.ControlledActorId.ToString(),
            ExpectedRevisionCount = command.ExpectedRevisions.Count,
            Intent = new
            {
                Kind = "transfer-lot",
                LotId = command.Payload.LotId.ToString(),
                SourceCustodianId = command.Payload.SourceCustodianId.ToString(),
                DestinationCustodianId = command.Payload.DestinationCustodianId.ToString(),
                command.Payload.Quantity
            }
        });
    }

    public string InspectPackage(string manifestPath, string payloadPath)
    {
        PackageManifest manifest = PackageManifestJson.Read(
            ReadBoundedFile(manifestPath, PackageManifestJson.MaximumDocumentBytes));
        byte[] payload = ReadBoundedFile(payloadPath, MaximumPayloadBytes);
        manifest.ValidatePayload(payload);
        return JsonSerializer.Serialize(new
        {
            Contract = "package-manifest-v1",
            Scope = "manifest-and-payload-bytes",
            PackageId = manifest.Id.Value,
            Version = manifest.Version.Value,
            PayloadHash = manifest.PayloadHash.Value,
            PayloadBytes = payload.Length,
            Dependencies = manifest.Dependencies.Select(dependency => new
            {
                Id = dependency.Id.Value,
                Version = dependency.Version.Value
            })
        });
    }

    private static byte[] ReadBoundedFile(string path, int maximumBytes)
    {
        using FileStream stream = File.OpenRead(path);
        long length = stream.Length;
        if (length > maximumBytes)
        {
            throw new InvalidDataException("Inspection input exceeds its byte limit.");
        }
        byte[] bytes = new byte[checked((int)length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Inspection input changed length while reading.");
        }
        return bytes;
    }
}
