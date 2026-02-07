# SvxLink Manager V2

Gestionnaire pour le logiciel SVXLink (version 19.09.2) - Refonte complète avec Clean Architecture et Event Sourcing.

## 🏗️ Architecture

Ce projet suit les principes de **Clean Architecture** avec une séparation stricte en 4 couches :

### Couches du projet

```
svxlinkmanagerV2/
├── src/
│   ├── SvxlinkManagerV2.Domain/          # Cœur métier (Entités, Aggregates, Règles)
│   ├── SvxlinkManagerV2.Application/     # Use Cases, CQRS (Commands/Queries)
│   ├── SvxlinkManagerV2.Infrastructure/  # Repositories, intégration SVXLink, PostgreSQL
│   └── SvxlinkManagerV2.Presentation/    # Interface Blazor Server
└── tests/
    ├── SvxlinkManagerV2.Domain.Tests/
    ├── SvxlinkManagerV2.Application.Tests/
    ├── SvxlinkManagerV2.Infrastructure.Tests/
    └── SvxlinkManagerV2.Presentation.Tests/
```

### 1. Domain (Cœur métier)
- **Aucune dépendance externe**
- Contient :
  - `Aggregates` : Aggregate Roots (Salon, Sound, RadioProfil)
  - `Entities` : Entités du domaine
  - `Events` : Événements métier (Event Sourcing)
  - `Common` : Classes de base (AggregateRoot, Entity, Error, DomainEvent)
- Règles métier pures, indépendantes de toute infrastructure

### 2. Application (Orchestration métier)
- **Dépend uniquement de Domain**
- Contient :
  - `Features` : Commands/Queries (CQRS avec Wolverine)
  - `Interfaces` : Contrats pour l'Infrastructure
- Pattern CQRS : séparation lecture (Queries) / écriture (Commands)
- Gestion fonctionnelle des erreurs avec `LanguageExt` (Result Pattern)

### 3. Infrastructure (Implémentations techniques)
- **Dépend de Application (et Domain par transitivité)**
- **SEULE couche autorisée à interagir directement avec SVXLink**
- Contient :
  - `Persistence` : Repositories, Marten, Event Sourcing
  - `Services` : Services d'intégration SVXLink (fichiers config, commandes système, logs)
- Implémente les interfaces définies par Application

### 4. Presentation (Interface utilisateur)
- **Dépend de Infrastructure (et transitoirement Application et Domain)**
- Application Blazor Server
- Pages, composants, services UI

## 🔧 Stack Technique

- **Framework** : .NET 9.0
- **Frontend** : Blazor Server
- **Médiation/CQRS** : Wolverine
- **Persistance** : Marten (Event Sourcing sur PostgreSQL)
- **Gestion des erreurs** : LanguageExt (Result Pattern avec `Validation<Error, T>`)
- **Tests** :
  - Xunit
  - FluentAssertions
  - NSubstitute
  - LanguageExt.UnitTesting

## 📋 Prérequis

- .NET 9.0 SDK
- PostgreSQL
- Docker & Docker Compose (pour développement)
- SVXLink 19.09.2

## 🚀 Build et Tests

```bash
# Compilation
dotnet build

# Exécution des tests
dotnet test

# Restaurer les packages
dotnet restore
```

## 🎯 Principes Architecturaux

### Event Sourcing
- Tout changement d'état est représenté par un **événement immutable**
- Les Aggregates sont reconstruits en rejouant les événements depuis le stream Marten
- Chaque Aggregate a son propre stream : `{aggregate-type}-{guid}`

### CQRS (Command Query Responsibility Segregation)
- **Commands** : Modifient l'état (créent des événements)
- **Queries** : Lisent l'état (depuis les projections Marten)
- Handlers gérés par Wolverine

### Result Pattern avec LanguageExt

#### Principe
- Les **erreurs métier ne sont pas des exceptions** - elles représentent des échecs prévisibles
- Retourner `Validation<Error, T>` pour représenter un Succès ou un Échec
- Composition fonctionnelle avec `Bind()`, `Map()`, `Match()`
- Accumulation automatique des erreurs multiples

#### Quand utiliser ?
✅ **Utiliser `Validation` pour** :
- Validations métier (règles business)
- Erreurs prévisibles (input invalide, règle métier non respectée)
- Flow métier avec plusieurs étapes de validation

❌ **Ne pas utiliser pour** :
- Erreurs techniques (I/O, réseau, base de données)
- Bugs logiques (NullReferenceException, IndexOutOfRangeException)
- Situations inattendues qui doivent interrompre l'exécution

#### Exemple simple

```csharp
using LanguageExt;
using SvxlinkManagerV2.Domain.Common;
using static LanguageExt.Prelude;

public static Validation<Error, string> ValidateCallsign(string callsign)
{
    return callsign
        .ValidateNotEmpty("EMPTY_CALLSIGN", "Callsign cannot be empty")
        .Bind(value => value.ValidateThat(
            v => v.Length >= 3,
            "CALLSIGN_TOO_SHORT",
            "Callsign must be at least 3 characters"))
        .Map(value => value.ToUpper());
}
```

#### Exemple avec composition

```csharp
public static Validation<Error, Salon> CreateSalon(
    string name, 
    string host, 
    int port)
{
    // Validation de chaque paramètre
    var nameValidation = name.ValidateNotEmpty(
        "EMPTY_NAME", 
        "Salon name cannot be empty");
    
    var hostValidation = host.ValidateNotEmpty(
        "EMPTY_HOST", 
        "Host cannot be empty");
    
    var portValidation = port.ValidateThat(
        p => p > 0 && p <= 65535,
        "INVALID_PORT",
        "Port must be between 1 and 65535");
    
    // Combinaison : si toutes réussissent, crée le Salon
    // Si au moins une échoue, retourne TOUTES les erreurs
    return (nameValidation, hostValidation, portValidation)
        .Apply((n, h, p) => new Salon(n, h, p));
}
```

#### Utilisation dans les Handlers

```csharp
public static class CreateSalonCommandHandler
{
    public static Validation<Error, Guid> Handle(CreateSalonCommand cmd)
    {
        // Validation + création
        var validation = SalonAggregate.Create(
            cmd.Name, 
            cmd.Host, 
            cmd.Port);
        
        return validation.Match(
            Succ: salon => 
            {
                // Sauvegarder en base
                _repository.Save(salon);
                return salon.Id.ToSuccess();
            },
            Fail: errors => errors.ToFailure<Guid>()
        );
    }
}
```

#### Extensions disponibles

Voir `ValidationExtensions.cs` pour les helpers :
- `ToSuccess()` : Convertit une valeur en succès
- `ToFailure()` : Convertit une/des erreur(s) en échec
- `ValidateNotEmpty()` : Valide qu'une chaîne/Guid n'est pas vide
- `ValidateThat()` : Valide un prédicat personnalisé
- `Sequence()` : Combine plusieurs validations

### Fondations du Domain

Le Domain Layer fournit les classes de base pour implémenter le DDD (Domain-Driven Design) :

#### **AggregateRoot<TId>**
- Point d'entrée transactionnel d'un ensemble d'entités
- Possède son propre stream d'événements dans Marten (`{type}-{guid}`)
- Gère une collection d'événements du domaine non commités
- Exemple : `SalonAggregate`, `SoundAggregate`, `RadioProfilAggregate`

#### **Entity<TId>**
- Identifiée par son `Id`, fait partie d'un Aggregate
- Ne peut exister indépendamment (pas de stream propre)
- Implémentation complète de l'égalité (`Equals`, `GetHashCode`, opérateurs `==`/`!=`)
- Exemple : `RxConfiguration`, `TxConfiguration`

#### **Error (record)**
- Représente une erreur métier (pas une exception)
- Utilisé avec `Validation<Error, T>` pour le Result Pattern
- Factory methods : `Validation()`, `NotFound()`, `Conflict()`
- Format : `Code` (ex: "INVALID_CALLSIGN") + `Message` descriptif

#### **DomainEvent (abstract record)**
- Fait immutable qui s'est produit dans le passé
- Source de vérité en Event Sourcing (reconstruit l'état des Aggregates)
- Propriétés automatiques : `OccurredOn` (UTC), `EventId` (Guid unique)
- Les Aggregates appliquent les événements via méthodes `Apply()`

## 📂 Conventions

### Dépendances entre projets
```
Domain ← Application ← Infrastructure ← Presentation
```

### Organisation des Features (CQRS)
```
Application/Features/
└── EntityName/
    ├── CreateEntity/
    │   └── CreateEntityCommand.cs       # Command + Handler dans le même fichier
    ├── UpdateEntity/
    │   └── UpdateEntityCommand.cs
    └── GetEntityById/
        └── GetEntityByIdQuery.cs        # Query + Handler dans le même fichier
```

### Tests
- Structure miroir de `src/`
- Tests unitaires : Domain et Application (avec mocks)
- Tests d'intégration : Infrastructure (avec Testcontainers PostgreSQL)

## 📖 Documentation

Pour plus d'informations sur :
- La logique métier : Consultez le [projet Legacy](../svxlinkmanager)
- La configuration SVXLink : Consultez le [code source SVXLink](../svxlink/src/doc/)
- Le Wiki du projet Legacy : https://github.com/marcbat/svxlinkmanager/wiki

## 🔄 Workflow Git

Ce projet utilise **Gitflow** :
- `main` : Production
- `develop` : Intégration
- `feature/*` : Nouvelles fonctionnalités
- `release/*` : Préparation des versions
- `hotfix/*` : Correctifs urgents

### Commits
- **Langue** : Français
- **Format** : `préfixe: description`
- **Préfixes** : `feat`, `fix`, `refactor`, `docs`, `test`, `chore`

Exemple : `feat: ajouter Aggregate Salon avec Event Sourcing`

## 📄 Licence

À définir

## 👥 Contributeurs

- Marc Battaglia ([@marcbat](https://github.com/marcbat))
