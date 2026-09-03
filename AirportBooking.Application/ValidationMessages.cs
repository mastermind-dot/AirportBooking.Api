namespace AirportBooking.Application;

/// <summary>
/// Marker type for the validation message resources.
///
/// It has no members: IStringLocalizer&lt;T&gt; uses the type only to locate the
/// .resx files. It deliberately sits in the project's root namespace rather
/// than in Resources — the localizer builds the resource name as
/// RootNamespace + ResourcesPath + the type name relative to the root, so a
/// type inside .Resources would make it look for Resources.Resources.
/// </summary>
public sealed class ValidationMessages
{
}
