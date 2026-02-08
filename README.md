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

## � Environnement de Développement Docker

L'environnement de développement utilise **Docker Compose** avec 2 conteneurs :

1. **svxlinkmanager-app** : Application .NET 9 avec SVXLink 19.09.2 installé localement
2. **postgresql** : Base de données PostgreSQL 16

### Pourquoi SVXLink dans le conteneur de l'application ?

L'application doit pouvoir **démarrer/arrêter SVXLink** via des commandes locales (`systemctl`, appels directs). SVXLink doit donc être installé dans le même conteneur que l'application.

### Configuration initiale

1. **Copier le fichier d'environnement** :
```bash
cp .env.example .env
```

2. **Modifier les variables si nécessaire** (optionnel) :
```bash
# .env
POSTGRES_DB=svxlinkmanager
POSTGRES_USER=svxlink
POSTGRES_PASSWORD=VotreMotDePasseSecurise
ASPNETCORE_ENVIRONMENT=Development

# Configuration Hardware et Daemon (Développement)
SA818__UseMock=true               # Mock SA818 (hardware non disponible en Docker)
SvxLink__UseMockDaemon=false      # Utilise le vrai daemon SVXLink du container
```

   **Variables d'environnement importantes** :
   - `SA818__UseMock=true` : Active le mock du hardware SA818 car il n'est pas accessible dans le conteneur Docker. En production, cette valeur sera `false`.
   - `SvxLink__UseMockDaemon=false` : Utilise le **vrai daemon SVXLink** installé dans le conteneur. Les commandes start/stop/status interagissent avec le daemon réel. En environnement de test unitaire, cette valeur peut être `true` pour utiliser un mock.

3. **Configuration SVXLink** :  
   Le fichier `svxlink-config/svxlink.conf` contient une configuration de base fonctionnelle.  
   Modifiez les paramètres selon votre installation (HOST, PORT, CALLSIGN, AUTH_KEY, GPIO, etc.).

### Démarrage de l'environnement

```bash
# Build et démarrage des conteneurs
docker-compose up --build -d

# Vérifier les logs
docker-compose logs -f svxlinkmanager-app

# Arrêter les conteneurs
docker-compose down

# Arrêter et supprimer les volumes (⚠️ perte des données PostgreSQL)
docker-compose down -v
```

### Accès aux services

- **Application web** : http://localhost:8080
- **PostgreSQL** : localhost:5432
  - Base : `svxlinkmanager`
  - User : `svxlink`
  - Password : (voir `.env`)

### Vérification de l'installation SVXLink

```bash
# Vérifier que SVXLink est bien installé dans le conteneur
docker exec svxlinkmanager-app which svxlink

# Afficher la version de SVXLink (doit être 19.09.2)
docker exec svxlinkmanager-app svxlink --version

# Vérifier les sons installés
docker exec svxlinkmanager-app ls -la /usr/share/svxlink/sounds/
```

### Accès à PostgreSQL

```bash
# Se connecter à PostgreSQL
docker exec -it svxlinkmanager-postgresql psql -U svxlink -d svxlinkmanager

# Vérifier les tables Marten (après premier démarrage)
docker exec -it svxlinkmanager-postgresql psql -U svxlink -d svxlinkmanager -c "\dt mt_*"
```

### Volumes Docker

- `postgres-data` : Données PostgreSQL (persistantes)
- `svxlink-spool` : Spool SVXLink (messages vocaux, etc.)
- `svxlink-logs` : Logs SVXLink
- `./svxlink-config` : Configuration SVXLink (montage local, modifiable à chaud)
- `./logs` : Logs de l'application .NET (montage local)

### Rebuild complet

```bash
# En cas de changement dans le Dockerfile ou les dépendances
docker-compose build --no-cache
docker-compose up -d
```

## �🚀 Build et Tests

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

#### Wolverine pour la médiation CQRS

**Wolverine** est le framework de médiation utilisé pour orchestrer les Commands et Queries. Il remplace MediatR (utilisé dans le projet Legacy) avec des fonctionnalités avancées :

##### Différences Legacy vs V2
| Aspect | Legacy (MediatR) | V2 (Wolverine) |
|--------|------------------|----------------|
| **Discovery** | Réflexion à l'exécution | Code-gen compilation |
| **Performance** | Bonne | Excellente (minimal overhead) |
| **Async natif** | Oui | Oui |
| **Event Sourcing** | Support limité | Intégration native Marten |
| **Testing** | Interface `IMediator` | Interface `IMessageBus` |

##### Convention : Command/Query + Handler dans le même fichier

**IMPORTANT** : Pour améliorer la lisibilité et la maintenabilité, chaque Command ou Query est définie avec son Handler **dans le même fichier**.

**Exemple - PingCommand.cs** :
```csharp
namespace SvxlinkManagerV2.Application.Features.Ping;

/// <summary>
/// Commande de test pour valider le fonctionnement CQRS.
/// Convention : La Command et son Handler sont définis dans le même fichier.
/// </summary>
public record PingCommand(string Message);

/// <summary>
/// Handler pour PingCommand. Wolverine le découvre automatiquement.
/// </summary>
public static class PingCommandHandler
{
    public static Task<string> Handle(PingCommand command)
    {
        return Task.FromResult($"Pong: {command.Message}");
    }
}
```

**Exemple - GetPingQuery.cs** :
```csharp
namespace SvxlinkManagerV2.Application.Features.Ping;

public record GetPingQuery();

public static class GetPingQueryHandler
{
    public static Task<string> Handle(GetPingQuery query)
    {
        return Task.FromResult("Ping service is alive");
    }
}
```

##### Utilisation avec IMessageBus

Dans les tests ou depuis les controllers/pages Blazor, utilisez `IMessageBus` pour invoquer les Commands/Queries :

```csharp
public class SalonController
{
    private readonly IMessageBus _messageBus;
    
    public SalonController(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }
    
    public async Task<IActionResult> Ping(string message)
    {
        // Envoyer une commande
        var result = await _messageBus.InvokeAsync<string>(
            new PingCommand(message));
        
        return Ok(result);
    }
    
    public async Task<IActionResult> GetStatus()
    {
        // Exécuter une query
        var result = await _messageBus.InvokeAsync<string>(
            new GetPingQuery());
        
        return Ok(result);
    }
}
```

##### Configuration

Wolverine est configuré dans [Program.cs](src/SvxlinkManagerV2.Presentation/Program.cs) :
```csharp
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseWolverine()  // Active Wolverine
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });
```

##### Structure des Features

```
Application/Features/
└── Ping/
    ├── PingCommand.cs           # Command + Handler ensemble
    └── GetPingQuery.cs          # Query + Handler ensemble
```

##### Tests avec Wolverine

Pour tester les handlers :
```csharp
public class PingCommandTests
{
    [Fact]
    public async Task Handle_ShouldReturnPongWithMessage()
    {
        // Arrange
        var command = new PingCommand("test");
        
        // Act
        var result = await PingCommandHandler.Handle(command);
        
        // Assert
        result.Should().Be("Pong: test");
    }
    
    [Fact]
    public async Task InvokeAsync_ShouldExecutePingCommand()
    {
        // Arrange
        var messageBus = Substitute.For<IMessageBus>();
        var command = new PingCommand("test");
        messageBus.InvokeAsync<string>(command)
            .Returns("Pong: test");
        
        // Act
        var result = await messageBus.InvokeAsync<string>(command);
        
        // Assert
        result.Should().Be("Pong: test");
    }
}
```

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
