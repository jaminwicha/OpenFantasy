using System.Security.Cryptography;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Host;

/// <summary>Prepares an exact offline request, then explicitly publishes a separate verified save.</summary>
internal sealed class PartyWithdrawalOperator
{
    internal string Prepare(string source, string requestPath)
    {
        if (File.Exists(requestPath)) throw new IOException("The withdrawal request destination already exists.");
        using var authority = SqliteWorldAuthority.OpenExisting(source);
        var state = authority.InspectForHost().State;
        var request = WorldPartyWithdrawalTransition.ForHead(state,
            EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength)));
        try
        {
            // Preview checks eligibility and identity occupancy without publishing a world or advancing time.
            _ = new WorldPartyWithdrawalTransitionProcessor().Decide(state, request, authority.IsIdentityOccupiedForHost);
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidDataException("This source is not eligible for the prepared withdrawal transition: " + error.Message, error);
        }
        byte[] bytes = WorldPartyWithdrawalTransitionCodecV1.Encode(request);
        using (var output = new FileStream(requestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            output.Write(bytes);
            output.Flush(flushToDisk: true);
        }
        return JsonSerializer.Serialize(new
        {
            Contract = "party-withdrawal-preparation-v1", Source = Path.GetFullPath(source),
            Request = Path.GetFullPath(requestPath), RequestHash = request.ContentHash.Value,
            WorldId = request.WorldId.ToString(), BranchId = request.BranchId.ToString(),
            request.ExpectedRevision, request.ExpectedAdvancingSteps, HeadHash = request.ExpectedHeadHash.Value,
            TargetProfile = WorldPartyWithdrawalTransition.TargetProfileVersion
        });
    }

    internal string Apply(string source, string destination, string requestPath)
    {
        var request = LocalAuthorityFiles.ReadPartyWithdrawalTransition(requestPath);
        try
        {
            using var authority = SqliteWorldAuthority.UpgradePartyWithdrawal(source, destination, request);
            var current = authority.InspectForHost().State;
            return JsonSerializer.Serialize(new
            {
                Contract = "party-withdrawal-publication-v1", Destination = Path.GetFullPath(destination),
                RequestHash = request.ContentHash.Value, current.ProfileVersion, current.Revision,
                current.AdvancingSteps, HeadHash = current.ContentHash.Value,
                WorldId = current.WorldId.ToString(), BranchId = current.BranchId.ToString()
            });
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidDataException("The withdrawal request cannot be applied to this source: " + error.Message, error);
        }
    }
}
