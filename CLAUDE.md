# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Langue

**Toutes les interactions se font en français** : réponses, messages de commit, titres/descriptions de PR et d'issues, commentaires de review, documentation générée.

**Exception** : le code source (noms de classes, méthodes, variables) suit les conventions .NET en anglais. Les commentaires XML/inline dans le code sont en français, comme le reste du code existant.

## Commandes

```bash
dotnet build SvxlinkManagerV2.sln
```

```bash
dotnet test SvxlinkManagerV2.sln
```

Un seul projet de test :

```bash
dotnet test tests/SvxlinkManagerV2.Domain.Tests/SvxlinkManagerV2.Domain.Tests.csproj
```

Un seul test / une classe de tests (filtre xUnit) :

```bash
dotnet test tests/SvxlinkManagerV2.Application.Tests --filter "FullyQualifiedName~CreateSalonCommandTests"
```

Couverture de code :

```bash
dotnet test SvxlinkManagerV2.sln --settings coverage.runsettings
```

Tests d'intégration bout-en-bout (montent la stack Docker — voir [Tests d'intégration](#tests-dintégration)) :

```bash
SVXLINK_INTEGRATION_TESTS=1 dotnet test tests/SvxlinkManagerV2.Integration.Tests
```

Lancer l'application en local (mocks SA818/WiFi/daemon activés par `appsettings.Development.json`, SQLite dans un fichier local) :

```bash
dotnet run --project src/SvxlinkManagerV2.Presentation
```

Environnement Docker complet (app + deux nœuds SVXLink + réflecteur), app sur http://localhost:8080 :

```bash
docker compose up --build
```

Voir [la stack de test Docker](#la-stack-de-test-docker) pour savoir qui parle à qui.

Construire le paquet Debian ARM (`artifacts/deb/`) :

```bash
pwsh ./build-deb.ps1 -PackageVersion 0.1.0
```

**Note SDK** : les projets `src/` ciblent **net8.0**, les projets `tests/` ciblent **net9.0**. Le build fonctionne avec un SDK plus récent installé (9.x / 10.x).

## Workflow Git

**Gitflow strict** : `master`/`main` (production), `develop` (intégration), `feature/*`, `release/*`, `hotfix/*`.

Versioning par **GitVersion** (`GitVersion.yml`, mode `ContinuousDeployment`). La CI (`.github/workflows/build-deb.yml`) construit un `.deb` sur push vers `main`/`master`/`develop`/`release/*`/`hotfix/*`, exécute les tests, et publie une GitHub Release avec un `manifest.json` consommé par le service de mise à jour OTA de l'application.

**Commits** : `préfixe: description` en français. Préfixes : `feat`, `fix`, `refactor`, `docs`, `test`, `chore`.

**Issues** : titre explicite + description + **Critères d'Acceptation**. Les étapes intermédiaires sont des Task Lists Markdown (`- [ ] Tâche`).

## Architecture

Clean Architecture + DDD, 4 projets sources avec une structure miroir dans `tests/`. Sens des dépendances : `Presentation` → `Infrastructure` → `Application` → `Domain`.

| Couche | Rôle |
|--------|------|
| `Domain` | Agrégats DDD, events de domaine, `Error`, `CtcssMapper`. Aucune dépendance hors LanguageExt. |
| `Application` | Commands/Queries MediatR (`Features/`), interfaces (`Interfaces/`), modèles partagés (`Models/`). |
| `Infrastructure` | **Seule couche autorisée à toucher SVXLink, le matériel, l'OS et la base** : EF Core, processus, ports série, `nmcli`, fichiers de config. |
| `Presentation` | Blazor Server + composition racine de la DI. |

### Conventions structurantes

**Command/Query et Handler dans le même fichier.** `Features/Salons/CreateSalon/CreateSalonCommand.cs` contient le record `CreateSalonCommand` **et** la classe `CreateSalonCommandHandler`. Ne jamais les séparer.

**Result pattern via LanguageExt** : les opérations faillibles retournent `Validation<Error, T>`, jamais d'exceptions métier. `Error` est un record `(Code, Message)` avec les factories `Validation()`, `NotFound()`, `Conflict()`. Les codes sont sémantiques et préfixés par domaine (`SALON_*`, `REFLECTOR_*`, `DTMF_*`, `SA818_*`) pour permettre la localisation côté UI.

**La DI se configure dans [Startup.cs](src/SvxlinkManagerV2.Presentation/Startup.cs)** — le projet utilise l'ancien modèle `Startup` (pas de minimal hosting dans `Program.cs`). Tout nouveau service d'infrastructure doit y être enregistré. MediatR scanne l'assembly `Application` en s'ancrant sur le type `PingCommand`.

**Les événements de domaine ne sont PAS dispatchés.** Les agrégats les accumulent via `AddDomainEvent()`, EF Core les ignore (`Ignore(e => e.DomainEvents)`), et les repositories appellent `ClearDomainEvents()` après sauvegarde. Il n'existe aucun `INotificationHandler` ni `IMediator.Publish` dans le code. La communication inter-composants runtime passe par des **événements C# (`event Action<T>`) exposés par des singletons d'infrastructure** — voir le pipeline DTMF ci-dessous. Ne pas supposer qu'ajouter un `DomainEvent` déclenche un effet de bord.

**Persistance : migrations EF Core.** [Program.cs](src/SvxlinkManagerV2.Presentation/Program.cs) appelle `DatabaseMigrator.MigrateAsync()` au démarrage, qui applique les migrations en attente. **Toute modification du schéma impose donc une migration** — jamais la suppression du fichier SQLite, qui détruirait les salons et la configuration de l'utilisateur :

```bash
dotnet ef migrations add NomDeLaMigration --project src/SvxlinkManagerV2.Infrastructure --startup-project src/SvxlinkManagerV2.Infrastructure
```

`SvxlinkDbContextFactory` (`IDesignTimeDbContextFactory`) permet aux outils EF de travailler sur la seule couche Infrastructure, sans démarrer l'hôte Blazor et ses services hébergés.

**Adoption des bases héritées.** Les installations déployées avant les migrations ont été créées par `EnsureCreated()` et n'ont pas de table `__EFMigrationsHistory`. `DatabaseMigrator.AdoptLegacyDatabase()` inspecte le schéma réel et inscrit comme déjà appliquées les seules migrations qui y correspondent, avant de laisser `Migrate()` appliquer le reste. Trois états sont reconnus : antérieur aux salons Parrot (pas de colonne `Salons.SalonType`), v1.0.0 (colonne présente, pas d'authentification) et post-authentification (table `AspNetUsers` présente). **Ajouter un état à reconnaître impose de mettre à jour cette méthode et ses constantes d'identifiants de migration.**

`SvxLinkConfiguration` est une owned entity de `SalonAggregate` sérialisée en JSON (`OwnsOne(...).ToJson()`) : ajouter un champ de configuration SVXLink ne change pas le schéma des colonnes.

### Authentification

L'application est protégée par **ASP.NET Identity** avec un **compte unique** créé à l'étape 0 du wizard d'installation. Points à connaître avant d'ajouter une page :

- **Tout est protégé par défaut.** `_Imports.razor` porte `@attribute [Authorize]` et `App.razor` route les composants via `AuthorizeRouteView` : une nouvelle page Blazor est protégée **sans rien faire** ; l'ouvrir au public demande `@attribute [AllowAnonymous]`.
- **Le middleware ne peut pas arbitrer les routes Blazor.** Elles sont toutes servies par la même page hôte `/_Host`, qui porte donc `@attribute [AllowAnonymous]` ; l'autorisation est faite côté composant. Le hub Blazor est également anonyme, sinon la négociation du circuit boucle en `ERR_TOO_MANY_REDIRECTS`.
- **`.AllowAnonymous()` posé en convention sur `MapFallbackToPage(...)` n'a aucun effet** — la métadonnée n'atteint pas l'endpoint de fallback Razor Pages. C'est la raison de l'attribut directement sur `_Host.cshtml`. La `FallbackPolicy` globale reste en place pour les Razor Pages explicites.
- **Login, logout et auto-login sont des Razor Pages**, pas des composants : écrire un cookie d'authentification est impossible depuis un circuit Blazor Server (WebSocket, réponse déjà émise).
- **`/setup` (Step0Account) est la seule page anonyme du wizard.** Les étapes suivantes (`/setup/callsign` … `/setup/summary`) sont protégées ; la transition passe par un **token à usage unique** en mémoire (`IPendingSetupLoginService`, TTL 5 min) consommé par `/account/setup-complete`, qui ouvre la session puis redirige.
- `SvxlinkDbContext` dérive d'`IdentityDbContext<IdentityUser>` : **appeler `base.OnModelCreating(modelBuilder)`** avant toute configuration, sinon le schéma Identity n'est pas construit.

**Mise à jour d'une installation existante** : les tables `AspNet*` sont créées par la migration `AddIdentitySchema` sur les déploiements antérieurs à l'authentification — voir l'adoption des bases héritées ci-dessus.

### Pipeline DTMF (chaîne à comprendre avant d'y toucher)

```
SVXLink → Logic.tcl (émet "DTMF_CMD:<code>" dans les logs)
        → SvxLinkLogBuffer (event OnLogReceived)
        → DtmfCommandTracker (parse le préfixe, event OnDtmfCommandReceived)
        → DtmfSalonSwitchService (codes 1-9999 → change de salon)
        → DtmfAnnounceService  (codes 300-399 → annonces TTS via IInfoProvider)
        → DtmfSystemCommandService (codes 310-320 → commandes système, cf. DtmfSystemCommands)
```

`Logic.tcl` est un `EmbeddedResource` de l'Infrastructure, déployé au démarrage dans les répertoires `events.d/local` des **deux** installations SVXLink par `LogicTclDeploymentService`.

### État du daemon vs état de la liaison réflecteur

Deux notions distinctes, à ne pas confondre :

- `ISvxLinkDaemonService.IsRunningAsync()` ne dit que si le **processus** svxlink tourne ;
- `IReflectorLinkStateService` (implémenté par `ReflectorLinkStateTracker`, singleton) suit l'état de la **liaison** au réflecteur en parsant les lignes `ReflectorLogic` du flux de logs, et le publie via `OnStateChanged`.

Un daemon actif ne garantit pas une liaison : `AUTH_KEY` erronée, hôte injoignable ou certificat rejeté laissent le processus en vie sans que le nœud soit relié. Les commandes d'activation appellent `BeginConnecting()` (salon réflecteur) ou `MarkNotApplicable()` (salon perroquet, mode autonome) avant le redémarrage du daemon — en mode autonome le tracker ignore les logs, sinon des lignes résiduelles feraient apparaître une liaison en erreur. **Ajouter un motif de log reconnu impose de mettre à jour `ReflectorLinkStateTracker.Interpret` et ses tests**, en vérifiant les deux versions de SVXLink (`ReflectorLogic.cpp`).

### Supervision système

`ISystemMetricsService` (implémenté par `LinuxSystemMetricsService`, dans `Infrastructure/Monitoring`) est **l'unique lecteur** de `/proc`, `/sys` et de l'espace disque. En découlent deux consommateurs :

- les `IInfoProvider` 301, 304-307 (température, disque, uptime, charge, mémoire), qui n'en font que la mise en forme vocale française — les providers 302/303 (adresse IP, état réseau) lisent le réseau directement ;
- la query `GetSystemStatusQuery`, qui agrège tout pour la page `/systeme`.

Ajouter une métrique = une méthode sur `ISystemMetricsService`, puis un provider pour l'annonce et un champ dans `SystemStatusDto`. Chaque métrique retourne un `Validation<Error, T>` **indépendant** : une source absente sur la plateforme courante est affichée comme indisponible, jamais propagée en échec de page. Seuils d'alerte et chemins supervisés dans la section `SystemMonitoring` des appsettings.

Le feature Application s'appelle `Features/SystemStatus` (et non `System`) et `Pages/System/Index.razor` déclare un `@namespace` explicite : un namespace nommé `System` masquerait celui du framework dans tous les fichiers voisins.

### Chaîne audio de la machine

Trois briques distinctes, réunies par la page `/audio` et l'étape 5 de l'assistant d'installation :

- **`IAudioService`** (`AlsaAudioService`, ou `AudioMockService` si `Audio:UseMock`) lit et écrit
  **deux** contrôles `amixer` seulement, désignés par la section `Audio` des appsettings :
  `CaptureControl` (défaut `ADC Gain`) et `PlaybackControl` (défaut `Line Out`). Le reste du routage
  de la carte n'est jamais touché — les commutateurs de capture d'un nœud radio sont un réglage
  matériel délicat, cf. [docs/svxlink-hb9gxp-configuration-validee.md](docs/svxlink-hb9gxp-configuration-validee.md).
- **`IPttTestService`** (`GpioPttTestService`, ou `PttTestMockService`) maintient le PTT une durée
  bornée en écrivant dans `/sys/class/gpio/<PttPin>/value`. C'est **la broche que SVXLink exporte**
  (`PTT_PIN` de la section Tx1) : le test exige donc un salon actif et un daemon en marche, condition
  portée par `PttTestAvailability` côté Application. Le minuteur de relâchement vit dans le singleton,
  jamais dans le circuit Blazor — fermer l'onglet ne doit pas laisser la station en émission.
- **`IRxDistortionService`** (`RxDistortionTracker`) compte les écrêtages de l'audio entrant en
  guettant `Distortion detected` dans le flux de logs. **Le périphérique de capture ALSA est ouvert
  en exclusivité par SVXLink** dès qu'un salon tourne : l'application ne peut pas mesurer le niveau
  d'entrée elle-même, et c'est le `PEAK_METER=1` du récepteur qui joue ce rôle.

Le test PTT ne porte que la **porteuse**, et une porteuse FM non modulée est inaudible : il ne
prouve rien du niveau de sortie. C'est le rôle de `StartModulationTestCommand`, qui diffuse une
annonce vocale via `IVoiceAnnouncementService` (TTS → `/tmp/svxlink_tts.wav` → DTMF interne 399 →
`playFile` dans `Logic.tcl`). L'audio traverse alors le contrôle ALSA de restitution, et **c'est
SVXLink qui commande le PTT** : les deux tests sont donc mutuellement exclusifs, forcer le GPIO
pendant une annonce ferait couper celle-ci par le minuteur de relâchement. Ils partagent en
revanche les mêmes préconditions (`PttTestAvailability`), et l'interface s'inhibe le temps de la
lecture — régénérer le WAV pendant que SVXLink le lit tronquerait l'annonce.

`AudioConfigurationAggregate` (singleton, ID fixe `...0004`) mémorise les niveaux **avec le nom du
contrôle dont ils proviennent** : une valeur n'est pas transposable d'un contrôle à l'autre, les
plages différant (0-31 pour `Line Out`, 0-7 pour `ADC Gain`). `AudioInitializerHostedService` les
réapplique au démarrage — mais **au premier lancement il adopte les niveaux trouvés sur la carte**
au lieu d'en imposer par défaut, et fait de même si la configuration désigne d'autres contrôles que
ceux mémorisés. Écraser au démarrage les niveaux d'un nœud déjà réglé serait une régression.

Le RSSI vient du module SA818 (`ISA818Service.ReadRssiAsync`, commande `RSSI?` → `RSSI=020`), pas de
la carte son. Sa valeur est brute (0-255) et affichée telle quelle : aucune conversion en dBm n'est
faite, le datasheet du module n'en garantissant pas la formule.

### Historique d'activité (page Statistiques)

Tout ce que le nœud observe était déjà publié par les trackers d'infrastructure sous forme
d'événements C#, mais **rien n'en survivait** : le buffer de logs plafonne à 1000 lignes et les
trackers ne tiennent qu'un état courant. Deux tables SQLite comblent ce trou, alimentées par
`ActivityRecorderHostedService`, qui ne fait que convertir en écritures les événements de
`ConnectedNodesTracker`, `DtmfCommandTracker`, `ReflectorLinkStateTracker`, `RxDistortionTracker`
et `SquelchStateTracker`. **Aucune analyse de logs n'a lieu dans l'enregistreur** : elle appartient
aux trackers.

- **`SalonSessions`** — périodes passées sur un salon. Le mode autonome et le perroquet y sont des
  natures de session (`SalonKind`) à part entière : sans cela le temps passé déconnecté serait un
  trou. Ouvertes par `ActivateSalonCommand` / `ActivateStandaloneModeCommand`, qui portent désormais
  un `SalonActivationOrigin` (web, DTMF, commande système, démarrage) — c'est lui qui dit si le nœud
  est piloté depuis le navigateur ou depuis la radio.
- **`ActivityEvents`** — table en ajout seul. **Les événements de durée sont écrits à leur fin**,
  avec la durée déjà calculée : la lecture n'a jamais à appairer un début et une fin, et un arrêt
  brutal ne laisse pas d'enregistrement à moitié constitué. L'heure locale y est *figée à l'écriture*
  (`LocalHour`, `LocalDayOfWeek`) parce que SQLite ne sait pas convertir de fuseau dans une agrégation.

**`IActivityRecorder` tient les intervalles encore ouverts** que la base ignore : la liaison
réflecteur en cours et l'interruption en cours. `PendingLinkUpSince` est lu par la query — sans ce
rattrapage, un nœud lié sans interruption depuis trois jours afficherait une disponibilité nulle.

**`ActivityRecorderHostedService` doit rester enregistré avant `StartupActivationHostedService`** :
son démarrage clôt les sessions laissées ouvertes par un arrêt brutal — à l'heure du dernier signe de
vie connu, pas à celle du redémarrage — et refermerait sinon la session que l'activation automatique
vient d'ouvrir.

Côté lecture, `Features/Statistics` délègue à SQLite tous les regroupements d'événements ; seules les
sessions, qui se comptent en unités par jour, sont chargées telles quelles. La page `/statistiques`
n'introduit **aucune dépendance JavaScript** : barres et carte de chaleur sont du CSS calculé côté
serveur, via `CssValue` — le CSS n'accepte que le point décimal, et une culture à virgule produirait
des déclarations que le navigateur rejette en silence. Rétention configurable (section `Statistics`,
90 jours par défaut) appliquée par `StatisticsPurgeHostedService`.

**Le suivi du squelch local est la seule mesure d'activité radio locale possible** — le périphérique
de capture ALSA est monopolisé par SVXLink, et en mode autonome comme en perroquet il n'existe aucune
liaison réflecteur pour rapporter les passages. Il repose sur le motif `The squelch is OPEN` /
`CLOSED` : si la configuration de SVXLink ne le produit pas, les compteurs locaux restent à zéro et
l'interface le dit explicitement plutôt que d'afficher un zéro trompeur.

### Strategy Pattern — double version SVXLink

L'application pilote deux installations SVXLink en parallèle, sélectionnées d'après le `ReflectorProtocol` du salon :

| Stratégie | Version SVXLink | Préfixe | Protocole |
|-----------|-----------------|---------|-----------|
| `SvxLinkLegacyStrategy` | 19.09.2 | `/opt/svxlink-legacy` | V2 (AUTH_KEY) |
| `SvxLinkModernStrategy` | 25.05 | `/opt/svxlink-modern` | V3 (certificats X.509, talk groups) |

`ISvxLinkVersionStrategy` expose `BinaryPath`, `LibraryPath`, `ConfigDirectory`, `SoundsDirectory`, `EventsDirectory`, `EnvironmentVariables`, `Protocol`. `ISvxLinkStrategyResolver` fait la résolution. Les deux versions sont compilées dans des stages distincts du `Dockerfile` et installées côte à côte. **Tout chemin vers un binaire, un son ou un fichier de config SVXLink doit passer par la stratégie**, jamais être codé en dur.

### Agrégats

- **`SalonAggregate`** — une connexion réflecteur. `Name`, `IsDefault`, `IsDeleted` (soft delete), `DtmfCode` (1-9999), `Configuration` (owned). Les events `SalonActivated`/`SalonDeactivated` sont `[Obsolete]` : l'état actif est suivi au runtime par `IActiveSessionTracker` (singleton), pas en base.
- **`SA818Aggregate`** — singleton, ID fixe `00000000-0000-0000-0000-000000000001`. `Volume` (1-8), `Squelch` (0-8), `Bandwidth`, `PreEmph`, `HighPass`, `LowPass`.
- **`GeneralConfigurationAggregate`** — singleton, ID fixe `00000000-0000-0000-0000-000000000003`.
- **`AudioConfigurationAggregate`** — singleton, ID fixe `00000000-0000-0000-0000-000000000004`. Niveaux ALSA mémorisés, avec le nom du contrôle auquel ils s'appliquent.
- **`ReflectorAggregate`** — config INI brute du démon `svxreflector` local.
- **`SalonSession` / `ActivityEvent`** (`Domain/Statistics`) — historique d'activité. Ce ne sont pas des agrégats : aucune invariante métier, uniquement des enregistrements horodatés.
- **`TestAggregate`** — placeholder pour les tests.

### Services hébergés au démarrage

`SA818InitializerHostedService`, `AudioInitializerHostedService`, `SalonSeederHostedService`, `ReflectorSeederHostedService`, `StartupActivationHostedService`, `LogicTclInitializerHostedService`, `DtmfSalonSwitchService`, `DtmfAnnounceService`, `DtmfSystemCommandService`, `ReflectorConnectionAnnouncementService`, `SvxLinkDiagnosticsHostedService`, `ActivityRecorderHostedService`, `StatisticsPurgeHostedService`.

### La stack de test Docker

Quatre conteneurs, sur le réseau `svxlink-network` :

| Conteneur | Rôle | Indicatif | Protocole | Talkgroups |
|-----------|------|-----------|-----------|------------|
| `svxreflector` | réflecteur local, port 5300 | — | sert V2 et V3 en parallèle | TG 0, 240, 2403, 2404 |
| `svxlinkmanager-app` | l'application et son démon SVXLink | celui du salon | V3 via le salon « Réflecteur Local » | `DEFAULT_TG` du salon (0 par défaut) |
| `svxlink-node2` | nœud nu | `HB9GXP2-H` | **V2** (`AUTH_KEY`) | TG 240, imposé par `TG_FOR_V1_CLIENTS` |
| `svxlink-node3` | nœud nu | `HB9GXP3-H` | **V3** (certificat X.509) | émet sur 2403, surveille 240 et 2404 |

Ce qu'il faut en retenir :

- **Deux nœuds V3 sont nécessaires** pour observer quoi que ce soit des fonctions de SVXLink 25.05 : un talkgroup, un QSY, une priorité de monitoring ou un talker distant n'existent pas avec un nœud seul. C'est la raison d'être de `svxlink-node3`.
- **Le nœud V2 est conservé volontairement** : la coexistence V2/V3 sur un même réflecteur est le scénario réel de migration du parc. Elle impose `TG_FOR_V1_CLIENTS` côté réflecteur — un nœud V2 ne sait pas sélectionner de talkgroup, et sans cette variable il reste muet dès qu'un TG est utilisé sur le réflecteur.
- **Un nœud V2 doit être déclaré dans `[USERS]`/`[PASSWORDS]`** de `svxreflector.conf` ; `ACCEPT_CALLSIGN` ne filtre que la forme de l'indicatif.
- **La PKI n'est pas partagée** : chaque nœud V3 a son volume, et télécharge la CA du serveur tout seul (`CERT_DOWNLOAD_CA_BUNDLE`, actif par défaut). En revanche, si le volume PKI du réflecteur est recréé, la CA change et les nœuds gardent l'ancien bundle — le remède est de supprimer leur `ca-bundle.crt`, ou de repartir d'un `docker compose down -v`.
- **`TG#2403` porte `AUTO_QSY_AFTER=30`** : une conversation qui s'y prolonge est déplacée vers un TG tiré dans `RANDOM_QSY_RANGE` (`2409900:100`, syntaxe `<borne basse>:<nombre>`). C'est court exprès, pour éprouver le QSY sans attendre.
- **Le nœud applicatif se place sur un talkgroup depuis la fiche de son salon** (`DEFAULT_TG`, `MONITOR_TGS`), pas depuis un fichier versionné : sa configuration SVXLink est générée à l'activation.

## Points de vigilance

- **`svxlink-config/svxlink.conf` doit rester versionné** (exception dans `.gitignore`) : `SvxLinkConfigurationService` l'utilise comme template pour générer la config d'un salon. Le supprimer casse l'activation des salons.
- **Mocks d'infrastructure activés par configuration** : `SA818:UseMock`, `Wifi:UseMock`, `SvxLink:UseMockDaemon`, `Audio:UseMock`. Ce sont des implémentations de production destinées au développement sans matériel — **pas** des mocks de tests.
- **Cible de production** : Orange Pi (ARM 32 bits, RID `linux-arm`, arch Debian `armhf`) sous Armbian, service systemd `svxlinkmanagerv2.service`. Le code d'infrastructure suppose un environnement Linux (`nmcli`, `pico2wave`, PTY, `/dev/ttyS2`).
- **`src/SvxlinkManagerV2.Infrastructure/Class1.cs`** est un vestige de template vide, sans usage.

## Tests

Stack : **xUnit 2.9.2**, **FluentAssertions 8.8.0**, **NSubstitute 5.3.0**, **LanguageExt.UnitTesting 4.4.9.1** (`ShouldBeSuccess()` / `ShouldBeFail()` sur `Validation<Error, T>`), **ini-parser** pour valider les fichiers de config générés dans `Infrastructure.Tests`.

Principes :
- **Mocker les interfaces avec NSubstitute**, jamais écrire de classes mock concrètes dans les projets de tests.
- Persistance : **SQLite in-memory** dans `Infrastructure.Tests`.
- Les tests de génération de config SVXLink écrivent sur le **filesystem réel** dans un répertoire temporaire.
- Chaque projet source déclare `InternalsVisibleTo` vers son projet de tests miroir.

### Tests d'intégration

`tests/SvxlinkManagerV2.Integration.Tests` monte la stack Docker versionnée et vérifie qu'une liaison en protocole V3 s'établit réellement : CSR signée par `dev-ca-hook.sh`, canal chiffré, `Login OK ... with protocol version 3.0`, et coexistence avec le nœud V2.

C'est le filet qui manquait : les tests unitaires ne couvrent que la **génération** de `svxlink.conf` et la résolution de stratégie, si bien que l'absence d'`openssl` dans l'image du réflecteur — qui rendait *toute* connexion V3 impossible — a pu vivre sans être détectée.

- **Ils sont ignorés par défaut.** `DockerComposeFactAttribute` ne les exécute que si `SVXLINK_INTEGRATION_TESTS=1` : `dotnet test SvxlinkManagerV2.sln` reste donc exécutable sans Docker et sans filtre. Ils portent aussi `Trait("Category", "Integration")`.
- **La stack est le sujet du test**, pas un décor : ils pilotent le `docker-compose.yml` du dépôt sous un nom de projet dédié (`svxlink-integration`), avec `down -v` avant et après — la stack de développement du contributeur n'est pas touchée, et une CA laissée par une exécution précédente ne peut pas masquer une régression de la chaîne de signature.
- **L'application .NET n'est pas montée** : la chaîne éprouvée (PKI, signature, chiffrement, login) passe intégralement par les nœuds nus, et son image coûterait plusieurs minutes de plus.
- **Durées mesurées** (poste de développement, 06/09/2026) : **1 min 15 s** images déjà construites — la connexion du nœud en est l'essentiel, le réflecteur refusant la première tentative avant de faire signer la CSR — et **2 min 20 s** de plus pour construire les deux images à froid (`--no-cache`), SVXLink 25.05 étant compilé depuis les sources avec `make -j$(nproc)`. Sur un runner GitHub à 2 vCPU, cette compilation domine largement le job : le workflow [`integration-tests.yml`](.github/workflows/integration-tests.yml) est prévu avec `timeout-minutes: 90` et publie sa durée réelle dans le résumé du job. C'est ce coût qui justifie de le tenir hors de la boucle de chaque commit, sur `develop` et `release/*` seulement.
- **Vérifié le 06/09/2026** : en retirant `openssl` de l'image du réflecteur, aucun `Login OK` n'est journalisé et le hook affiche « dépendance manquante » — les tests tombent bien, ce qui était l'objet de leur écriture.

## Références externes

- Projet legacy (spécification fonctionnelle de référence) : https://github.com/marcbat/svxlinkmanager et son wiki https://github.com/marcbat/svxlinkmanager/wiki
- Sources SVXLink (validation des paramètres de config) : manpages `src/doc/man/*.5` et `src/doc/*.adoc` du dépôt SVXLink. Versions cibles **19.09.2** et **25.05**.
- [docs/svxlink-hb9gxp-configuration-validee.md](docs/svxlink-hb9gxp-configuration-validee.md) : configuration matérielle/SVXLink validée en conditions réelles sur Orange Pi Zero, référence pour les valeurs par défaut.
