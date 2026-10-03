using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

internal sealed partial class FarmClientSession
{
    /// <summary>Issues recipe offers from the same permitted observation the panel is presenting.</summary>
    internal void RefreshRecipes(FarmRecipeLibraryModel model, LocalSelectedPlayerObservation? observation)
    {
        ArgumentNullException.ThrowIfNull(model);
        observation ??= ReadSelection();
        FarmProductionPresentation production;
        FarmActionScope scope;
        if (observation is not null)
        {
            if (observation.ControlledView is { } controlled && controlled.Production?.Crafting is null
                || observation.ControlledView is null && observation.HomeView.Crafting is null)
            {
                model.Clear();
                return;
            }
            production = FarmProductionPresentation.From(observation);
            scope = FarmActionScope.From(_delivery, observation);
        }
        else
        {
            var view = World.ReadView();
            if (view.Crafting is null)
            {
                model.Clear();
                return;
            }
            production = FarmProductionPresentation.From(view);
            scope = FarmActionScope.From(_delivery, view);
        }
        model.Refresh(production, scope);
        foreach (var entry in model.Entries) _issuedOffers.Add(entry.Offer, new OfferedAction());
    }
}
