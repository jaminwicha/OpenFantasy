using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

public static class WorldReservoirTransportAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, ReservoirTransportState transport)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(transport);
        if (basis.ProfileVersion != 5 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold
            || transport.Revision != 0 || transport.Stages.Count != 0 || transport.Boundary != basis.Calendar)
        {
            throw new ArgumentException("Transport admission requires an unplayed construction world and a fresh transport journal at the same calendar.");
        }
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.reservoir-transport-admission.v1",
            basis.AdmissionVersion.ToByteArray(), transport.ContentHash.ToByteArray()));
        return new(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction, transport: transport);
    }
}
