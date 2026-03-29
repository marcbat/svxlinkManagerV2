# Instructions Copilot pour le projet SvxLinkManager V2

## Description du Projet
Ce projet `svxlinkmanagerV2` est une refonte ("reboot") du gestionnaire pour le logiciel SVXLink. Il a pour but de fournir une interface de pilotage et de configuration pour les nœuds radioamateurs utilisant SVXLink.

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
*   **Framework** : .NET 8.
*   **Frontend** : Blazor.
*   **Pattern** : Clean Architecture.
*   **Structure de Projet** :
    *   **src/** : Contient tous les projets sources (Domain, Application, Infrastructure, Presentation).
    *   **tests/** : Contient tous les projets de tests (structure miroir de src/).
        *   Projets `*.Tests` : Tests unitaires et d'intégration par couche (Domain.Tests, Application.Tests, Infrastructure.Tests, Presentation.Tests).
*   **Structure des Couches** :
    1.  **Presentation (Blazor)** : Interface utilisateur.
    2.  **Application** : Cas d'utilisation, orchestration.
    3.  **Domain** : Règles métier, entités (Logique Core).
    4.  **Infrastructure** : **Seule couche autorisée à interagir directement avec SVXLink** (fichiers de config, commandes système, lecture de logs). Elle implémente les interfaces définies par les couches supérieures.

*   **Stack CQRS & Persistance** :
    *   **Médiation/CQRS** : Utiliser **MediatR 12.4.0** pour l'envoi et le traitement de toutes les Commandes et Queries.
        *   **Convention de Code** : Les Commands/Queries et leurs Handlers doivent **systématiquement être dans le même fichier** pour améliorer la lisibilité et la maintenabilité.
        *   Exemple : `PingCommand.cs` contient à la fois `PingCommand` (record) et `PingCommandHandler` (classe).
    *   **Persistance** : Utiliser **EF Core 9 + SQLite**.
    *   **Stratégie de Données** : **CRUD simple** (pas d'Event Sourcing).

*   **Programmation Fonctionnelle & Gestion des Erreurs** :
    *   Utiliser le **Result Pattern** (préférence pour l'objet **Validation**) via la librairie **LanguageExt** dans les couches **Application** et **Infrastructure** pour la gestion des flux et des erreurs.

*   **Stratégie de Test & Environnement** :
    *   **Environnement de Développement et Tests (Docker Compose)** : 2 conteneurs distincts :
        1.  **Application + SVXLink** : Le conteneur Blazor/.NET avec **SVXLink 19.09.2 installé localement**. Cela permet à l'application de démarrer/arrêter le daemon SVXLink via des commandes systemctl réelles.
        2.  **SVXReflector** : Un conteneur dédié au réflecteur SVXLink.
    *   **Environnement de Production** :
        *   **Hardware cible** : **Orange Pi** (architecture ARM)
        *   **OS cible** : **Armbian** (distribution Linux basée sur Debian pour ARM)
        *   **Déploiement** : L'application et SVXLink 19.09.2 sont installés directement sur l'Orange Pi
        *   **Architecture** : Optimisation nécessaire pour ARM (compilation native, dépendances ARM)
    *   **Stack de Tests** : Utiliser systématiquement la stack suivante pour tous les tests :
        *   **Xunit** : Framework de tests unitaires et d'intégration.
        *   **FluentAssertions** : Assertions expressives et lisibles.
        *   **NSubstitute** : Mocking et substitution pour les tests unitaires (interfaces uniquement, pas de classes concrètes mock).
        *   **LanguageExt.UnitTesting** : Extensions pour tester les types `Validation<Error, T>` et autres constructs fonctionnels.
    *   **Principe fondamental** : **Pas d'implémentations mock concrètes**. Utiliser NSubstitute pour mocker les interfaces dans les tests unitaires.
    *   **Persistance dans les tests** : Utiliser **SQLite in-memory** pour les tests d'intégration de persistance (EF Core). La génération des fichiers de configuration SVXLink utilise le **filesystem réel** (répertoire temporaire).
    *   **Organisation des tests** : Les tests sont organisés par couche dans 4 projets distincts : `Application.Tests`, `Infrastructure.Tests`, `Domain.Tests`, `Presentation.Tests`. Chaque projet couvre les tests unitaires et d'intégration propres à sa couche.
