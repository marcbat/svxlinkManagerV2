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
    *   **Versions Cibles** : **19.09.2** (legacy, protocole V2 AUTH_KEY) et **25.05** (moderne, protocole V3 X.509). L'application supporte les deux versions via un **Strategy Pattern** d'installation duale.

## Objectifs
*   Moderniser l'architecture par rapport à la version Legacy.
*   Supporter SVXLink 19.09.2 (V2) et 25.05 (V3) via une architecture multi-version.
*   Utiliser le code Legacy comme base de spécification fonctionnelle.

## Workflow de Développement
*   **Gestion de version** : Appliquer strictement le workflow **Gitflow**.
    *   `master/main` : Production.
    *   `develop` : Intégration des features.
    *   `feature/*` : Développement des nouvelles fonctionnalités.
    *   `release/*` : Préparation des versions.
    *   `hotfix/*` : Correctifs urgents.
*   **Versioning sémantique** : Géré par **GitVersion** (`GitVersion.yml`). Prochaine version : `0.1.0`. Mode `ContinuousDeployment`.

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
*   **ImplicitUsings** et **Nullable reference types** activés sur tous les projets.

### Structure de Projet
*   **src/** : Contient les 4 projets sources.
*   **tests/** : Contient les 4 projets de tests (structure miroir de src/).

### Structure des Couches
1.  **Presentation** (`SvxlinkManagerV2.Presentation`, net8.0) : Interface utilisateur Blazor Server.
2.  **Application** (`SvxlinkManagerV2.Application`, net8.0) : Cas d'utilisation, commandes CQRS, interfaces de repositories.
3.  **Domain** (`SvxlinkManagerV2.Domain`, net8.0) : Agrégats DDD, événements de domaine, règles métier.
4.  **Infrastructure** (`SvxlinkManagerV2.Infrastructure`, net8.0) : **Seule couche autorisée à interagir directement avec SVXLink, le matériel et l'OS** (EF Core, fichiers de config, commandes système, ports série). Elle implémente les interfaces définies par les couches supérieures.

### Pattern DDD - Classes de base
*   `AggregateRoot<TId>` / `AggregateRoot` (Guid par défaut) : Collection `DomainEvents`, méthodes `AddDomainEvent()`, `ClearDomainEvents()`.
*   `Entity<TId>` : Base pour les entités enfants, implémente `IEquatable`.
*   `DomainEvent` : Record abstrait immuable avec `OccurredOn` (UTC) et `EventId` (Guid).
*   `Error` : Record `(Code, Message)` avec factory methods `Validation()`, `NotFound()`, `Conflict()`.
*   `ValidationExtensions` : Extensions pour le pattern `Validation<Error, T>`.

### Pattern DDD - Agrégats

Le domaine utilise des **Agrégats** qui émettent des **Événements de Domaine** (non persistés dans la base de données, mais dispatché via MediatR en tant que Notifications) :

#### `SalonAggregate`
Représente une connexion à un réflecteur SVXLink.
*   **Propriétés** : `Name`, `IsDefault`, `IsDeleted`, `DtmfCode` (int?, 1-9999), `Configuration` (owned entity JSON).
*   **Factory** : `Create(id, name, isDefault, configuration)` → `Validation<Error, SalonAggregate>`.
*   **Méthodes** : `UpdateConfiguration()`, `SetAsDefault()`, `UnsetDefault()`, `Activate()`, `Deactivate()`, `Delete()` (soft), `UpdateDtmfCode()`.
*   **Événements actifs** : `SalonCreated`, `SalonConfigurationUpdated`, `SalonDeleted`, `SalonDtmfCodeUpdated`, `SalonSetAsDefault`, `SalonUnsetDefault`.
*   **Événements obsolètes** : `SalonActivated`, `SalonDeactivated` (marqués `[Obsolete]` — remplacés par le tracking runtime via `IActiveSessionTracker`).

#### `SvxLinkConfiguration` (Owned Entity de SalonAggregate)
Record immuable sérialisé en JSON, contient toute la configuration SVXLink d'un salon :
*   **GLOBAL** : `Logics`, `CfgDir`, `CardSampleRate` (Hz), `CardChannels`.
*   **ReflectorLogic** : `Host`, `Port`, `Callsign`, `AuthKey` (V2 uniquement), `JitterBufferDelay`, `ReflectorProtocol` (enum V2/V3), `CertEmail` (V3 uniquement).
*   **ReflectorLogic V3 (SVXLink 25.05+)** : `DefaultTg`, `MonitorTgs` (CSV), `TgSelectTimeout`, `TgSelectInhibitTimeout`, `MuteFirstTxLoc`, `MuteFirstTxRem`, `TmpMonitorTimeout`, `QsyPendingTimeout`.
*   **SimplexLogic** : `SimplexCallsign`, `Modules`, `ShortIdentInterval`, `LongIdentInterval`, `ReportCtcss`, `DefaultLang`, `RgrSoundDelay`.
*   **Radio** : `RxFrequency`, `TxFrequency` (MHz, 30-3000), `RxCtcss`, `TxCtcss` (Hz, 67.0-250.3, nullable).

#### `SA818Aggregate`
Configuration du radio-émetteur SA818. **Singleton** avec ID fixe `00000000-0000-0000-0000-000000000001`.
*   **Propriétés** : `Volume` (1-8), `Squelch` (0-8), `Bandwidth` (enum `SA818Bandwidth`), `PreEmph`, `HighPass`, `LowPass`.
*   **Événement** : `SA818ConfigurationUpdatedEvent`.

#### `ReflectorAggregate`
Configuration du démon SVXReflector local.
*   **Propriétés** : `Name`, `Config` (INI brut), `IsDeleted` (soft delete).
*   **Méthodes** : `Create()`, `UpdateConfiguration()`, `Delete()` avec Apply handlers.
*   **Événements** : `ReflectorCreated`, `ReflectorConfigurationUpdated`, `ReflectorDeleted`.

#### `GeneralConfigurationAggregate`
Configuration générale de l'application. **Singleton** avec ID fixe `00000000-0000-0000-0000-000000000003`.
*   **Propriétés** : `StartReflectorOnStartup`, `StartDefaultSalonOnStartup`, `DefaultRxFrequency` (MHz, défaut 145.550), `DefaultTxFrequency` (MHz, défaut 145.550).

#### `TestAggregate`
Agrégat placeholder utilisé pour les scénarios de tests.

*   Les agrégats héritent de `AggregateRoot`. Les `DomainEvents` sont ignorés par EF Core (`Ignore(e => e.DomainEvents)`).

### Enums du Domaine
*   `ReflectorProtocol` : `V3` (0, moderne SVXLink 25.05+, X.509) / `V2` (1, legacy SVXLink 19.09.2, AUTH_KEY).
*   `SA818Bandwidth` : `Narrow12_5kHz` (0, NFM) / `Wide25kHz` (1, Wide FM, défaut VHF/UHF).

### Utilitaires du Domaine
*   `CtcssMapper` : Conversion bidirectionnelle code SA818 ↔ fréquence Hz CTCSS (52 tons standard, 67.0-250.3 Hz).

### Stack CQRS & Persistance
*   **Médiation/CQRS** : **MediatR 12.4.0** pour l'envoi et le traitement de toutes les Commandes et Queries.
    *   **Convention de Code** : Les Commands/Queries et leurs Handlers doivent **systématiquement être dans le même fichier**.
    *   Exemple : `CreateSalonCommand.cs` contient à la fois `CreateSalonCommand` (record) et `CreateSalonCommandHandler` (class).
    *   Les événements de domaine sont publiés via `IMediator.Publish()` (Notifications MediatR) pour déclencher les effets de bord.
*   **Persistance** : **EF Core 9.0.4 + SQLite**.
    *   `SvxlinkDbContext` expose : `Salons`, `SA818`, `Reflectors`, `GeneralConfigurations`.
    *   `SvxLinkConfiguration` (owned entity de `SalonAggregate`) est sérialisée en JSON (`OwnsOne(...).ToJson()`).
    *   Fichier DB en production : `/app/data/svxlinkmanager.db`.
*   **Stratégie de Données** : **Pas d'Event Sourcing**. Persistance directe des agrégats par EF Core.

### Programmation Fonctionnelle & Gestion des Erreurs
*   Utiliser le **Result Pattern** via la librairie **LanguageExt.Core 4.4.9**.
*   Type privilégié : `Validation<Error, T>` pour les opérations pouvant échouer (Application et Infrastructure).
*   Codes d'erreur sémantiques : `SALON_*`, `REFLECTOR_*`, `DTMF_*`, `SA818_*`, etc. pour permettre la localisation dans l'UI.

### Strategy Pattern — Multi-Version SVXLink
L'application supporte une **installation duale** de SVXLink (19.09.2 et 25.05) via un Strategy Pattern :
*   **`ISvxLinkVersionStrategy`** : Interface définissant les chemins et variables d'environnement spécifiques à une version (`BinaryPath`, `LibraryPath`, `ConfigDirectory`, `SoundsDirectory`, `EventsDirectory`, `EnvironmentVariables`, `Protocol`).
*   **`SvxLinkLegacyStrategy`** : SVXLink 19.09.2, préfixe `/opt/svxlink-legacy`, protocole V2.
*   **`SvxLinkModernStrategy`** : SVXLink 25.05, préfixe `/opt/svxlink-modern`, protocole V3.
*   **`ISvxLinkStrategyResolver`** : Résout la stratégie appropriée selon le `ReflectorProtocol` du salon.
*   Les deux versions sont compilées dans le Dockerfile et installées en parallèle.

## Fonctionnalités Implémentées

### Salons (Connexions au Réflecteur)
*   CRUD complet : création, mise à jour de la configuration, suppression douce (soft delete).
*   Activation/Désactivation : génère `svxlink.conf` (via la stratégie de version appropriée), déploie le fichier WAV TTS du nom du salon, démarre/arrête le démon SVXLink.
*   Salon par défaut : activé automatiquement au démarrage (`StartupActivationHostedService`).
*   Mode autonome (Standalone) : connexion sans réflecteur.
*   Code DTMF optionnel (1-9999) pour changer de salon par commande radio.
*   Support protocole V2 (AUTH_KEY) et V3 (X.509 certificates, talk groups).

### SA818 (Radio SA818/SA868)
*   Configuration de l'émetteur-récepteur VHF/UHF SA818 via port série (AT commands).
*   Paramètres : Volume (1-8), Squelch (0-8), Bandwidth (12.5/25 kHz), Pre-emphasis, High/Low Pass.
*   Mode mock disponible via `SA818__UseMock=true` (développement sans matériel).

### Réflecteur (SVXReflector Local)
*   Gestion du démon `svxreflector` local : configuration INI brute, démarrage/arrêt.
*   Visualisation des logs en temps réel (`/reflector/logs`).
*   Suivi des nœuds connectés (`ConnectedNodesTracker`).
*   Seeding automatique d'un réflecteur local par défaut (`ReflectorSeederHostedService`).

### WiFi
*   Gestion du WiFi via `nmcli` (NetworkManager) : scan, connexion, déconnexion, suppression de profils.
*   Composants UI dédiés : `WifiNetworkCard`, `WifiPasswordDialog`, `SignalStrengthBars`.
*   Mode mock disponible via `Wifi__UseMock=true` (développement sans adaptateur WiFi).

### Configuration Générale
*   Paramètres : `StartReflectorOnStartup`, `StartDefaultSalonOnStartup`, `DefaultRxFrequency`, `DefaultTxFrequency`.

### DTMF
*   **Pipeline** : SVXLink → Logic.tcl → `DtmfCommandTracker` (parsing des logs) → services consommateurs.
*   **Codes 1-9999** : Changement de salon (`DtmfSalonSwitchService`).
*   **Codes 300-399** : Annonces (`DtmfAnnounceService`) — ex. code 300 = répète le nom du salon actif.
*   **Codes 301-398** : Informations système via `IInfoProvider` (ex. température CPU via `CpuTemperatureInfoProvider`).
*   Écriture DTMF sur PTY via `DtmfPtyWriter` (interface `IDtmfPtyWriter`).

### Annonces & TTS
*   **PicoTTS** (`pico2wave`) : génération de fichiers WAV pour les noms de salons.
*   **Logic.tcl** : déployé dans les répertoires events des deux versions SVXLink par `LogicTclDeploymentService`.
*   **WAV de salon** : déployés dans les répertoires sounds des versions SVXLink.
*   **ReflectorConnectionAnnouncementService** : annonce audio lors de connexion/déconnexion de nœuds.

### Wizard de Configuration Initiale (Setup)
*   Wizard en 4 étapes : `Step1Callsign` → `Step2Wifi` → `Step3Frequencies` → `Step4Summary`.
*   `SetupStatusService` détecte si le setup a été complété.
*   `SetupWizardState` (Scoped) maintient l'état du wizard dans la session.

### Mise à Jour de l'Application
*   Téléchargement et installation d'une nouvelle version depuis GitHub Releases.
*   Service : `GitHubReleaseUpdateService` + `ApplicationUpdateWorkflowService`.
*   Nécessite `ApplicationUpdate__GitHubToken` (variable d'environnement).
*   Script d'installation : `deploy/linux/install-update.sh`.

### Ping / Validation
*   `PingCommand` / `GetPingQuery` : Vérification de connectivité.
*   `ValidateInputCommand` : Validation générique d'entrées utilisateur.

## Application — Features CQRS

### Salons (12 opérations)
| Opération | Type | Fichier |
|-----------|------|---------|
| `CreateSalon` | Command | `Features/Salons/CreateSalon/CreateSalonCommand.cs` |
| `ActivateSalon` | Command | `Features/Salons/ActivateSalon/` |
| `ActivateStandaloneMode` | Command | `Features/Salons/ActivateStandaloneMode/` |
| `DeactivateSalon` | Command | `Features/Salons/DeactivateSalon/` |
| `DeleteSalon` | Command | `Features/Salons/DeleteSalon/` |
| `UpdateSalonConfiguration` | Command | `Features/Salons/UpdateSalonConfiguration/` |
| `UpdateDtmfCode` | Command | `Features/Salons/UpdateDtmfCode/` |
| `SetSalonAsDefault` | Command | `Features/Salons/SetSalonAsDefault/` |
| `GetActiveSalon` | Query | `Features/Salons/GetActiveSalon/` |
| `GetAllSalons` | Query | `Features/Salons/GetAllSalons/` |
| `GetSalonById` | Query | `Features/Salons/GetSalonById/` |
| `GetSalonByDtmfCode` | Query | `Features/Salons/GetSalonByDtmfCode/` |

### SA818 (3 opérations)
*   `GetSA818Configuration` (Query), `UpdateSA818Configuration` (Command), `SA818ConfigurationDto` (DTO).

### Réflecteurs (7 opérations)
*   `CreateReflector`, `GetAllReflectors`, `GetReflectorById`, `ActivateReflector`, `DeactivateReflector`, `UpdateReflectorConfiguration`, `DeleteReflector`.

### WiFi (5 opérations)
*   `ActivateWifiCommand`, `ConnectToWifiCommand`, `DeactivateWifiCommand`, `DeleteWifiConnectionCommand`, `GetWifiStatusQuery`.

### Configuration Générale (2 opérations)
*   `CreateOrUpdate` (Command), `Get` (Query).

### Setup, ApplicationUpdate, Ping, Validation
*   `CompleteSetupCommand` + `SetupData`.
*   `DownloadApplicationUpdate`, `GetApplicationUpdateStatus`, `GetApplicationUpdateWorkflowStatus`, `RequestApplicationUpdateInstallation` + DTOs.
*   `PingCommand`, `GetPingQuery`.
*   `ValidateInputCommand`.

## Application — Interfaces (25)

### Repositories (4)
`ISalonRepository`, `ISA818Repository`, `IReflectorRepository`, `IGeneralConfigurationRepository`.

### Services SVXLink (13)
| Interface | Rôle |
|-----------|------|
| `ISvxLinkConfigurationService` | Génération du fichier `svxlink.conf` |
| `ISvxLinkDaemonService` | Démarrage/arrêt du démon SVXLink |
| `ISvxLinkLogService` | Buffer et récupération des logs |
| `IDtmfCommandTracker` | Parsing des événements DTMF depuis les logs |
| `IDtmfPtyWriter` | Sortie DTMF vers PTY |
| `ISalonAnnouncementService` | Génération des annonces TTS |
| `ILogicTclDeploymentService` | Déploiement du script Logic.tcl |
| `IConnectedNodesService` | Tracking des nœuds connectés |
| `IInfoProvider` | Infos système pour DTMF 301-398 |
| `ISvxLinkVersionStrategy` | Stratégie spécifique à une version SVXLink |
| `ISvxLinkStrategyResolver` | Résolution de la stratégie par protocole |
| `IReflectorConfigurationService` | Génération config réflecteur |
| `IReflectorDaemonService` | Démarrage/arrêt du démon SVXReflector |
| `IReflectorLogService` | Buffer des logs réflecteur |

### Hardware / Réseau (4)
`ISA818Service`, `IWifiService`, `IApplicationUpdateService`, `IApplicationUpdateWorkflowService`.

### Utilitaires (3)
`IActiveSessionTracker`, `ISetupStatusService`, `ITtsService`.

### Modèles Application (2)
`ConnectedNodeInfo`, `SvxLinkLogEntry`.

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
| `ISvxLinkVersionStrategy` | `SvxLinkLegacyStrategy` | Stratégie SVXLink 19.09.2 (V2) |
| `ISvxLinkVersionStrategy` | `SvxLinkModernStrategy` | Stratégie SVXLink 25.05 (V3) |
| `ISvxLinkStrategyResolver` | `SvxLinkStrategyResolver` | Résolution de la stratégie par protocole |
| `ToastService` | `ToastService` | Notifications toast UI |

### Services Hébergés (HostedService)
*   `SA818InitializerHostedService` : Initialise l'agrégat SA818 si absent.
*   `SalonSeederHostedService` : Peuple la base avec des salons par défaut si vide.
*   `ReflectorSeederHostedService` : Peuple la base avec un réflecteur local par défaut (ID fixe, config V3 X.509).
*   `StartupActivationHostedService` : Active le salon par défaut au démarrage.
*   `LogicTclInitializerHostedService` : Déploie Logic.tcl au démarrage.
*   `DtmfSalonSwitchService` : Écoute les événements DTMF (codes salon).
*   `DtmfAnnounceService` : Écoute les événements DTMF (codes 300-399).
*   `ReflectorConnectionAnnouncementService` : Annonce les connexions/déconnexions de nœuds.
*   `SvxLinkDiagnosticsHostedService` : Monitoring en arrière-plan du démon SVXLink.

### Scoped (par requête)
*   Repositories : `ISalonRepository`, `ISA818Repository`, `IReflectorRepository`, `IGeneralConfigurationRepository`.
*   `ISvxLinkConfigurationService` → `SvxLinkConfigurationService`
*   `ISalonAnnouncementService` → `SalonAnnouncementService`
*   `ILogicTclDeploymentService` → `LogicTclDeploymentService`
*   `IReflectorConfigurationService` → `ReflectorConfigurationService`
*   `ISA818Service` → `SA818Service` (ou `SA818MockService` si `SA818:UseMock=true`)
*   `IWifiService` → `WifiService` (ou `WifiMockService` si `Wifi:UseMock=true`)
*   `IApplicationUpdateService` → `GitHubReleaseUpdateService` (HttpClient)
*   `SetupWizardState` (état du wizard par session)

## Présentation — Pages & Composants Blazor

### Routes des Pages
| Page | Route | Description |
|------|-------|-------------|
| Dashboard | `/` | Tableau de bord principal |
| Salons (liste) | `/salons` | Liste des salons |
| Salons (création) | `/salons/nouveau` | Formulaire de création |
| Salons (édition) | `/salons/{Id:guid}/editer` | Édition d'un salon |
| SA818 | `/sa818` | Configuration radio SA818 |
| Réflecteur | `/reflector` | Gestion du réflecteur |
| Réflecteur (logs) | `/reflector/logs` | Logs en temps réel |
| Logs SVXLink | `/logs` | Visualiseur de logs |
| WiFi | `/wifi` | Gestion réseau WiFi |
| Paramètres | `/settings` | Configuration générale |
| Aide | `/aide` | Page d'aide |
| Setup Step 1 | `/setup` | Wizard — Callsign |
| Setup Step 2 | `/setup/wifi` | Wizard — WiFi |
| Setup Step 3 | `/setup/frequencies` | Wizard — Fréquences |
| Setup Step 4 | `/setup/summary` | Wizard — Récapitulatif |

### Composants WiFi
*   `WifiNetworkCard.razor` : Carte réseau WiFi.
*   `WifiPasswordDialog.razor` : Dialogue de saisie mot de passe.
*   `SignalStrengthBars.razor` : Indicateur de force du signal.

### Layouts
*   `MainLayout.razor` : Layout principal de l'application.
*   `SetupLayout.razor` : Layout dédié au wizard.
*   `ToastContainer.razor` : Conteneur de notifications toast.

### Services UI (Presentation)
*   `ToastService` / `ToastModel` : Notifications toast (Singleton).
*   `SetupWizardState` : État du wizard (Scoped).
*   `ValidationHelper` : Validation des entrées utilisateur.
*   `CtcssDropdownService` : Options pour les dropdowns CTCSS.
*   `SA818LabelsService` : Génération des labels SA818.

## Infrastructure — Organisation des Dossiers
*   `Persistence/` : DbContext, Repositories, Migrations, Hosted Services d'initialisation, SetupStatusService.
*   `SvxLink/` : Services SVXLink (configuration, daemon, logs, DTMF, TTS, annonces, connected nodes).
*   `SvxLink/Strategies/` : Stratégies de version (Legacy/Modern).
*   `SvxLink/InfoProviders/` : Fournisseurs d'informations système (température CPU, etc.).
*   `SvxLink/Resources/Logic.tcl` : Script TCL embarqué (EmbeddedResource).
*   `Hardware/` : SA818 (service réel + mock), CtcssMapper.
*   `Network/` : WiFi (service réel + mock), Mise à jour OTA (GitHub Releases).
*   `Reflector/` : Daemon, logs, configuration du SVXReflector.
*   `Runtime/` : ActiveSessionTracker.
*   `Common/` : Utilitaire IniFile (parsing/génération INI).

## Environnement Docker (Développement)

### Architecture Multi-Stage du Dockerfile
Le `Dockerfile` principal utilise un **build multi-stage** :
1.  **`dotnet-builder`** : SDK .NET 8.0, build Release avec version injectable (`APP_VERSION`).
2.  **`svxlink-legacy-builder`** : Compile SVXLink **19.09.2** → `/opt/svxlink-legacy`.
3.  **`svxlink-modern-builder`** : Compile SVXLink **25.05** → `/opt/svxlink-modern`.
4.  **`final`** : Image runtime ASP.NET 8.0 avec les deux installations SVXLink côte à côte.

### 3 conteneurs définis dans `docker-compose.yml`

1.  **`svxlinkmanager-app`** (port 8080) : Application Blazor/.NET + les deux versions de SVXLink installées. Base SQLite dans le volume `./data/`.
2.  **`svxlink-node2`** : Second nœud SVXLink pour tester la connexion multi-nœuds.
3.  **`svxreflector`** : Serveur de conférence SVXReflector.

### Volumes Docker
*   `./svxlink-config:/etc/svxlink` : Configuration SVXLink (template).
*   `./data:/app/data` : Base de données SQLite.
*   `./logs:/app/logs` : Logs application.
*   `svxlink-spool` : Spool SVXLink.
*   `svxlink-logs` : Logs SVXLink.
*   `svxlink-pki-app` : Certificats PKI (protocole V3) pour l'application.
*   `svxlink-pki-reflector` : Certificats PKI (protocole V3) pour le réflecteur.

### Réseau
*   `svxlink-network` (bridge) : Réseau interne entre les 3 conteneurs.

### Variables d'environnement notables
*   `ConnectionStrings__SQLite` : Chaîne de connexion SQLite.
*   `SA818__UseMock=true` : Active le mock SA818 (pas de matériel requis).
*   `SvxLink__UseMockDaemon=false` : Utilise le vrai démon SVXLink.
*   `ApplicationUpdate__GitHubToken` : Token GitHub pour les mises à jour OTA.

**Important** : Le fichier `svxlink-config/svxlink.conf` **doit rester versionné** (exception `.gitignore`) car il sert de template à `SvxLinkConfigurationService`. Le supprimer casse l'activation des salons.

## Environnement de Production
*   **Hardware cible** : **Orange Pi** (architecture ARM).
*   **OS cible** : **Armbian** (distribution Linux basée sur Debian pour ARM).
*   **Déploiement** : Paquet `.deb` généré par `build-deb.ps1`.
*   **Service systemd** : `svxlinkmanagerv2.service` (dans `deploy/systemd/`).
*   **Scripts de déploiement** : `deploy/debian/` (postinst, prerm, postrm), `deploy/linux/` (setup-svxlink.sh, install-update.sh).

## Configuration de l'Application (appsettings)

### appsettings.json (Production)
*   `ConnectionStrings.SQLite` : `/data/svxlinkmanager.db`.
*   `SA818.*` : Port série (`ttyS2`, 9600 baud), timeouts.
*   `SvxLink.ConfigPath` : `/etc/svxlink/svxlink.conf`.
*   `Wifi.UseMock` : `false`.
*   `ApplicationUpdate.*` : GitHub token, répertoire staging, script d'installation.

### appsettings.Development.json
*   SQLite : `svxlinkmanager-dev.db` (fichier local).
*   Mocks activés : SA818, WiFi, SVXLink daemon.
*   Channel : `Prerelease`.
*   `DetailedErrors` : `true`.
*   Logging Infrastructure : `Debug`.

## Stratégie de Test

### Projets de Tests (4 projets, tous net9.0)
| Projet | Contenu |
|--------|---------|
| `Domain.Tests` | Tests unitaires des agrégats DDD et règles métier |
| `Application.Tests` | Tests des handlers CQRS (commandes/queries) |
| `Infrastructure.Tests` | Tests de persistence (SQLite in-memory), services SVXLink, SA818, WiFi |
| `Presentation.Tests` | Tests des composants Blazor et services UI |

### Stack de Tests
*   **xUnit 2.9.2** : Framework de tests.
*   **FluentAssertions 8.8.0** : Assertions expressives et lisibles.
*   **NSubstitute 5.3.0** : Mocking d'interfaces (jamais de classes concrètes).
*   **LanguageExt.UnitTesting 4.4.9.1** : Extensions pour tester `Validation<Error, T>` (`ShouldBeSuccess()`, `ShouldBeFail()`).
*   **ini-parser 2.5.2** : Utilisé dans `Infrastructure.Tests` pour valider les fichiers de config générés.

### Principes Fondamentaux
*   **Pas d'implémentations mock concrètes** : Utiliser NSubstitute pour mocker les interfaces.
*   **SQLite in-memory** pour les tests de persistance EF Core (`Infrastructure.Tests`).
*   Les tests de génération de configuration SVXLink utilisent le **filesystem réel** (répertoire temporaire).
*   Les mocks de services systèmes (`SA818MockService`, `WifiMockService`) sont des implémentations d'infrastructure pour le développement, pas des mocks de tests.
