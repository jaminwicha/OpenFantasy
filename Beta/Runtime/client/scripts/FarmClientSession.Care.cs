using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

internal sealed partial class FarmClientSession
{
    internal void RefreshCare(FarmCareLibraryModel model, LocalSelectedPlayerObservation? observation)
    {
        ArgumentNullException.ThrowIfNull(model);
        observation ??= ReadSelection();
        FarmCarePresentation care;
        FarmActionScope scope;
        if (observation is not null)
        {
            care = FarmCarePresentation.From(observation);
            scope = FarmActionScope.From(_delivery, observation);
        }
        else
        {
            var view = World.ReadView();
            care = FarmCarePresentation.From(view);
            scope = FarmActionScope.From(_delivery, view);
        }
        model.Refresh(care, scope);
        foreach (var entry in model.Entries) _issuedOffers.Add(entry.Offer, new OfferedAction());
    }
}
