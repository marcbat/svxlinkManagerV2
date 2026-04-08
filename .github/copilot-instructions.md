# Instructions Copilot pour le projet SvxLinkManager V2

## Description du Projet
Ce projet `svxlinkmanagerV2` est une refonte ("reboot") du gestionnaire pour le logiciel SVXLink. Il fournit une interface web Blazor pour piloter et configurer des nœuds radioamateurs utilisant SVXLink, incluant la gestion des connexions réflecteur, du matériel SA818, du WiFi, des commandes DTMF et des annonces vocales TTS.

## Structure et Ressources du Workspace

L'environnement de travail contient plusieurs dossiers critiques auxquels vous devez vous référer :

1.  **Projet Legacy (Référence Métier)**
    *   **Emplacement** : `../svxlinkmanager` (ou `c:\repos\svxlinkmanager`)
    *   **Usage** : Consultez ce dossier pour comprendre la logique métier, les anciennes implémentations et les fonctionnalités qui doivent être portées ou améliorées dans la V2. C'est la source de vérité pour le comportement attendu.
    *   **Documentation (Wiki)** : Consultez le Wiki du projet legacy pour des informations complémentaires : https://github.com/marcbat/svxlinkmanager/wiki

2.  **Logiciel Cible (SVXLink)**
    *   **Emplacement** : `../svxlink` (ou `c:\repos\svxlink`)
    *   **Usage** : Ce dossier contient le code source C++ de SVXLink. Il est impératif de s'y référer pour valider les fichiers de configuration, les paramètres disponibles et la structure attendue par le logiciel.
    *   **Documentation de Configuration** : Consultez en priorité les fichiers dans `src/doc/man/` (fichiers `.5` pour les manpages de configuration comme `svxlink.conf.5`) et les fichiers `.adoc` dans `src/doc/` pour comprendre les paramètres officiels.
    *   **Version Cible** : **19.09.2**. Assurez-vous que toute configuration ou interaction générée est strictement compatible avec cette version spécifique de SVXLink.

## Objectifs
*   Moderniser l'architecture par rapport à la version Legacy.
*   Assurer une compatibilité stricte avec SVXLink 19.09.2.
*   Utiliser le code Legacy comme base de spécification fonctionnelle.

## Workflow de Développement
*   **Gestion de version** : Appliquer strictement le workflow **Gitflow**.
    *   `master/main` : Production.
    *   `develop` : Intégration des features.
    *   `feature/*` : Développement des nouvelles fonctionnalités.
    *   `release/*` : Préparation des versions.
    *   `hotfix/*` : Correctifs urgents.

*   **Conventions des Commits** :
    *   **Langue** : Systématiquement en **Français**.
    *   **Format** : `préfixe: description` (ex: `feat: ajout du support EchoLink`).
    *   **Préfixes** :
        *   `feat` : Nouvelle fonctionnalité.
        *   `fix` : Correction de bug.
        *   `refactor` : Refactorisation sans changement fonctionnel.
        *   `docs` : Documentation.
        *   `test` : Tests unitaires/intégration.
        *   `chore` : Maintenance, build, Tâches diverses.

*   **Conventions des Issues** :
    *   Toute nouvelle **Issue** GitHub générée ou demandée doit suivre ce format strict :
        1.  **Titre** : Explicite.
        2.  **Description** : Contexte et détails de la tâche.
        3.  **Critères d'Acceptation (Acceptance Criteria)** : Liste des conditions obligatoires pour considérer la tâche comme terminée.
    *   **Sous-tâches** : Si une Issue nécessite des étapes intermédiaires, utilisez systématiquement des **Task Lists** Markdown (`- [ ] Tâche`) pour permettre le suivi.

## Communication et Langue
*   **Langue de Communication** : **Français obligatoire** pour toutes les interactions avec l'agent Copilot GitHub.
    *   **Pull Requests** : Tous les titres, descriptions, commentaires et reviews doivent être rédigés en français.
    *   **Issues** : Titres, descriptions, commentaires en français (déjà spécifié ci-dessus).
    *   **Code Reviews** : Les suggestions et commentaires de l'agent Copilot doivent être en français.
    *   **Documentation générée** : Tout contenu généré automatiquement doit être en français.
*   **Exception** : Le code source (noms de variables, classes, méthodes, commentaires techniques) suit les conventions .NET standard en anglais pour maintenir la cohérence avec l'écosystème.

## Architecture Technique

### Framework & Frontend
*   **Framework** : **.NET 8** (projets sources) / **.NET 9** (projets de tests).
*   **Frontend** : **Blazor Server** (Server-Side Blazor).
*   **Pattern** : **Clean Architecture** avec **Domain-Driven Design (DDD)**.

### Structure de Projet
*   **src/** : Contient les 4 projets sources.
*   **tests/** : Contient les 5 projets de tests (structure miroir de src/ + intégration).

### Structure des Couches
1.  **Presentation** (`SvxlinkManagerV2.Presentation`, net8.0) : Interface utilisateur Blazor Server.
2.  **Application** (`SvxlinkManagerV2.Application`, net8.0) : Cas d'utilisation, commandes CQRS, interfaces de repositories.
3.  **Domain** (`SvxlinkManagerV2.Domain`, net8.0) : Agrégats DDD, événements de domaine, règles métier.
4.  **Infrastructure** (`SvxlinkManagerV2.Infrastructure`, net8.0) : **Seule couche autorisée à interagir directement avec SVXLink, le matériel et l'OS** (EF Core, fichiers de config, commandes système, ports série). Elle implémente les interfaces définies par les couches supérieures.

### Pattern DDD - Agrégats
Le domaine utilise des **Agrégats** qui émettent des **Événements de Domaine** (non persistés dans la base de données, mais dispatché via MediatR en tant que Notifications) :
*   `SalonAggregate` : Représente une connexion à un réflecteur SVXLink. Propriétés clés : `Name`, `IsDefault`, `IsTemporized`, `IsActive`, `IsDeleted`, `DtmfCode`, `Configuration` (owned entity JSON).
*   `SA818Aggregate` : Configuration du radio-émetteur SA818 (Volume, Squelch, Bandwidth, CTCSS, etc.).
*   `ReflectorAggregate` : Configuration du démon SVXReflector local (`Name`, `Config` INI brut, `IsActive`, `IsDeleted`).
*   `GeneralConfigurationAggregate` : Configuration générale de l'application.
*   Les agrégats héritent de `AggregateRoot`. Les `DomainEvents` sont ignorés par EF Core (`Ignore(e => e.DomainEvents)`).

### Stack CQRS & Persistance
*   **Médiation/CQRS** : **MediatR 12.4.0** pour l'envoi et le traitement de toutes les Commandes et Queries.
    *   **Convention de Code** : Les Commands/Queries et leurs Handlers doivent **systématiquement être dans le même fichier**.
    *   Exemple : `CreateSalonCommand.cs` contient à la fois `CreateSalonCommand` (record) et `CreateSalonCommandHandler` (class).
    *   Les événements de domaine sont publiés via `IMediator.Publish()` (Notifications MediatR) pour déclencher les effets de bord.
*   **Persistance** : **EF Core 9 + SQLite**.
    *   `SvxlinkDbContext` expose : `Salons`, `SA818`, `Reflectors`, `GeneralConfigurations`.
    *   `SvxLinkConfiguration` (owned entity de `SalonAggregate`) est sérialisée en JSON (`OwnsOne(...).ToJson()`).
    *   Fichier DB en production : `/app/data/svxlinkmanager.db`.
*   **Stratégie de Données** : **Pas d'Event Sourcing**. Persistance directe des agrégats par EF Core.

### Programmation Fonctionnelle & Gestion des Erreurs
*   Utiliser le **Result Pattern** via la librairie **LanguageExt.Core 4.4.9**.
*   Type privilégié : `Validation<Error, T>` pour les opérations pouvant échouer (Application et Infrastructure).
*   Codes d'erreur sémantiques : `SALON_*`, `REFLECTOR_*`, etc. pour permettre la localisation dans l'UI.

## Fonctionnalités Implémentées

### Salons (Connexions au Réflecteur)
*   CRUD complet : création, mise à jour de la configuration, suppression douce (soft delete).
*   Activation/Désactivation : génère `svxlink.conf`, déploie le fichier WAV TTS du nom du salon, démarre/arrête le démon SVXLink.
*   Salon par défaut : activé automatiquement au démarrage (`StartupActivationHostedService`).
*   Mode autonome (Standalone) : connexion sans réflecteur.
*   Code DTMF optionnel (1-299) pour changer de salon par commande radio.
*   Plages horaires de temporisation (`IsTemporized`).

### SA818 (Radio SA818/SA868)
*   Configuration de l'émetteur-récepteur VHF/UHF SA818 via port série.
*   Paramètres : Volume, Squelch, Bandwidth, Pre-emphasis, High/Low Pass, fréquences et CTCSS.
*   Mode mock disponible via `SA818__UseMock=true` (développement sans matériel).

### Réflecteur (SVXReflector Local)
*   Gestion du démon `svxreflector` local : configuration INI brute, démarrage/arrêt.
*   Visualisation des logs en temps réel (`/reflector/logs`).
*   Suivi des nœuds connectés (`ConnectedNodesTracker`).

### WiFi
*   Gestion du WiFi via `nmcli` : scan, connexion, déconnexion, suppression de profils.
*   Mode mock disponible via `Wifi__UseMock=true` (développement sans adaptateur WiFi).

### Configuration Générale
*   Paramètres généraux de l'application (callsign, etc.).

### DTMF
*   **Pipeline** : SVXLink → Logic.tcl → `DtmfCommandTracker` (parsing des logs) → services consommateurs.
*   **Codes 1-299** : Changement de salon (`DtmfSalonSwitchService`).
*   **Codes 300-399** : Annonces (`DtmfAnnounceService`) — ex. code 300 = répète le nom du salon actif.
*   **Codes 301-398** : Informations système via `IInfoProvider` (ex. température CPU via `CpuTemperatureInfoProvider`).
*   Écriture DTMF sur PTY via `DtmfPtyWriter` (interface `IDtmfPtyWriter`).

### Annonces & TTS
*   **PicoTTS** (`pico2wave`) : génération de fichiers WAV pour les noms de salons.
*   **Logic.tcl** : déployé dans `/usr/share/svxlink/events.d/local/` par `LogicTclDeploymentService`.
*   **WAV de salon** : déployés dans `/usr/share/svxlink/sounds/fr_FR/svxlinkmanager/Name.wav`.
*   **ReflectorConnectionAnnouncementService** : annonce audio lors de connexion/déconnexion de nœuds.

### Wizard de Configuration Initiale (Setup)
*   Wizard de premier lancement guidant la configuration minimale.
*   `SetupStatusService` détecte si le setup a été complété.

### Mise à Jour de l'Application
*   Téléchargement et installation d'une nouvelle version depuis GitHub Releases.
*   Service : `GitHubReleaseUpdateService` + `ApplicationUpdateWorkflowService`.
*   Nécessite `ApplicationUpdate__GitHubToken` (variable d'environnement).

## Infrastructure — Services & Registrations DI

### Singletons (durée de vie application)
| Interface | Implémentation | Rôle |
|-----------|---------------|------|
| `ISvxLinkLogService` | `SvxLinkLogBuffer` | Buffer circulaire des logs SVXLink |
| `IConnectedNodesService` | `ConnectedNodesTracker` | Nœuds connectés au réflecteur |
| `IDtmfCommandTracker` | `DtmfCommandTracker` | Parse les DTMF depuis les logs |
| `ISvxLinkDaemonService` | `SvxLinkDaemonService` | Démarrage/arrêt du démon SVXLink |
| `IReflectorLogService` | `ReflectorLogBuffer` | Buffer circulaire des logs réflecteur |
| `IReflectorDaemonService` | `ReflectorDaemonService` | Démarrage/arrêt du démon SVXReflector |
| `ITtsService` | `PicoTtsService` | Synthèse vocale (pico2wave) |
| `IDtmfPtyWriter` | `DtmfPtyWriter` | Écriture DTMF sur PTY |
| `IInfoProvider` | `CpuTemperatureInfoProvider` | Infos système pour DTMF 301-398 |
| `IActiveSessionTracker` | `ActiveSessionTracker` | Suivi de la session active (runtime) |
| `ISetupStatusService` | `SetupStatusService` | Statut du wizard de configuration |
| `IApplicationUpdateWorkflowService` | `ApplicationUpdateWorkflowService` | Workflow de mise à jour |

### Services Hébergés (HostedService)
*   `SA818InitializerHostedService` : Initialise l'agrégat SA818 si absent.
*   `SalonSeederHostedService` : Peuple la base avec des salons par défaut si vide.
*   `StartupActivationHostedService` : Active le salon par défaut au démarrage.
*   `LogicTclInitializerHostedService` : Déploie Logic.tcl au démarrage.
*   `DtmfSalonSwitchService` : Écoute les événements DTMF (codes 1-299).
*   `DtmfAnnounceService` : Écoute les événements DTMF (codes 300-399).
*   `ReflectorConnectionAnnouncementService` : Annonce les connexions/déconnexions de nœuds.
*   `SvxLinkDiagnosticsHostedService` : Monitoring en arrière-plan du démon SVXLink.

### Scoped (par requête)
*   Repositories : `ISalonRepository`, `ISA818Repository`, `IReflectorRepository`, `IGeneralConfigurationRepository`.
*   `ISvxLinkConfigurationService` → `SvxLinkConfigurationService`
*   `ISalonAnnouncementService` → `SalonAnnouncementService`
*   `ILogicTclDeploymentService` → `LogicTclDeploymentService`
*   `IReflectorConfigurationService` → `ReflectorConfigurationService`
*   `ISA818Service` → `SA818Service` (ou `SA818MockService`)
*   `IWifiService` → `WifiService` (ou `WifiMockService`)
*   `IApplicationUpdateService` → `GitHubReleaseUpdateService` (HttpClient)

## Environnement Docker (Développement)

**3 conteneurs** définis dans `docker-compose.yml` :

1.  **`svxlinkmanager-app`** (port 8080) : Application Blazor/.NET + SVXLink 19.09.2 installé. Base SQLite dans le volume `./data/`.
2.  **`svxlink-node2`** : Second nœud SVXLink pour tester la connexion multi-nœuds.
3.  **`svxreflector`** : Serveur de conférence SVXReflector (port 5300 TCP/UDP).

Variables d'environnement notables :
*   `ConnectionStrings__SQLite` : Chaîne de connexion SQLite.
*   `SA818__UseMock=true` : Active le mock SA818 (pas de matériel requis).
*   `SvxLink__UseMockDaemon=false` : Utilise le vrai démon SVXLink.
*   `ApplicationUpdate__GitHubToken` : Token GitHub pour les mises à jour OTA.

**Important** : Le fichier `svxlink-config/svxlink.conf` **doit rester versionné** (exception `.gitignore`) car il sert de template à `SvxLinkConfigurationService`. Le supprimer casse l'activation des salons.

## Environnement de Production
*   **Hardware cible** : **Orange Pi** (architecture ARM)
*   **OS cible** : **Armbian** (distribution Linux basée sur Debian pour ARM)
*   **Déploiement** : L'application et SVXLink 19.09.2 sont installés directement sur l'Orange Pi.
*   **Architecture** : Optimisation nécessaire pour ARM.

## Stratégie de Test

### Projets de Tests (5 projets, tous net9.0)
| Projet | Contenu |
|--------|---------|
| `Domain.Tests` | Tests unitaires des agrégats DDD et règles métier |
| `Application.Tests` | Tests des handlers CQRS (commandes/queries) |
| `Infrastructure.Tests` | Tests de persistence (SQLite in-memory), services SVXLink, SA818, WiFi |
| `Presentation.Tests` | Tests des composants Blazor et services UI |
| `Integration.Tests` | Tests d'intégration bout-en-bout |

### Stack de Tests
*   **xUnit 2.9.2** : Framework de tests.
*   **FluentAssertions 8.8.0** : Assertions expressives et lisibles.
*   **NSubstitute 5.3.0** : Mocking d'interfaces (jamais de classes concrètes).
*   **LanguageExt.UnitTesting 4.4.9.1** : Extensions pour tester `Validation<Error, T>`.
*   **ini-parser 2.5.2** : Utilisé dans `Infrastructure.Tests` pour valider les fichiers de config générés.

### Principes Fondamentaux
*   **Pas d'implémentations mock concrètes** : Utiliser NSubstitute pour mocker les interfaces.
*   **SQLite in-memory** pour les tests de persistance EF Core (`Infrastructure.Tests`).
*   Les tests de génération de configuration SVXLink utilisent le **filesystem réel** (répertoire temporaire).
*   Les mocks de services systèmes (`SA818MockService`, `WifiMockService`) sont des implémentations d'infrastructure pour le développement, pas des mocks de tests.
