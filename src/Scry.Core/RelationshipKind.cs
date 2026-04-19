namespace Scry.Core;

public enum RelationshipKind
{
    Unknown = 0,
    DependsOn,
    Hosts,
    Contains,
    ConnectsTo,
    Resolves,
    Issues,
    Owns,
}
