using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    /// <summary>
    /// Lends the existing fresh potion world, its recipes and the pair helpers to the durable journey.
    /// It authors nothing new: the same finite berries, recipes and receiver headroom precede genesis.
    /// </summary>
    internal sealed class DurableAccess
    {
        private readonly ConsentedPotionFixture _fixture = new(reservoirHeadroom: true);

        internal WorldSnapshot OrdinaryGenesis => _fixture.Genesis;
        internal CraftRecipeDefinition Bulk => _fixture.Bulk;
        internal CraftRecipeDefinition Repack => _fixture.Repack;
        internal RestorativeUseDefinition Self => _fixture.Self;

        internal bool IsBerries(CountedSourceLot lot) => lot.Definition.Equals(_fixture.Supply.Berries);

        internal static (WorldActionCommand Offer, WorldActionCommand Acceptance) PairFor(WorldSnapshot state,
            CareRewardJourney journey, ConsentedMaterialTerms terms) => Pair(state, journey, terms);

        internal static ConsentedMaterialTerms RawTermsFor(WorldSnapshot world, EntityId sender, EntityId recipient) =>
            RawTerms(world, sender, recipient);

        internal static WorldActionCommand CraftFor(WorldSnapshot world, CareRewardJourney journey, EntityId actor,
            CraftRecipeDefinition recipe, IngredientRequest ingredient) => CraftCommand(world, journey, actor, recipe, ingredient);
    }
}
