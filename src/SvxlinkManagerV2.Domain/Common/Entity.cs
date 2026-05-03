namespace SvxlinkManagerV2.Domain.Common;

/// <summary>
/// Classe de base pour toutes les entités du domaine.
/// Une entité est identifiée par son Id et fait partie d'un Aggregate.
/// Contrairement à un Aggregate Root, une entité ne peut exister indépendamment.
/// </summary>
/// <typeparam name="TId">Type de l'identifiant de l'entité</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    /// <summary>
    /// Constructeur par défaut protégé
    /// </summary>
    protected Entity()
    {
        Id = default!;
    }

    /// <summary>
    /// Constructeur avec identifiant
    /// </summary>
    /// <param name="id">Identifiant de l'entité</param>
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// Identifiant unique de l'entité
    /// </summary>
    public TId Id { get; protected set; }

    /// <summary>
    /// Détermine si l'entité actuelle est égale à une autre entité
    /// </summary>
    /// <param name="other">Entité à comparer</param>
    /// <returns>true si les entités sont égales, false sinon</returns>
    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <summary>
    /// Détermine si l'entité actuelle est égale à un objet
    /// </summary>
    /// <param name="obj">Objet à comparer</param>
    /// <returns>true si les objets sont égaux, false sinon</returns>
    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && Equals(entity);
    }

    /// <summary>
    /// Retourne le code de hachage de l'entité
    /// </summary>
    /// <returns>Code de hachage basé sur l'identifiant</returns>
    public override int GetHashCode()
    {
        return EqualityComparer<TId>.Default.GetHashCode(Id);
    }

    /// <summary>
    /// Opérateur d'égalité
    /// </summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    /// <summary>
    /// Opérateur d'inégalité
    /// </summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !(left == right);
    }
}
