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

**`Logic.tcl` est l'aiguillage, pas un simple mouchard.** `Logic::processCommandQueue` l'appelle *avant* le `CmdParser` de SVXLink et s'arrête si le script retourne autre chose que 0. Tout ce qui n'est pas explicitement rendu à SVXLink (`return 0`) lui échappe donc définitivement — c'est le cas des modules 1-19, et désormais des commandes talkgroup.

**Les commandes talkgroup (V3) ne passent pas par l'application.** Elles sont routées par SVXLink lui-même vers `ReflectorLogic::remoteCmdReceived`, à travers le **préfixe de commande** déclaré dans `CONNECT_LOGICS` de la section `[LinkToReflector]` : `SimplexLogic:35,ReflectorLogic`. Sans ce préfixe, `LinkManager::addLogic` n'instancie aucun `LinkCmd` (condition `atoi(cmd) > 0`) et **aucune** commande talkgroup n'est atteignable, ni par radio ni par le PTY.

| Séquence | Effet (`DtmfTalkGroupCommands`) |
|---|---|
| `35*#` | Annonce du talkgroup courant et de l'état de la liaison |
| `351<tg>#` / `351#` | Sélectionner `<tg>` / revenir au talkgroup précédent |
| `352<tg>#` / `352#` | QSY vers `<tg>` / vers un talkgroup tiré au hasard |
| `353#` | Suivre le dernier QSY annoncé |
| `354<tg>#` | Surveillance temporaire de `<tg>` (`TMP_MONITOR_TIMEOUT`) |

Trois conséquences à retenir :

- **`35` est un préfixe, pas une plage.** SVXLink capte toute séquence qui commence par ces chiffres, quelle que soit sa longueur : `DtmfCodeRanges.IsValidForSalon` refuse donc `35` et `3500-3599` en plus des plages historiques. Le préfixe `9` des exemples de la documentation SVXLink a été écarté pour cette raison — il aurait capté les codes semés 96 (RRF), 97 (FON) et 98 (Salon Technique). `35` se loge dans la plage 300-399, déjà réservée, sans toucher aux annonces 301-307, aux commandes système 310-320 ni aux commandes internes 398/399.
- **`35#` seul est volontairement non routé.** `LinkManager::cmdReceived` interpréterait la sous-commande vide comme une *désactivation du lien*, ce qui couperait l'audio entre la radio et le réflecteur sur une faute de frappe. Le motif de `Logic.tcl` et `DtmfTalkGroupCommands.IsTalkGroupCommand` doivent rester synchronisés.
- **Un salon V2 ne déclare aucun préfixe** : SVXLink 19.09.2 ignore les talkgroups, et sa configuration générée reste `SimplexLogic,ReflectorLogic`.

**`EVENT_HANDLER` désigne `events.tcl`, jamais `events.d/local/Logic.tcl`.** Chaque logique a son propre interpréteur TCL, et SVXLink y appelle des procédures qualifiées par le nom de la logique (`ReflectorLogic::report_tg_status`, `SimplexLogic::startup`). C'est `events.tcl` qui charge `events.d/*.tcl` — dont `ReflectorLogic.tcl`, qui crée ce namespace — **puis** les surcharges de `events.d/local/*.tcl`, dont notre `Logic.tcl`. Pointer `Logic.tcl` directement laissait l'interpréteur de `ReflectorLogic` sans namespace : toutes ses annonces échouaient en « invalid command name », et aucune annonce de talkgroup n'était jouée.

**Le talkgroup sélectionné n'est pas rémanent.** Passé `TG_SELECT_TIMEOUT` sans activité, SVXLink journalise un `Selecting TG #0` et le nœud retombe hors talkgroup. C'est une raison de plus pour que l'état affiché vienne des logs et non de la commande émise.

### Talkgroups (protocole V3)

`ITalkGroupStateService` (implémenté par `TalkGroupTracker`, singleton) suit l'état des talkgroups du nœud et le publie sous forme de `TalkGroupState` : talkgroup courant et précédent, **origine** de la sélection, surveillances temporaires, QSY en attente ou échoué. Comme `ReflectorLinkStateTracker`, **il lit le flux de logs plutôt que la commande émise** — et pour la même raison : le talkgroup change aussi sans que l'application l'ait demandé.

**La source est l'instrumentation TCL, pas les libellés de log.** `Logic.tcl` porte un namespace `ReflectorLogic` qui **enveloppe** les procédures d'événement de SVXLink (`tg_selected`, `tg_qsy`, `tmp_monitor_add`…) et émet des lignes `TG_EVENT:<type>[:<valeur>…]`. Ces procédures sont une interface stable de l'amont, là où les messages de log changent d'une version à l'autre — et le projet en pilote deux.

Trois précautions tiennent cette instrumentation :

- **Envelopper, jamais remplacer.** La procédure d'origine est renommée `__svxmgr_orig_<nom>` puis appelée à la fin de la nôtre. La redéfinir supprimerait les annonces vocales de talkgroup et de QSY, qui vivent dans son corps. Le renommage sert aussi de garde contre un double enveloppement.
- **Le fichier est chargé dans l'interpréteur de *chaque* logique.** Dans celui de `SimplexLogic`, ou sur SVXLink 19.09.2 qui ignore les talkgroups, ces procédures n'existent pas : la garde `[info procs]` fait alors du bloc un no-op.
- **`EVENT_HANDLER` doit désigner `events.tcl`** (cf. pipeline DTMF), faute de quoi le namespace `ReflectorLogic` n'existe pas et rien n'est enveloppé.

Le motif `ReflectorLogic: Selecting TG #<n>`, émis par le C++, est **conservé en repli** : lui seul reste disponible sur un nœud dont le `Logic.tcl` n'a pas encore été redéployé. Les deux sources décrivent le même appel à `selectTg` et ne peuvent pas se contredire ; le repli n'apporte simplement ni l'origine ni les surveillances.

`TalkGroupState.NotApplicable` vaut pour un salon V2, un perroquet ou le mode autonome, et le tracker ignore alors les lignes résiduelles du daemon. Les commandes d'activation appellent `ApplyDefault(DefaultTg)` ou `MarkNotApplicable()`, comme elles le font déjà pour l'état de la liaison ; `ApplyDefault` remet aussi les surveillances temporaires à zéro, puisqu'elles appartiennent au daemon qui s'arrête.

`SelectTalkGroupCommand` (`Features/Salons/SelectTalkGroup`) ne touche ni la base ni le daemon : elle compose `351<tg>` dans le PTY DTMF via `IDtmfPtyWriter`. **Son succès n'est que celui de l'émission** — c'est le tracker qui dit le talkgroup réellement courant. Le talkgroup sélectionné à chaud n'est donc pas persisté : le salon repart sur son `DEFAULT_TG` à la réactivation.

### État du daemon vs état de la liaison réflecteur

Deux notions distinctes, à ne pas confondre :

- `ISvxLinkDaemonService.IsRunningAsync()` ne dit que si le **processus** svxlink tourne ;
- `IReflectorLinkStateService` (implémenté par `ReflectorLinkStateTracker`, singleton) suit l'état de la **liaison** au réflecteur en parsant les lignes `ReflectorLogic` du flux de logs, et le publie via `OnStateChanged`.

Un daemon actif ne garantit pas une liaison : `AUTH_KEY` erronée, hôte injoignable ou certificat rejeté laissent le processus en vie sans que le nœud soit relié. **Les deux échecs de certificat sont distincts et leurs remèdes sont opposés** : `CertificateRejected` désigne le certificat *du nœud* (à faire signer à nouveau), `ServerCertificateUntrusted` l'autorité *du réflecteur* que le nœud ne reconnaît plus — typiquement une PKI régénérée côté serveur. Le second se répare depuis l'interface : `ResetReflectorTrustCommand` supprime le `ca-bundle.crt` de `CERT_PKI_DIR` (et lui seul) puis redémarre le daemon, seul moyen de rouvrir une session TLS et de retélécharger l'autorité.

**Une PKI de réflecteur régénérée casse la confiance dans les deux sens** — vérifié sur la stack le 07/09/2026 en recréant le volume `svxlink-pki-reflector`. Supprimer le `ca-bundle.crt` rétablit le chiffrement, mais le réflecteur rejette ensuite le certificat du nœud, signé par l'ancienne autorité : `tls_process_client_certificate: certificate verify failed` côté serveur, et côté nœud une simple `Connection closed by remote peer` — aucun message de certificat, donc aucune cause identifiable depuis le nœud. Le rétablissement complet demande de supprimer aussi le `.crt` et le `.csr` du nœud pour qu'il émette une nouvelle demande de signature. `ResetReflectorTrustCommand` ne le fait délibérément pas : sur un réflecteur distant, la signature dépend d'un tiers, et détruire le certificat du nœud transformerait une panne réparable en attente indéfinie. C'est `RegenerateCertificateRequestCommand`, à confirmation explicite, qui porte cette seconde moitié — voir le cycle de vie du certificat du nœud ci-dessous. Les commandes d'activation appellent `BeginConnecting()` (salon réflecteur) ou `MarkNotApplicable()` (salon perroquet, mode autonome) avant le redémarrage du daemon — en mode autonome le tracker ignore les logs, sinon des lignes résiduelles feraient apparaître une liaison en erreur. **Ajouter un motif de log reconnu impose de mettre à jour `ReflectorLinkStateTracker.Interpret` et ses tests**, en vérifiant les deux versions de SVXLink (`ReflectorLogic.cpp`).

### Paramètres V3 du salon et identité du certificat

Trois réglages de comportement vivent dans `SvxLinkConfiguration`, donc dans le JSON de l'owned entity — **aucune migration n'est nécessaire pour en ajouter** :

| Variable | Défaut SVXLink | Quand y toucher |
|---|---|---|
| `UDP_HEARTBEAT_INTERVAL` | 15 s | déconnexions répétées par expiration de présence UDP, typiquement en 4G ou sur un faisceau |
| `ANNOUNCE_REMOTE_MIN_INTERVAL` | non défini | un talkgroup qui s'active en boucle fait parler le nœud sans arrêt |
| `VERBOSE` | actif | réflecteur très fréquenté, dont les entrées/sorties noient le tampon de 1000 lignes |

**Une valeur laissée au défaut de SVXLink n'est pas écrite** dans la configuration générée, et la clé est retirée du template si elle s'y trouvait : le fichier dit ce que l'opérateur a choisi, pas ce que le logiciel aurait fait de toute façon.

L'identité du certificat (`CERT_SUBJ_GN`, `SN`, `OU`, `O`, `L`, `ST`, `C`) relève du **nœud**, pas du salon : elle vit dans `GeneralConfigurationAggregate` et est reprise par tous les salons V3. `SvxLinkConfigurationService` la lit donc par `IGeneralConfigurationRepository`. Les champs vides ne sont pas écrits — SVXLink construit alors un sujet réduit au Common Name — et **les anciennes valeurs sont effacées à chaque génération**, sans quoi un champ vidé par l'opérateur survivrait dans le fichier et continuerait d'être signé.

Elle est stockée en JSON (`OwnsOne(...).ToJson()`) comme la configuration des salons : une seule migration, et aucune pour les sept champs suivants. **Toute migration ajoutée impose en revanche deux gestes** — l'inscrire dans `AllMigrations` de `DatabaseMigratorTests`, et retirer sa colonne de la base héritée reconstituée par `SeedLegacyDatabase`, qui part du schéma courant et défait ce qui lui est postérieur. Sans le second, la migration s'applique sur une colonne déjà présente et échoue.

Modifier ces valeurs change le sujet de la demande de signature : le certificat existant devient caduc. L'interface le dit, et la section « Certificat du nœud » du tableau de bord offre la régénération.

### Cycle de vie du certificat du nœud

En V3, obtenir un certificat est un **processus asynchrone qui fait intervenir un tiers** : le nœud génère sa clé et sa demande, l'envoie au réflecteur, puis attend que le sysop la signe. L'attente peut durer des heures ou des jours, et l'application n'en montrait qu'un « échec de connexion » — de quoi conclure, à raison de son point de vue, que le logiciel ne marche pas.

`INodeCertificateReader` déduit l'état de la présence des fichiers de `CERT_PKI_DIR`, nommés d'après l'indicatif du salon actif :

| Fichiers présents | État | Ce que dit l'interface |
|---|---|---|
| aucun | `NotGenerated` | la clé est créée au premier démarrage en V3 |
| `.key` + `.csr` | `PendingSignature` | **pas une panne** : la demande attend le sysop |
| `.crt` | `Valid` / `Expiring` / `Expired` | sujet, émetteur, échéance |
| `.crt` illisible | `Unreadable` | régénérer la remplacera |

**La clé privée n'est jamais ouverte.** Sa présence est constatée, rien de plus : aucun chemin de ce code ne doit pouvoir la faire remonter jusqu'à une page web.

**Régénérer la demande efface le `.csr` et le `.crt`, jamais le `.key`** — la clé est l'identité du nœud, et la renouveler n'apporte rien à une demande à refaire signer. Le redémarrage de SVXLink qui suit est indispensable : le processus en cours garderait sinon en mémoire le certificat effacé.

C'est aussi le remède qui manquait au cas `ServerCertificateUntrusted` : après une régénération de PKI côté réflecteur, oublier l'autorité ne suffit pas, le certificat du nœud est lui aussi signé par l'ancienne. L'action est volontairement à confirmation explicite — le nœud reste hors ligne jusqu'à la nouvelle signature, immédiate sur le réflecteur local, dépendante d'un tiers ailleurs.

### Configuration du réflecteur local

La configuration vit en INI brut dans `ReflectorAggregate.Config`, éditée telle quelle sur la page `/reflector`. Trois mécanismes l'encadrent.

**Le modèle par défaut est livré complet.** `ReflectorSeederHostedService.GetDefaultReflectorConfig()` déclare, commentés en français : `HTTP_SRV_PORT` et `COMMAND_PTY` (lots 3 et 4), `TG_FOR_V1_CLIENTS` — sans lequel **un nœud V2 reste muet** dès qu'un talkgroup est utilisé —, `RANDOM_QSY_RANGE` — sans lequel le QSY aléatoire et `AUTO_QSY_AFTER` ne fonctionnent pas —, et la protection `SQL_TIMEOUT` / `SQL_TIMEOUT_BLOCKTIME` contre un émetteur bloqué.

`RANDOM_QSY_RANGE` suit la convention `<MCC>9900:100` : `2289900:100` en Suisse, `2089900:100` en France. `TG_FOR_V1_CLIENTS` désigne le TG 240, le même que la stack de test — sur un réflecteur local le numéro est libre, mais un seul chiffre dans tout le projet évite les malentendus. **Le talkgroup désigné doit exister** : la section `[TG#240]` accompagne la variable.

**Le seeder ne met jamais à jour une configuration existante** — il sort dès qu'un réflecteur est en base. C'est arrivé trois lots de suite : chaque clé ajoutée au modèle manquait aux installations déjà en service, et la fonctionnalité correspondante s'y dégradait en silence. `ReflectorRecommendedSettings` est désormais la liste de référence, partagée par le modèle et par le diagnostic de la page ; `ApplyRecommendedSettingsCommand` ajoute à une configuration existante ce qu'elle ne déclare pas. **Y ajouter une entrée suffit** pour qu'une installation ancienne se la voie proposer.

La fusion est **textuelle** (`ReflectorConfigurationMerger`), délibérément : `IniFile` ne conserve pas les commentaires des sections, et réécrire le fichier à partir de lui effacerait toutes les notes de l'opérateur. Rien n'est jamais modifié ni supprimé — seules des lignes manquantes sont ajoutées en fin de section, précédées de leur justification.

**La syntaxe est validée avant enregistrement.** `IniFile.ParseContent` est tolérant et ignore en silence ce qu'il ne comprend pas ; le démon, lui, répond `Illegal value syntax on line N` et **redémarre en boucle**. `ReflectorConfigurationValidator` signale la ligne fautive pendant la saisie, et `ReflectorAggregate` refuse d'enregistrer une configuration qui mettrait le réflecteur hors service. Les sections inconnues ne sont qu'un avertissement : SVXLink en ajoute d'une version à l'autre.

### Signature des certificats du réflecteur local

En protocole V3, un nœud dépose une demande de signature (CSR) et **ne peut pas se connecter tant qu'elle n'est pas signée**. Sans mécanisme de signature, la demande reste dans `pending_csrs/` et le nœud enchaîne les `Access denied` indéfiniment — vérifié sur la stack le 07/09/2026 en retirant le hook. La configuration par défaut du réflecteur déclare donc `COMMAND_PTY`, sans quoi le salon V3 livré par défaut serait structurellement inutilisable.

**Deux canaux, deux rôles.** Ce qui se **lit** passe par le système de fichiers, ce qui s'**ordonne** passe par le PTY :

| Besoin | Canal | Pourquoi |
|---|---|---|
| Lister les demandes | `<CERT_PKI_DIR>/pending_csrs/` | `CA PENDING` répond « Not yet implemented » en 25.05 |
| Signer, bloquer | `COMMAND_PTY` | seule voie d'action ; le PTY est en écriture, ses réponses partent au journal |

**Le jeu de commandes réel n'est pas celui de la manpage.** Le binaire 25.05 répond `Usage: CA PENDING|SIGN <callsign>|LS|RM <callsign>` — les `CA LS/LSC/LSP` documentés en amont n'existent pas. C'est le message d'usage du binaire qui fait foi.

**`openssl` n'est pas une dépendance de production.** Le réflecteur signe avec sa propre autorité, en interne. Seul `dev-ca-hook.sh` a besoin du binaire, et il est réservé au développement. La lecture des demandes est faite par .NET (`CertificateRequest.LoadSigningRequestPem`), le Subject Alternative Name étant décodé en ASN.1 — son énumération par `X509SubjectAlternativeNameExtension` n'existe qu'à partir de .NET 9, et les projets sources ciblent net8.0.

**Un indicatif venu d'une demande est une entrée non fiable.** Il est fourni par un tiers et part dans un PTY que le démon lit ligne par ligne : `ReflectorCallsign.IsValid` le restreint à `[A-Za-z0-9/-]`, et `ReflectorCommandPtyWriter` refuse toute commande contenant un saut de ligne. Sans cela, `CA SIGN <indicatif>` pourrait en devenir deux.

**L'auto-signature n'est pas une case à cocher.** Elle vit dans la section `ReflectorCertificateAuthority` des appsettings, à `false` par défaut, et `CertificateAutoSignHostedService` ne démarre que si elle est explicitement activée. Elle signe **n'importe quel** indicatif : sur un réflecteur joignable de l'extérieur, l'activer revient à l'ouvrir à tout venant. La friction d'un fichier à éditer est celle que mérite ce réglage — et c'est aussi pourquoi le hook shell de développement n'est pas livré sur la cible de production.

**Dans la stack Docker, l'application ne peut pas signer.** Le PTY appartient à l'espace de noms `/dev/pts` du conteneur `svxreflector`. La stack garde donc `CERT_CA_HOOK` pour rester utilisable sans intervention ; commenter ce hook fait apparaître les demandes en attente, et la signature s'éprouve alors à la main :

```bash
docker exec svxreflector sh -c 'printf "CA SIGN HB9GXP3-H\n" > /tmp/reflector_ctrl'
```

### Nœuds connectés et leur talkgroup

`ConnectedNodesTracker` (singleton) tient la liste des nœuds connectés en parsant les lignes `Connected nodes:`, `Node joined:`, `Node left:`, `Talker start:` et `Talker stop:`. Ces lignes fonctionnent avec **n'importe quel** réflecteur, distant compris.

**Elles ne portent pas le talkgroup de chaque nœud** — seulement des indicatifs. Ce champ vient donc de l'API de statut du réflecteur local (voir ci-dessous), et reste `null` sur un réflecteur distant, où l'API n'est pas accessible. Le tableau de bord ne groupe par talkgroup que si le salon actif est en V3 **et** qu'au moins un nœud a un talkgroup connu ; sinon il conserve la liste plate. Tout ranger sous « aucun talkgroup » faute d'information serait plus trompeur que de ne rien grouper.

En V3, la liste plate est de toute façon trompeuse : deux nœuds connectés au même réflecteur sur des talkgroups différents ne s'entendent pas, et rien ne l'expliquait à l'opérateur.

Le talkgroup d'un nœud change **sans qu'aucune ligne de log ne le dise**. Le tracker s'abonne donc à `IReflectorStatusService.OnStatusChanged` et republie la liste via `OnNodesInitialized` — et non `OnNodeJoined`, qui déclenche une notification d'arrivée dans l'interface et sonnerait à chaque changement de talkgroup.

### Statut du réflecteur local (API HTTP)

SVXLink 25.05 expose l'état complet des nœuds connectés sur le serveur HTTP du réflecteur, activé par `HTTP_SRV_PORT`. C'est l'interface prévue par l'amont pour la supervision — l'outil officiel `svxreflector-status` ne fait rien d'autre que la lire — et elle donne ce que les logs ne donnent pas : le talkgroup de chaque nœud, ses talkgroups surveillés, sa version de protocole.

```json
{"nodes":{"HB9GXP3-H":{"isTalker":false,"machineArch":"x86_64","monitoredTGs":[240,2404],
 "projVer":"25.05","protoVer":{"majorVer":3,"minorVer":0},"restrictedTG":true,
 "sw":"SvxLink","swVer":"1.9.0","tg":0}}}
```

`ReflectorStatusPoller` (singleton **et** service hébergé — une seule instance, sinon la page lirait un instantané que personne n'alimente) interroge `/status` toutes les 5 s et publie un `ReflectorStatusSnapshot`. La page `/reflector` le lit par `GetReflectorStatusQuery` et s'abonne à `OnStatusChanged` : **elle ne parle jamais au serveur HTTP elle-même**, un serveur que sa propre documentation décrit comme simple, non audité et sensible à la charge.

Points à connaître :

- **Le port vient du fichier de configuration, pas de la base.** `HTTP_SRV_PORT` est relu dans `/etc/svxlink/svxreflector.conf` à chaque cycle : c'est ce fichier que charge le démon, et une modification prend effet sans redémarrer l'application. Un réflecteur configuré avant cette fonctionnalité n'a pas la clé — l'interface le dit et donne la ligne à ajouter, plutôt que d'afficher un réflecteur désert.
- **Chaque indisponibilité est nommée** (`DaemonStopped`, `PortNotConfigured`, `Unreachable`, `Invalid`). Afficher « aucun nœud connecté » alors que le démon est arrêté serait un mensonge. `Invalid` se distingue d'une liste vide : un réflecteur qui tourne sans nœud est un cas normal.
- **L'hôte interrogé vient de `LocalReflectorOptions`** — boucle locale en production, nom du service dans la stack Docker. L'état du *processus* n'est vérifié (`pgrep`) que si l'hôte est local : ailleurs le réflecteur vit dans un autre conteneur, et c'est l'absence de réponse HTTP qui fait foi.
- **SVXLink lie ce port sur toutes les interfaces** (`Async::TcpServer` construit sans adresse — non configurable). Il n'est pas publié dans `docker-compose.yml`, mais sur une machine exposée **il doit être fermé au pare-feu** : la manpage demande explicitement de ne pas l'exposer.
- La configuration par défaut du réflecteur vit dans `ReflectorSeederHostedService.GetDefaultReflectorConfig()`, désormais **publique et utilisée aussi par la page** `/reflector` : elle en tenait une copie, et une clé ajoutée d'un côté manquait de l'autre.

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
- **`svxlink-node3` porte une `SimplexLogic` reliée par `[LinkToReflector]`**, à l'image du nœud applicatif, et un `DTMF_CTRL_PTY`. C'est ce qui rend les commandes talkgroup éprouvables sans radio ni image applicative : `TalkGroupCommandTests` y écrit la séquence comme le ferait `DtmfPtyWriter`. Son `Rx1` utilise des valeurs **entières** de `VOX_THRESH`/`VOX_FILTER_DEPTH` — SVXLink rejette une valeur décimale par « Config variable Rx1/VOX_THRESH not set », et sans récepteur valide la logique simplex ne démarre pas.

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

`tests/SvxlinkManagerV2.Integration.Tests` monte la stack Docker versionnée et vérifie deux choses qu'aucun test unitaire ne peut atteindre :

- **la liaison V3 s'établit réellement** (`ReflectorV3ConnectionTests`) : CSR signée par `dev-ca-hook.sh`, canal chiffré, `Login OK ... with protocol version 3.0`, et coexistence avec le nœud V2 ;
- **les commandes talkgroup sont atteignables** (`TalkGroupCommandTests`) : une séquence écrite dans le `DTMF_CTRL_PTY` de `svxlink-node3` — comme le fait `DtmfPtyWriter` — doit traverser `Logic.tcl`, le préfixe de `CONNECT_LOGICS` et aboutir sur `Selecting TG #240`. Les tests unitaires ne valident que le *contenu* du fichier généré ; seule la stack montre que SVXLink l'honore.

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
