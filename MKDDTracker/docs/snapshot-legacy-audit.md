# Audit des dépendances au système Snapshot legacy

## Résumé

Audit en lecture seule du dépôt. Aucun comportement, page, inscription DI, migration ou fichier SQLite n’a été modifié. Seul ce rapport est ajouté.

- **34 fichiers de production et de test pertinents** : 25 contiennent une référence explicite au modèle ou aux tables legacy, 7 tests les exercent, et 2 fichiers supplémentaires assurent indirectement le maintien de la compatibilité SQLite (`MkddDatabaseInitializer.cs`, `MkddDatabaseMigrationMetadata.cs`). Les occurrences génériques de `Performances` dans le modèle moderne `HistoricalPerformances` ne sont pas comptées comme dépendances legacy.
- **4 fichiers classés G**, candidats à suppression après vérification de leur périmètre : `MkddEvolutionService.cs`, `MkddSnapshotService.cs`, `MkddTimeEvolutionChart.razor`, `MkddRankEvolutionChart.razor`. Aucun n’a été supprimé.
- **13 fichiers de schéma/compatibilité** ne doivent être retirés ou adaptés qu’après migration et validation des données : entités/contexte, inspecteur, artefacts de migrations, script SQL, initialiseur et métadonnées de baseline.
- **3 pages UI** restent dépendantes de Snapshot/Performances : `/` (dashboard), `/database-check` et `/import`.
- **`/evolution`, `/ranking` et `/joueurs` ne dépendent plus fonctionnellement** des tables `Snapshots` ou `Performances`. Elles utilisent les services, entités et graphiques historiques basés sur `HistoricalPerformances`.
- Vérifications exécutées : `dotnet build` réussi ; `dotnet test` réussi (**80 réussis, 0 échec, 0 ignoré**).

Les catégories A–G décrivent le rôle actuel. Elles peuvent se chevaucher pour une même chaîne : par exemple, `/import` est une dépendance UI (F) qui fournit encore l’import legacy (B).

## Catégories

- **A — Runtime métier encore nécessaire** : fonctionnalité applicative active qui requiert encore les données Snapshot.
- **B — Import/collecte** : ingestion HTML, création de données de démonstration ou collecte dans l’ancien modèle.
- **C — Migration vers `HistoricalPerformances`** : lecture des anciens snapshots et conversion vers l’historique.
- **D — Compatibilité legacy / migration de base** : modèle EF, initialisation, baseline et structure SQLite existante.
- **E — Tests uniquement** : code de test ou de support qui construit ou vérifie l’ancien schéma.
- **F — UI encore dépendante** : page qui interroge ou écrit l’ancien modèle.
- **G — Code devenu inutilisé et supprimable** : candidats sans appelant fonctionnel trouvé dans le projet ; suppression conditionnée aux précautions indiquées.

## Inventaire par fichier et catégorie

### A — Runtime métier encore nécessaire

Aucun des trois parcours historiques principaux n’utilise l’ancien modèle. La lecture de Snapshot encore active est limitée au dashboard, à la page d’inspection, aux diagnostics et aux outils de conversion/démonstration ci-dessous ; elle ne constitue pas une dépendance des classements historiques affichés dans les trois routes principales.

### B — Import et collecte legacy

| Fichier | Usage et décision |
|---|---|
| `MKDDTracker.Web/Components/Pages/Import.razor` | Import HTML via `MkddSnapshotImportService` ; dépendance UI F et import B. À conserver jusqu’au remplacement fonctionnel de l’import, puis à migrer vers `HistoricalPerformances`. |
| `Services/SnapshotService/MkddSnapshotImportService.cs` | Parse le HTML, crée les joueurs, snapshots et performances dans l’ancienne structure. Import réellement actif via `/import` ; ne pas supprimer avant remplacement. |
| `Services/SnapshotService/SnapshotImportResult.cs` | Résultat et statut de l’import legacy, consommés par la page `/import`. À remplacer avec l’import moderne. |
| `Services/MkddDemoDataSeeder.cs` | Crée une histoire synthétique dans une base de démonstration Snapshot/Performances ; invoqué explicitement par `--seed-demo`, pas au démarrage normal. |
| `Program.cs` | Le chemin `--seed-demo` parse le HTML et lance le seeder, puis l’analyse comparative legacy. Il s’agit d’un outil explicite, pas du démarrage normal de l’application. |
| `Scraper/Parsing/MkddCoursePageParser.cs`, `Scraper/Models/MkddPerformance.cs` | Le parseur et son DTO produisent des performances de scraping, mais ne dépendent pas du schéma Snapshot. Ils peuvent être réutilisés par un import moderne ; le mot `Performances` ne suffit pas à en faire une dépendance EF legacy. |

### C — Migration vers `HistoricalPerformances`

| Fichier | Usage et décision |
|---|---|
| `Program.cs` | Expose `--dry-run-snapshot-history-migration` et `--migrate-snapshot-history`, ainsi qu’un diagnostic en lecture seule qui compare les données Snapshot à `HistoricalPerformances`. Le diagnostic est à retirer ou adapter après la transition ; conserver les commandes jusqu’à validation de la conversion. |
| `Services/SnapshotService/MkddSnapshotHistoryMigrationService.cs` | Lit `db.Performances`, prépare les alias modernes et les événements de `HistoricalPerformances`, puis les importe transactionnellement. Précondition essentielle à toute suppression des données source. |
| `Services/SnapshotService/MkddSnapshotHistoryMigrationResult.cs` | Porte le résultat et les plans de la conversion. À conserver avec le service jusqu’à ce que la migration soit exécutée, rapprochée et archivée. |
| `MKDDTracker.Tests/MkddSnapshotHistoryMigrationServiceTests.cs` | Vérifie dry-run, import, calcul des PR et idempotence. À garder tant que le convertisseur est maintenu ; ensuite remplacer par des tests de migration/validation des données. |

La conversion actuelle ne copie pas chaque rang d'un snapshot : elle extrait les améliorations strictes de chrono par joueur/circuit, ignore les chronos identiques et moins bons, définit `TimeType = ThreeLap`, laisse la fréquence inconnue et dérive `FirstSeenAt` de `RecordDate`. Il faut valider que ces règles répondent aux besoins avant de considérer les tables source supprimables.

### D — Compatibilité legacy et base SQLite

| Fichier | Usage et décision |
|---|---|
| `Data/Database/MkddDbContext.cs` | Expose les DbSet `Snapshots` et `Performances` ainsi que leurs index. Retrait seulement après migration de schéma et suppression des consommateurs. |
| `Data/Database/MkddDatabaseSchemaInspector.cs` | Détecte le schéma SQLite legacy (`Snapshots`, `Performances`) pour reconnaître les bases existantes. Adapter en même temps que la stratégie de compatibilité, après vérification des bases utilisateur. |
| `Data/Entities/MkddSnapshot.cs`, `Data/Entities/MkddPerformanceEntity.cs` | Entités persistées de l’ancien modèle ; à retirer uniquement après élimination de toutes les références et migration des données. |
| `Data/Entities/MkddCourse.cs`, `Data/Entities/MkddPlayer.cs` | Contiennent les navigations `Snapshots` / `Performances` vers le modèle legacy, en plus de relations utilisées par l’historique moderne. Retirer seulement les navigations legacy ; conserver les entités et relations modernes nécessaires. |
| `Data/Database/Migrations/20261110103156_InitialCreate.cs` et `Data/Database/Migrations/20261110103156_InitialCreate.Designer.cs` | Migration initiale créant `Snapshots` et `Performances` et sa représentation EF. À **ne pas modifier** ; une éventuelle suppression de tables doit être une nouvelle migration ultérieure. |
| `Data/Database/Migrations/20261116135957_AddHistoricalYahooImport.Designer.cs` et `Data/Database/Migrations/MkddDbContextModelSnapshot.cs` | Le modèle EF généré conserve à la fois les tables historiques et les entités legacy. À mettre à jour uniquement par une nouvelle migration EF après la conversion. |
| `historical-import-migration.sql` | Script SQL autonome qui crée les anciennes tables et les tables historiques. À distinguer des migrations exécutées par EF ; ne pas le modifier dans le cadre de cet audit. |
| `Data/Database/MkddDatabaseInitializer.cs` | Détecte les bases existantes, enregistre la baseline initiale et applique les migrations ; crée une sauvegarde utilisateur avant les migrations. Dépendance indirecte à la compatibilité des bases legacy, même sans référence directe aux types Snapshot. |
| `Data/Database/Migrations/MkddDatabaseMigrationMetadata.cs` | Conserve l’identifiant de la migration initiale utilisé pour baseliner une ancienne base. À revoir avec l’initialiseur et la stratégie de migration, pas isolément. |
| `MKDDTracker.Tests/MkddDatabaseMigrationTests.cs`, `MKDDTracker.Tests/MkddDatabaseInitializerTests.cs`, `MKDDTracker.Tests/DatabaseTestSupport.cs` | Vérifient la création du schéma initial, la baseline, la sauvegarde et la préservation des données legacy. Ces tests protègent le comportement de compatibilité et devront évoluer avec la migration finale. |

La migration `20261116135957_AddHistoricalYahooImport.cs` crée les tables `HistoricalPlayerAliases`, `HistoricalPerformances` et `HistoricalPerformanceSources`, mais ne supprime pas `Snapshots`/`Performances`. La présence de cette migration ne signifie donc pas que le schéma legacy a déjà été retiré.

### E — Tests legacy

Les fichiers ci-dessous utilisent directement les entités ou les tables legacy :

- `MKDDTracker.Tests/DatabaseTestSupport.cs` — construit une base legacy et des données de test.
- `MKDDTracker.Tests/MkddDatabaseInitializerTests.cs` — baseline, lecture, sauvegarde et préservation de l’ancien schéma.
- `MKDDTracker.Tests/MkddDatabaseMigrationTests.cs` — vérifie que la migration initiale crée les tables legacy.
- `MKDDTracker.Tests/MkddDemoDataSeederTests.cs` — vérifie le seeder Snapshot de démonstration.
- `MKDDTracker.Tests/MkddSnapshotHistoryMigrationServiceTests.cs` — conversion vers l’historique.
- `MKDDTracker.Tests/MkddSnapshotImportServiceTests.cs` — import HTML legacy et transaction.
- `MKDDTracker.Tests/MkddSnapshotServiceTests.cs` — persistance et calcul des rangs legacy.

Les mentions de `HistoricalPerformances` dans les tests des services historiques modernes ne sont pas des dépendances aux anciennes tables. Les tests de `MkddCoursePageParser` et `MkddRankingCalculator` concernent le DTO de scraping et la règle de classement, pas le stockage Snapshot.

### F — UI encore dépendante

| Route / fichier | Dépendance constatée |
|---|---|
| `/` — `MKDDTracker.Web/Components/Pages/Home.razor` | Compte `db.Snapshots`, liste les snapshots récents et compte leurs performances. Le dashboard est directement dépendant. |
| `/database-check` — `MKDDTracker.Web/Components/Pages/DatabaseCheck.razor` | Charge `db.Snapshots` avec circuit et performances ; outil de diagnostic legacy. |
| `/import` — `MKDDTracker.Web/Components/Pages/Import.razor` | Injecte `MkddSnapshotImportService` et présente le résultat d’un import Snapshot. |

**Confirmation des trois pages principales :**

- `/evolution` utilise `MkddHistoricalRankingSelectionService`, `MkddHistoricalRankingComparisonService`, `MkddHistoricalPlayerTimelineService` et les graphiques `MkddHistoricalTimeEvolutionChart` / `MkddHistoricalRankEvolutionChart` ; leur source est l’historique moderne.
- `/ranking` utilise `MkddHistoricalRankingSelectionService` et `MkddHistoricalRankingService`.
- `/joueurs` utilise `MkddHistoricalRankingSelectionService` et `MkddHistoricalPlayerProfileService`.

Aucune de ces pages ne requête `db.Snapshots` ou `db.Performances`, ni n’injecte un service de Snapshot. Elles ne dépendent donc plus fonctionnellement de ces tables. `MKDDTracker.Web/Program.cs` conserve néanmoins des inscriptions legacy : `MkddSnapshotImportService` pour `/import` et `MkddSnapshotComparisonService`, qui n’est pas utilisé par ces trois routes. Aucune inscription DI n’a été modifiée.

### G — Candidats au code inutilisé et supprimable

| Fichier | Constat et condition |
|---|---|
| `Services/EvolutionService/MkddEvolutionService.cs` | Service historique ancien lisant `db.Performances`, sans appelant applicatif trouvé. Candidat direct, à confirmer par recherche d’usages lors de la suppression. |
| `Services/SnapshotService/MkddSnapshotService.cs` | Service d’écriture Snapshot sans appelant de production trouvé ; seul le test dédié l’utilise. Candidat direct après confirmation que le chemin de sauvegarde n’est pas consommé par un usage externe. |
| `MKDDTracker.Web/Components/Charts/MkddTimeEvolutionChart.razor` | Ancien graphique non référencé par les pages actuelles ; celles-ci utilisent les graphiques historiques. |
| `MKDDTracker.Web/Components/Charts/MkddRankEvolutionChart.razor` | Ancien graphique non référencé par les pages actuelles ; celles-ci utilisent les graphiques historiques. |

Ne sont **pas** classés supprimables immédiatement : `MkddSnapshotComparisonService`, `MkddPlayerEvolutionService`, `MkddEvolutionReportService` et `MkddEvolutionConsoleRenderer` participent au chemin explicite `--seed-demo` de `Program.cs`. Il faut décider de maintenir, moderniser ou retirer cette démonstration avant de supprimer cette chaîne. `MkddSnapshotImportService` reste utilisé par `/import`.

## Cleanup Step 1 Results

### Décisions finales

| Candidat G | Décision | Justification |
|---|---|---|
| `MKDDTracker/Services/EvolutionService/MkddEvolutionService.cs` | `SafeToDelete` | Aucun appelant direct, aucune injection DI, aucune référence Razor, aucun test, aucune réflexion/configuration/sérialisation ou commande console ne l’utilise. Le composant ne participe à aucun flux actif. |
| `MKDDTracker/Services/SnapshotService/MkddSnapshotService.cs` | `SafeToDelete` | Aucun appelant de production ni inscription DI. Il n’est utilisé que par son test dédié, qui couvre exclusivement cette ancienne persistance. |
| `MKDDTracker/MKDDTracker.Web/Components/Charts/MkddTimeEvolutionChart.razor` | `SafeToDelete` | Aucun composant Razor ou page ne le référence ; les pages utilisent les graphiques historiques modernes. |
| `MKDDTracker/MKDDTracker.Web/Components/Charts/MkddRankEvolutionChart.razor` | `SafeToDelete` | Aucun composant Razor ou page ne le référence ; les pages utilisent les graphiques historiques modernes. |

### Éléments réellement supprimés

- `MKDDTracker/Services/EvolutionService/MkddEvolutionService.cs`
- `MKDDTracker/Services/SnapshotService/MkddSnapshotService.cs`
- `MKDDTracker/MKDDTracker.Web/Components/Charts/MkddTimeEvolutionChart.razor`
- `MKDDTracker/MKDDTracker.Web/Components/Charts/MkddRankEvolutionChart.razor`
- `MKDDTracker.Tests/MkddSnapshotServiceTests.cs`, test exclusivement dédié au service Snapshot supprimé

Aucune inscription DI n’était associée à ces candidats : les classes n’étaient enregistrées ni dans `MKDDTracker.Web/Program.cs`, ni dans les modes console. Aucun using de production n’a donc dû être retiré pour une inscription devenue orpheline.

### Dépendances Snapshot restantes

Les entités, DbSet, tables SQLite, migrations, initialiseur, inspecteur, service de migration, import legacy, données de démonstration et leurs tests restent inchangés. Les dépendances opérationnelles restantes sont notamment `/`, `/database-check`, `/import`, `MkddSnapshotImportService`, `MkddSnapshotComparisonService`, `MkddDemoDataSeeder`, `MkddSnapshotHistoryMigrationService` et la compatibilité SQLite.

### Prochaine étape recommandée

Remplacer ou retirer en priorité les dépendances UI du dashboard, puis remplacer l’import legacy par un flux historique moderne. Ensuite, traiter les modes console de démonstration/comparaison séparément, puis valider les données avant toute migration finale retirant les tables ou entités legacy.

### Validation de cette étape

- branche Git : `dev`, aucun commit créé
- aucune migration EF Core créée ou modifiée
- aucune donnée SQLite lue ou modifiée
- aucune table, entité, DbSet ou migration des candidats interdits modifié
- les pages `/`, `/joueurs`, `/ranking`, `/evolution`, `/import` et `/database-check` n’ont pas été modifiées
- les noms des quatre types supprimés ne sont plus présents dans le code source ou les tests; seules les mentions historiques de ce rapport peuvent subsister

## Programmes, scraping et injection de dépendances

- `MKDDTracker.Web/Program.cs` initialise la base utilisateur et inscrit encore les services d’import/comparaison legacy ; la page d’import en consomme un. La comparaison legacy n’est pas utilisée par les trois routes historiques.
- `Program.cs` (projet console) conserve les deux commandes de migration, un diagnostic Snapshot en lecture seule et la démo `--seed-demo`. Les outils de migration peuvent écrire dans la base réelle après confirmation explicite ; ils n’ont pas été exécutés pendant cet audit.
- Le parseur de pages et `MkddPerformance` sont une source d’ingestion réutilisable, et non une dépendance au schéma EF ancien. La persistance legacy commence dans `MkddSnapshotImportService` / `MkddDemoDataSeeder`.
- L’initialiseur SQLite et l’inspecteur de schéma doivent continuer de reconnaître les bases existantes jusqu’à ce qu’une nouvelle migration et une politique de sauvegarde/compatibilité soient prêtes.

## Migrations, base et risques

1. `20261110103156_InitialCreate` définit le schéma Snapshot d’origine. Ne pas réécrire une migration déjà appliquée.
2. `20261116135957_AddHistoricalYahooImport` ajoute le stockage historique mais conserve les anciennes tables.
3. `MkddDbContextModelSnapshot` et le designer de la migration historique reflètent encore l’ancien modèle EF.
4. Le script `historical-import-migration.sql` est un autre artefact de création de schéma et contient toujours les tables legacy.
5. Aucune donnée SQLite n’a été lue ou modifiée par ce rapport ; les commandes de migration Snapshot n’ont pas été lancées.

**Risques à traiter avant une migration DB finale :** pertes de données ou de métadonnées lors de la conversion (rang, dates de classement, fréquence, standard et vidéo), sens métier de `RecordDate` versus `RankingDate`, absence de fréquence dans les événements importés, base utilisateur déjà migrée ou non, contraintes SQLite/FK et reprise après échec. La procédure finale devra sauvegarder la base utilisateur, comparer les volumes et séries, vérifier l’idempotence et conserver un moyen de restauration. Le comportement du seeder de démonstration doit être traité séparément de la base réelle.

## Tests et validation

- `dotnet build` : réussi.
- `dotnet test` : total **80**, réussis **80**, échecs **0**, ignorés **0**.
- Les tests existants de conversion, import legacy, sauvegarde et initialisation constituent une protection utile avant refactor. Aucun nouveau test n’était nécessaire pour cet audit documentaire, qui n’altère aucun comportement.

## Ordre recommandé de suppression / migration

1. **Étape 1 — Dashboard** : remplacer les comptes et la liste Snapshot de `Home.razor` par des indicateurs explicitiemnt définis à partir de l’historique moderne ; tester les cas sans données et historiques incomplets.
2. **Étape 2 — Import moderne** : définir la conversion du HTML vers `HistoricalPerformances` (type, fréquence, date/source, statut, alias) et remplacer `/import` sans perte des validations transactionnelles requises.
3. **Étape 3 — Anciens services Evolution et graphiques** : décider du sort de `--seed-demo`, puis retirer ou moderniser l’ancienne chaîne de comparaison et ses modèles/graphiques ; la route `/evolution` actuelle reste sur ses services historiques.
4. **Étape 4 — DI et code mort** : après preuve d’absence d’appelants, retirer les inscriptions et composants/services legacy inutilisés ; conserver les services de conversion tant que l’étape 6 n’est pas validée.
5. **Étape 5 — Tests legacy** : remplacer les tests des imports et pages retirés ; conserver des tests de migration, sauvegarde et compatibilité tant que les bases existantes doivent être supportées.
6. **Étape 6 — Entités et DbSet** : retirer les navigations, `MkddSnapshot`, `MkddPerformanceEntity`, puis les DbSet et l’inspection legacy seulement lorsque plus aucun code ne les consomme et que les données ont été rapprochées.
7. **Étape 7 — Migration DB finale** : ajouter une **nouvelle** migration EF (sans modifier les migrations existantes) pour retirer les tables legacy selon une stratégie de sauvegarde et de compatibilité testée ; déployer après validation sur copie de base et procédure de restauration.

## Chiffres de clôture

- **Fichiers pertinents dépendant encore du système Snapshot legacy : 34** — 25 fichiers de production avec références concrètes, 7 fichiers de tests, 2 fichiers de compatibilité indirecte (initialiseur et métadonnées de baseline).
- **Fichiers classés G, candidats supprimables : 4** — deux anciens services et deux anciens graphiques listés ci-dessus ; suppression différée et non effectuée.
- **Fichiers de schéma/compatibilité nécessitant une migration préalable : 13** — `MkddDbContext`, `MkddDatabaseSchemaInspector`, quatre entités/navigations (`MkddSnapshot`, `MkddPerformanceEntity`, `MkddCourse`, `MkddPlayer`), les quatre artefacts EF (`InitialCreate.cs`, son `.Designer.cs`, `AddHistoricalYahooImport.Designer.cs`, `MkddDbContextModelSnapshot.cs`), `historical-import-migration.sql`, `MkddDatabaseInitializer.cs` et `MkddDatabaseMigrationMetadata.cs`.
- **Pages UI encore dépendantes : 3** — `/`, `/database-check`, `/import`.
- **Tests réussis : 80 exactement**.
- **Ordre recommandé :** dashboard ? import moderne ? anciens services Evolution et graphiques ? DI/code mort ? tests legacy ? entités/DbSet ? migration DB finale.

- **Références textuelles Snapshot restantes dans les fichiers C#/Razor source : 371 occurrences** après le retrait des quatre candidats G et du test dédié supprimé. Ce chiffre est une mesure de contrôle textuelle, pas un décompte de dépendances activées; les usages runtime sont documentés dans les sections précédentes.
- **Tests pertinents restants : 104 réussis, 0 échec, 0 ignoré.**
- **Avertissements de build restants : 3 avertissements préexistants** (duplicate using CS0105 dans un test; 2 recommandations analytiques xUnit2013). Aucun avertissement nouveau lié au nettoyage.

## Cleanup Step 2 — Legacy Evolution Engine

### Sécurisation DI

La configuration des services du projet Web est désormais centralisée dans `MkddWebServiceRegistration.AddMkddWebServices`. `MKDDTracker.Web/Program.cs` utilise cette même méthode; il n’existe plus de seconde configuration DI parallèle.

Le test `MkddWebServiceRegistrationTests.CriticalHistoricalServices_CanBeResolved` vérifie explicitement la résolution de:

- `MkddHistoricalModernImportService`
- `MkddHistoricalDatabaseCheckService`
- `MkddHistoricalDashboardService`
- `MkddHistoricalRankingService`
- `MkddHistoricalRankingComparisonService`
- `MkddHistoricalRankingSelectionService`
- `MkddHistoricalPlayerTimelineService`
- `MkddHistoricalPlayerProfileService`

La configuration fournit désormais aussi `MkddDbContext` comme service scoped en plus du `IDbContextFactory<MkddDbContext>`, afin que les services historiques utilisant directement `MkddDbContext` soient résolvables.

### Validation Step 2

- Branche: `dev`; aucun commit créé.
- Tests avant ajout du test DI: 104; après ajout: 105; réussi: 105, échec: 0, ignoré: 0.
- `dotnet build`: réussi avec les 3 avertissements préexistants, aucun avertissement nouveau.
- Dry-run Release: 600 performances analysées, 200 séries, 204 événements PR existants, 0 événement à créer, 394 snapshots identiques ignorés, 2 chronos moins bons ignorés.
- Aucun mode d’écriture, aucune migration EF, aucune modification SQLite.

### Classification finale

| Élément | Décision | Justification |
|---|---|---|
| `MkddEvolutionService` | `SafeToDelete` | Déjà absent du workspace; aucune référence runtime, Razor, DI, console, migration ou test trouvée. |
| `MkddPlayerEvolutionService` | `SafeToDelete` | Déjà absent du workspace; ancien moteur Snapshot sans appelant actuel. |
| `MkddEvolutionReportService` | `SafeToDelete` | Déjà absent du workspace; uniquement lié aux anciens rapports/étapes d’évolution. |
| `MkddSnapshotComparisonService` | `LegacyRequired` | Conservé: utilisé par les tests de compatibilité legacy et les chemins Snapshot conservés. |
| `MkddRankEvolutionChart` | `SafeToDelete` | Déjà absent; `/evolution` utilise uniquement `MkddHistoricalRankEvolutionChart`. |
| `MkddTimeEvolutionChart` | `SafeToDelete` | Déjà absent; `/evolution` utilise uniquement `MkddHistoricalTimeEvolutionChart`. |
| `MkddPlayerEvolution` | `SafeToDelete` | Déjà absent; ancien modèle Snapshot sans appelant. |
| `MkddCompletePlayerEvolution` | `SafeToDelete` | Déjà absent; exclusif à l’ancien moteur `MkddPlayerEvolutionService`. |
| `MkddPlayerEvolutionStep` | `SafeToDelete` | Déjà absent; exclusif à l’ancien moteur/rendements de rapport. |
| Modèles exclusifs `MkddPlayerEvolutionPoint`, `MkddPlayerSnapshotComparison`, `MkddPlayerEvolutionReport`, renderer console | `SafeToDelete` | Déjà absents; aucune référence restante hors documentation historique. |

### Éléments conservés

- `MkddSnapshot`, `MkddPerformanceEntity`, leurs DbSet et les tables SQLite sont conservés.
- `MkddDatabaseInitializer`, `MkddDatabaseSchemaInspector`, les migrations EF Core et les scripts SQL sont conservés.
- `MkddSnapshotHistoryMigrationService` reste le pont vers `HistoricalPerformances`.
- L’ancien import Snapshot et `/import` restent actifs.
- Les tests historiques, de migration, de sauvegarde/initialisation SQLite et de compatibilité restent conservés.

### Nettoyage effectué

Aucune inscription DI utile n’a été retirée dans cette étape hormis celles déjà absentes avec les anciens services. Les anciens services et modèles n’apportent aucune référence actuelle. Aucun ancien composant graphique ni test exclusivement associé à ces composants ne reste.

### Dépendances Snapshot restantes

Les dépendances Snapshot restantes sont le dashboard, `/database-check`, `/import`, la compatibilité SQLite, `MkddSnapshotComparisonService` et `MkddSnapshotHistoryMigrationService` jusqu’à validation finale de la migration de données.

### Prochaine étape recommandée

Valider le dry-run de migration, puis moderniser `/import` et le diagnostic/dashboard. Supprimer les entités, DbSet et tables Snapshot seulement après rapprochement des données et nouvelle migration EF explicitement planifiée.
