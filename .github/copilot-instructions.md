# Instructions Copilot pour le projet SvxLinkManager V2

## Description du Projet
Ce projet `svxlinkmanagerV2` est une refonte ("reboot") du gestionnaire pour le logiciel SVXLink. Il a pour but de fournir une interface de pilotage et de configuration pour les nœuds radioamateurs utilisant SVXLink.

## Structure et Ressources du Workspace

L'environnement de travail contient plusieurs dossiers critiques auxquels vous devez vous référer :

1.  **Projet Legacy (Référence Métier)**
    *   **Emplacement** : `../svxlinkmanager` (ou `c:\repos\svxlinkmanager`)
    *   **Usage** : Consultez ce dossier pour comprendre la logique métier, les anciennes implémentations et les fonctionnalités qui doivent être portées ou améliorées dans la V2. C'est la source de vérité pour le comportement attendu.

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

## Architecture Technique
*   **Framework** : .NET 10.
*   **Frontend** : Blazor.
*   **Pattern** : Clean Architecture.
*   **Structure des Couches** :
    1.  **Presentation (Blazor)** : Interface utilisateur.
    2.  **Application** : Cas d'utilisation, orchestration.
    3.  **Domain** : Règles métier, entités (Logique Core).
    4.  **Infrastructure** : **Seule couche autorisée à interagir directement avec SVXLink** (fichiers de config, commandes système, lecture de logs). Elle implémente les interfaces définies par les couches supérieures.

*   **Stack CQRS & Persistance** :
    *   **Médiation/CQRS** : Utiliser **Wolverine** pour l'envoi et le traitement de toutes les Commandes et Queries.
    *   **Persistance** : Utiliser **Marten** (sur **PostgreSQL**).
    *   **Stratégie de Données** : Implémenter strictement le pattern **Event Sourcing** pour la persistance des états.

*   **Programmation Fonctionnelle & Gestion des Erreurs** :
    *   Utiliser le **Result Pattern** (préférence pour l'objet **Validation**) via la librairie **LanguageExt** dans les couches **Application** et **Infrastructure** pour la gestion des flux et des erreurs.
