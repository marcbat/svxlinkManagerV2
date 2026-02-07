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

### Result Pattern
- Utilisation de `Validation<Error, T>` (LanguageExt)
- Les erreurs métier ne sont **pas des exceptions**
- Composition fonctionnelle avec `Bind()`, `Map()`, `Match()`

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
