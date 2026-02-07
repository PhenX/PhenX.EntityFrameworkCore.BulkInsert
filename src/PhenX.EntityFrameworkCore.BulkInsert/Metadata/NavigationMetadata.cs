using Microsoft.EntityFrameworkCore.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

/// <summary>
/// Metadata for a navigation property in an entity type.
/// </summary>
internal sealed class NavigationMetadata
{
    public NavigationMetadata(INavigationBase navigation)
    {
        Navigation = navigation;
        PropertyName = navigation.Name;
        TargetType = navigation.TargetEntityType.ClrType;
        IsCollection = navigation.IsCollection;

        if (navigation is ISkipNavigation skipNavigation)
        {
            IsManyToMany = true;
            JoinEntityType = skipNavigation.JoinEntityType;
            ForeignKey = skipNavigation.ForeignKey;
            InverseForeignKey = skipNavigation.Inverse?.ForeignKey;
        }
        else if (navigation is INavigation regularNavigation)
        {
            IsManyToMany = false;
            ForeignKey = regularNavigation.ForeignKey;
            IsDependentToPrincipal = regularNavigation.IsOnDependent;
        }
    }

    /// <summary>
    /// The underlying EF Core navigation.
    /// </summary>
    public INavigationBase Navigation { get; }

    /// <summary>
    /// The name of the navigation property.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// The CLR type of the related entity.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// True if this is a collection navigation (one-to-many, many-to-many).
    /// </summary>
    public bool IsCollection { get; }

    /// <summary>
    /// True if this is a many-to-many navigation.
    /// </summary>
    public bool IsManyToMany { get; }

    /// <summary>
    /// For many-to-many, the join entity type.
    /// </summary>
    public IEntityType? JoinEntityType { get; }

    /// <summary>
    /// The foreign key associated with this navigation.
    /// </summary>
    public IForeignKey? ForeignKey { get; }

    /// <summary>
    /// For many-to-many, the inverse foreign key.
    /// </summary>
    public IForeignKey? InverseForeignKey { get; }

    /// <summary>
    /// True if this navigation goes from dependent to principal (the entity owns the FK).
    /// </summary>
    public bool IsDependentToPrincipal { get; }

    /// <summary>
    /// Gets the FK property names on the source entity (for dependent-to-principal navigations).
    /// </summary>
    public IReadOnlyList<string> GetForeignKeyPropertyNames()
    {
        if (ForeignKey == null)
        {
            return [];
        }

        return ForeignKey.Properties.Select(p => p.Name).ToList();
    }

    /// <summary>
    /// Gets the principal key property names.
    /// </summary>
    public IReadOnlyList<string> GetPrincipalKeyPropertyNames()
    {
        if (ForeignKey == null)
        {
            return [];
        }

        return ForeignKey.PrincipalKey.Properties.Select(p => p.Name).ToList();
    }

    public override string ToString()
    {
        var relationshipType = IsManyToMany ? "ManyToMany" : (IsCollection ? "OneToMany" : "OneToOne");
        return $"{PropertyName} -> {TargetType.Name} ({relationshipType})";
    }
}
