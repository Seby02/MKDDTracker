# Audit des d�pendances au syst�me Snapshot legacy

## R�sum�

Audit en lecture seule du d�p�t. Aucun comportement, page, inscription DI, migration ou fichier SQLite n�a �t� modifi�. Seul ce rapport est ajout�.

- **34 fichiers de production et de test pertinents** : 25 contiennent une r�f�rence explicite au mod�le ou aux tables legacy, 7 tests les exercent, et 2 fichiers suppl�mentaires assurent indirectement le maintien de la compatibilit� SQLite (`MkddDatabaseInitializer.cs`, `MkddDatabaseMigrationMetadata.cs`). Les occurrences g�n�riques de `Performances` dans le mod�le moderne `HistoricalPerformances` ne sont pas compt�es comme d�pendances legacy.
- **4 fichiers class�s G**, candidats � suppression apr�s v�rification de leur p�rim�tre : `MkddEvolutionService.cs`, `MkddSnapshotService.cs`, `MkddTimeEvolutionChart.razor`, `MkddRankEvolutionChart.razor`. Aucun n�a �t� supprim�.
- **13 fichiers de sch�ma/compatibilit�** ne doivent �tre retir�s ou adapt�s qu�apr�s migration et validation des donn�es : entit�s/contexte, inspecteur, artefacts de migrations, script SQL, initialiseur et m�tadonn�es de baseline.
- **3 pages UI** restent d�pendantes de Snapshot/Performances : `/` (dashboard), `/database-check` et `/import`.
- **`/evolution`, `/ranking` et `/joueurs` ne d�pendent plus fonctionnellement** des tables `Snapshots` ou `Performances`. Elles utilisent les services, entit�s et graphiques historiques bas�s sur `HistoricalPerformances`.
- V�rifications ex�cut�es : `dotnet build` r�ussi ; `dotnet test` r�ussi (**80 r�ussis, 0 �chec, 0 ignor�**).

Les cat�gories A�G d�crivent le r�le actuel. Elles peuvent se chevaucher pour une m�me cha�ne : par exemple, `/import` est une d�pendance UI (F) qui fournit encore l�import legacy (B).

## Cat�gories

- **A � Runtime m�tier encore n�cessaire** : fonctionnalit� applicative active qui requiert encore les donn�es Snapshot.
- **B � Import/collecte** : ingestion HTML, cr�ation de donn�es de d�monstration ou collecte dans l�ancien mod�le.
- **C � Migration vers `HistoricalPerformances`** : lecture des anciens snapshots et conversion vers l�historique.
- **D � Compatibilit� legacy / migration de base** : mod�le EF, initialisation, baseline et structure SQLite existante.
- **E � Tests uniquement** : code de test ou de support qui construit ou v�rifie l�ancien sch�ma.
- **F � UI encore d�pendante** : page qui interroge ou �crit l�ancien mod�le.
- **G � Code devenu inutilis� et supprimable** : candidats sans appelant fonctionnel trouv� dans le projet ; suppression conditionn�e aux pr�cautions indiqu�es.

## Inventaire par fichier et cat�gorie

### A � Runtime m�tier encore n�cessaire

Aucun des trois parcours historiques principaux n�utilise l�ancien mod�le. La lecture de Snapshot encore active est limit�e au dashboard, � la page d�inspection, aux diagnostics et aux outils de conversion/d�monstration ci-dessous ; elle ne constitue pas une d�pendance des classements historiques affich�s dans les trois routes principales.

### B � Import et collecte legacy

| Fichier | Usage et d�cision |
|---|---|
| `MKDDTracker.Web/Components/Pages/Import.razor` | Import HTML via `MkddSnapshotImportService` ; d�pendance UI F et import B. � conserver jusqu�au remplacement fonctionnel de l�import, puis � migrer vers `HistoricalPerformances`. |
| `Services/SnapshotService/MkddSnapshotImportService.cs` | Parse le HTML, cr�e les joueurs, snapshots et performances dans l�ancienne structure. Import r�ellement actif via `/import` ; ne pas supprimer avant remplacement. |
| `Services/SnapshotService/SnapshotImportResult.cs` | R�sultat et statut de l�import legacy, consomm�s par la page `/import`. � remplacer avec l�import moderne. |
| `Services/MkddDemoDataSeeder.cs` | Cr�e une histoire synth�tique dans une base de d�monstration Snapshot/Performances ; invoqu� explicitement par `--seed-demo`, pas au d�marrage normal. |
| `Program.cs` | Le chemin `--seed-demo` parse le HTML et lance le seeder, puis l�analyse comparative legacy. Il s�agit d�un outil explicite, pas du d�marrage normal de l�application. |
| `Scraper/Parsing/MkddCoursePageParser.cs`, `Scraper/Models/MkddPerformance.cs` | Le parseur et son DTO produisent des performances de scraping, mais ne d�pendent pas du sch�ma Snapshot. Ils peuvent �tre r�utilis�s par un import moderne ; le mot `Performances` ne suffit pas � en faire une d�pendance EF legacy. |

### C � Migration vers `HistoricalPerformances`

| Fichier | Usage et d�cision |
|---|---|
| `Program.cs` | Expose `--dry-run-snapshot-history-migration` et `--migrate-snapshot-history`, ainsi qu�un diagnostic en lecture seule qui compare les donn�es Snapshot � `HistoricalPerformances`. Le diagnostic est � retirer ou adapter apr�s la transition ; conserver les commandes jusqu�� validation de la conversion. |
| `Services/SnapshotService/MkddSnapshotHistoryMigrationService.cs` | Lit `db.Performances`, pr�pare les alias modernes et les �v�nements de `HistoricalPerformances`, puis les importe transactionnellement. Pr�condition essentielle � toute suppression des donn�es source. |
| `Services/SnapshotService/MkddSnapshotHistoryMigrationResult.cs` | Porte le r�sultat et les plans de la conversion. � conserver avec le service jusqu�� ce que la migration soit ex�cut�e, rapproch�e et archiv�e. |
| `MKDDTracker.Tests/MkddSnapshotHistoryMigrationServiceTests.cs` | V�rifie dry-run, import, calcul des PR et idempotence. � garder tant que le convertisseur est maintenu ; ensuite remplacer par des tests de migration/validation des donn�es. |

La conversion actuelle ne copie pas chaque rang d'un snapshot : elle extrait les am�liorations strictes de chrono par joueur/circuit, ignore les chronos identiques et moins bons, d�finit `TimeType = ThreeLap`, laisse la fr�quence inconnue et d�rive `FirstSeenAt` de `RecordDate`. Il faut valider que ces r�gles r�pondent aux besoins avant de consid�rer les tables source supprimables.

### D � Compatibilit� legacy et base SQLite

| Fichier | Usage et d�cision |
|---|---|
| `Data/Database/MkddDbContext.cs` | Expose les DbSet `Snapshots` et `Performances` ainsi que leurs index. Retrait seulement apr�s migration de sch�ma et suppression des consommateurs. |
| `Data/Database/MkddDatabaseSchemaInspector.cs` | D�tecte le sch�ma SQLite legacy (`Snapshots`, `Performances`) pour reconna�tre les bases existantes. Adapter en m�me temps que la strat�gie de compatibilit�, apr�s v�rification des bases utilisateur. |
| `Data/Entities/MkddSnapshot.cs`, `Data/Entities/MkddPerformanceEntity.cs` | Entit�s persist�es de l�ancien mod�le ; � retirer uniquement apr�s �limination de toutes les r�f�rences et migration des donn�es. |
| `Data/Entities/MkddCourse.cs`, `Data/Entities/MkddPlayer.cs` | Contiennent les navigations `Snapshots` / `Performances` vers le mod�le legacy, en plus de relations utilis�es par l�historique moderne. Retirer seulement les navigations legacy ; conserver les entit�s et relations modernes n�cessaires. |
| `Data/Database/Migrations/20261110103156_InitialCreate.cs` et `Data/Database/Migrations/20261110103156_InitialCreate.Designer.cs` | Migration initiale cr�ant `Snapshots` et `Performances` et sa repr�sentation EF. � **ne pas modifier** ; une �ventuelle suppression de tables doit �tre une nouvelle migration ult�rieure. |
| `Data/Database/Migrations/20261116135957_AddHistoricalYahooImport.Designer.cs` et `Data/Database/Migrations/MkddDbContextModelSnapshot.cs` | Le mod�le EF g�n�r� conserve � la fois les tables historiques et les entit�s legacy. � mettre � jour uniquement par une nouvelle migration EF apr�s la conversion. |
| `historical-import-migration.sql` | Script SQL autonome qui cr�e les anciennes tables et les tables historiques. � distinguer des migrations ex�cut�es par EF ; ne pas le modifier dans le cadre de cet audit. |
| `Data/Database/MkddDatabaseInitializer.cs` | D�tecte les bases existantes, enregistre la baseline initiale et applique les migrations ; cr�e une sauvegarde utilisateur avant les migrations. D�pendance indirecte � la compatibilit� des bases legacy, m�me sans r�f�rence directe aux types Snapshot. |
| `Data/Database/Migrations/MkddDatabaseMigrationMetadata.cs` | Conserve l�identifiant de la migration initiale utilis� pour baseliner une ancienne base. � revoir avec l�initialiseur et la strat�gie de migration, pas isol�ment. |
| `MKDDTracker.Tests/MkddDatabaseMigrationTests.cs`, `MKDDTracker.Tests/MkddDatabaseInitializerTests.cs`, `MKDDTracker.Tests/DatabaseTestSupport.cs` | V�rifient la cr�ation du sch�ma initial, la baseline, la sauvegarde et la pr�servation des donn�es legacy. Ces tests prot�gent le comportement de compatibilit� et devront �voluer avec la migration finale. |

La migration `20261116135957_AddHistoricalYahooImport.cs` cr�e les tables `HistoricalPlayerAliases`, `HistoricalPerformances` et `HistoricalPerformanceSources`, mais ne supprime pas `Snapshots`/`Performances`. La pr�sence de cette migration ne signifie donc pas que le sch�ma legacy a d�j� �t� retir�.

### E � Tests legacy

Les fichiers ci-dessous utilisent directement les entit�s ou les tables legacy :

- `MKDDTracker.Tests/DatabaseTestSupport.cs` � construit une base legacy et des donn�es de test.
- `MKDDTracker.Tests/MkddDatabaseInitializerTests.cs` � baseline, lecture, sauvegarde et pr�servation de l�ancien sch�ma.
- `MKDDTracker.Tests/MkddDatabaseMigrationTests.cs` � v�rifie que la migration initiale cr�e les tables legacy.
- `MKDDTracker.Tests/MkddDemoDataSeederTests.cs` � v�rifie le seeder Snapshot de d�monstration.
- `MKDDTracker.Tests/MkddSnapshotHistoryMigrationServiceTests.cs` � conversion vers l�historique.
- `MKDDTracker.Tests/MkddSnapshotImportServiceTests.cs` � import HTML legacy et transaction.
- `MKDDTracker.Tests/MkddSnapshotServiceTests.cs` � persistance et calcul des rangs legacy.

Les mentions de `HistoricalPerformances` dans les tests des services historiques modernes ne sont pas des d�pendances aux anciennes tables. Les tests de `MkddCoursePageParser` et `MkddRankingCalculator` concernent le DTO de scraping et la r�gle de classement, pas le stockage Snapshot.

### F � UI encore d�pendante

| Route / fichier | D�pendance constat�e |
|---|---|
| `/` � `MKDDTracker.Web/Components/Pages/Home.razor` | Compte `db.Snapshots`, liste les snapshots r�cents et compte leurs performances. Le dashboard est directement d�pendant. |
| `/database-check` � `MKDDTracker.Web/Components/Pages/DatabaseCheck.razor` | Charge `db.Snapshots` avec circuit et performances ; outil de diagnostic legacy. |
| `/import` � `MKDDTracker.Web/Components/Pages/Import.razor` | Injecte `MkddSnapshotImportService` et pr�sente le r�sultat d�un import Snapshot. |

**Confirmation des trois pages principales :**

- `/evolution` utilise `MkddHistoricalRankingSelectionService`, `MkddHistoricalRankingComparisonService`, `MkddHistoricalPlayerTimelineService` et les graphiques `MkddHistoricalTimeEvolutionChart` / `MkddHistoricalRankEvolutionChart` ; leur source est l�historique moderne.
- `/ranking` utilise `MkddHistoricalRankingSelectionService` et `MkddHistoricalRankingService`.
- `/joueurs` utilise `MkddHistoricalRankingSelectionService` et `MkddHistoricalPlayerProfileService`.

Aucune de ces pages ne requ�te `db.Snapshots` ou `db.Performances`, ni n�injecte un service de Snapshot. Elles ne d�pendent donc plus fonctionnellement de ces tables. `MKDDTracker.Web/Program.cs` conserve n�anmoins des inscriptions legacy : `MkddSnapshotImportService` pour `/import` et `MkddSnapshotComparisonService`, qui n�est pas utilis� par ces trois routes. Aucune inscription DI n�a �t� modifi�e.

### G � Candidats au code inutilis� et supprimable

| Fichier | Constat et condition |
|---|---|
| `Services/EvolutionService/MkddEvolutionService.cs` | Service historique ancien lisant `db.Performances`, sans appelant applicatif trouv�. Candidat direct, � confirmer par recherche d�usages lors de la suppression. |
| `Services/SnapshotService/MkddSnapshotService.cs` | Service d��criture Snapshot sans appelant de production trouv� ; seul le test d�di� l�utilise. Candidat direct apr�s confirmation que le chemin de sauvegarde n�est pas consomm� par un usage externe. |
| `MKDDTracker.Web/Components/Charts/MkddTimeEvolutionChart.razor` | Ancien graphique non r�f�renc� par les pages actuelles ; celles-ci utilisent les graphiques historiques. |
| `MKDDTracker.Web/Components/Charts/MkddRankEvolutionChart.razor` | Ancien graphique non r�f�renc� par les pages actuelles ; celles-ci utilisent les graphiques historiques. |

Ne sont **pas** class�s supprimables imm�diatement : `MkddSnapshotComparisonService`, `MkddPlayerEvolutionService`, `MkddEvolutionReportService` et `MkddEvolutionConsoleRenderer` participent au chemin explicite `--seed-demo` de `Program.cs`. Il faut d�cider de maintenir, moderniser ou retirer cette d�monstration avant de supprimer cette cha�ne. `MkddSnapshotImportService` reste utilis� par `/import`.

## Cleanup Step 1 Results

### D�cisions finales

| Candidat G | D�cision | Justification |
|---|---|---|
| `MKDDTracker/Services/EvolutionService/MkddEvolutionService.cs` | `SafeToDelete` | Aucun appelant direct, aucune injection DI, aucune r�f�rence Razor, aucun test, aucune r�flexion/configuration/s�rialisation ou commande console ne l�utilise. Le composant ne participe � aucun flux actif. |
| `MKDDTracker/Services/SnapshotService/MkddSnapshotService.cs` | `SafeToDelete` | Aucun appelant de production ni inscription DI. Il n�est utilis� que par son test d�di�, qui couvre exclusivement cette ancienne persistance. |
| `MKDDTracker/MKDDTracker.Web/Components/Charts/MkddTimeEvolutionChart.razor` | `SafeToDelete` | Aucun composant Razor ou page ne le r�f�rence ; les pages utilisent les graphiques historiques modernes. |
| `MKDDTracker/MKDDTracker.Web/Components/Charts/MkddRankEvolutionChart.razor` | `SafeToDelete` | Aucun composant Razor ou page ne le r�f�rence ; les pages utilisent les graphiques historiques modernes. |

### �l�ments r�ellement supprim�s

- `MKDDTracker/Services/EvolutionService/MkddEvolutionService.cs`
- `MKDDTracker/Services/SnapshotService/MkddSnapshotService.cs`
- `MKDDTracker/MKDDTracker.Web/Components/Charts/MkddTimeEvolutionChart.razor`
- `MKDDTracker/MKDDTracker.Web/Components/Charts/MkddRankEvolutionChart.razor`
- `MKDDTracker.Tests/MkddSnapshotServiceTests.cs`, test exclusivement d�di� au service Snapshot supprim�

Aucune inscription DI n��tait associ�e � ces candidats : les classes n��taient enregistr�es ni dans `MKDDTracker.Web/Program.cs`, ni dans les modes console. Aucun using de production n�a donc d� �tre retir� pour une inscription devenue orpheline.

### D�pendances Snapshot restantes

Les entit�s, DbSet, tables SQLite, migrations, initialiseur, inspecteur, service de migration, import legacy, donn�es de d�monstration et leurs tests restent inchang�s. Les d�pendances op�rationnelles restantes sont notamment `/`, `/database-check`, `/import`, `MkddSnapshotImportService`, `MkddSnapshotComparisonService`, `MkddDemoDataSeeder`, `MkddSnapshotHistoryMigrationService` et la compatibilit� SQLite.

### Prochaine �tape recommand�e

Remplacer ou retirer en priorit� les d�pendances UI du dashboard, puis remplacer l�import legacy par un flux historique moderne. Ensuite, traiter les modes console de d�monstration/comparaison s�par�ment, puis valider les donn�es avant toute migration finale retirant les tables ou entit�s legacy.

### Validation de cette �tape

- branche Git : `dev`, aucun commit cr��
- aucune migration EF Core cr��e ou modifi�e
- aucune donn�e SQLite lue ou modifi�e
- aucune table, entit�, DbSet ou migration des candidats interdits modifi�
- les pages `/`, `/joueurs`, `/ranking`, `/evolution`, `/import` et `/database-check` n�ont pas �t� modifi�es
- les noms des quatre types supprim�s ne sont plus pr�sents dans le code source ou les tests; seules les mentions historiques de ce rapport peuvent subsister

## Programmes, scraping et injection de d�pendances

- `MKDDTracker.Web/Program.cs` initialise la base utilisateur et inscrit encore les services d�import/comparaison legacy ; la page d�import en consomme un. La comparaison legacy n�est pas utilis�e par les trois routes historiques.
- `Program.cs` (projet console) conserve les deux commandes de migration, un diagnostic Snapshot en lecture seule et la d�mo `--seed-demo`. Les outils de migration peuvent �crire dans la base r�elle apr�s confirmation explicite ; ils n�ont pas �t� ex�cut�s pendant cet audit.
- Le parseur de pages et `MkddPerformance` sont une source d�ingestion r�utilisable, et non une d�pendance au sch�ma EF ancien. La persistance legacy commence dans `MkddSnapshotImportService` / `MkddDemoDataSeeder`.
- L�initialiseur SQLite et l�inspecteur de sch�ma doivent continuer de reconna�tre les bases existantes jusqu�� ce qu�une nouvelle migration et une politique de sauvegarde/compatibilit� soient pr�tes.

## Migrations, base et risques

1. `20261110103156_InitialCreate` d�finit le sch�ma Snapshot d�origine. Ne pas r��crire une migration d�j� appliqu�e.
2. `20261116135957_AddHistoricalYahooImport` ajoute le stockage historique mais conserve les anciennes tables.
3. `MkddDbContextModelSnapshot` et le designer de la migration historique refl�tent encore l�ancien mod�le EF.
4. Le script `historical-import-migration.sql` est un autre artefact de cr�ation de sch�ma et contient toujours les tables legacy.
5. Aucune donn�e SQLite n�a �t� lue ou modifi�e par ce rapport ; les commandes de migration Snapshot n�ont pas �t� lanc�es.

**Risques � traiter avant une migration DB finale :** pertes de donn�es ou de m�tadonn�es lors de la conversion (rang, dates de classement, fr�quence, standard et vid�o), sens m�tier de `RecordDate` versus `RankingDate`, absence de fr�quence dans les �v�nements import�s, base utilisateur d�j� migr�e ou non, contraintes SQLite/FK et reprise apr�s �chec. La proc�dure finale devra sauvegarder la base utilisateur, comparer les volumes et s�ries, v�rifier l�idempotence et conserver un moyen de restauration. Le comportement du seeder de d�monstration doit �tre trait� s�par�ment de la base r�elle.

## Tests et validation

- `dotnet build` : r�ussi.
- `dotnet test` : total **80**, r�ussis **80**, �checs **0**, ignor�s **0**.
- Les tests existants de conversion, import legacy, sauvegarde et initialisation constituent une protection utile avant refactor. Aucun nouveau test n��tait n�cessaire pour cet audit documentaire, qui n�alt�re aucun comportement.

## Ordre recommand� de suppression / migration

1. **�tape 1 � Dashboard** : remplacer les comptes et la liste Snapshot de `Home.razor` par des indicateurs explicitiemnt d�finis � partir de l�historique moderne ; tester les cas sans donn�es et historiques incomplets.
2. **�tape 2 � Import moderne** : d�finir la conversion du HTML vers `HistoricalPerformances` (type, fr�quence, date/source, statut, alias) et remplacer `/import` sans perte des validations transactionnelles requises.
3. **�tape 3 � Anciens services Evolution et graphiques** : d�cider du sort de `--seed-demo`, puis retirer ou moderniser l�ancienne cha�ne de comparaison et ses mod�les/graphiques ; la route `/evolution` actuelle reste sur ses services historiques.
4. **�tape 4 � DI et code mort** : apr�s preuve d�absence d�appelants, retirer les inscriptions et composants/services legacy inutilis�s ; conserver les services de conversion tant que l��tape 6 n�est pas valid�e.
5. **�tape 5 � Tests legacy** : remplacer les tests des imports et pages retir�s ; conserver des tests de migration, sauvegarde et compatibilit� tant que les bases existantes doivent �tre support�es.
6. **�tape 6 � Entit�s et DbSet** : retirer les navigations, `MkddSnapshot`, `MkddPerformanceEntity`, puis les DbSet et l�inspection legacy seulement lorsque plus aucun code ne les consomme et que les donn�es ont �t� rapproch�es.
7. **�tape 7 � Migration DB finale** : ajouter une **nouvelle** migration EF (sans modifier les migrations existantes) pour retirer les tables legacy selon une strat�gie de sauvegarde et de compatibilit� test�e ; d�ployer apr�s validation sur copie de base et proc�dure de restauration.

## Chiffres de cl�ture

- **Fichiers pertinents d�pendant encore du syst�me Snapshot legacy : 34** � 25 fichiers de production avec r�f�rences concr�tes, 7 fichiers de tests, 2 fichiers de compatibilit� indirecte (initialiseur et m�tadonn�es de baseline).
- **Fichiers class�s G, candidats supprimables : 4** � deux anciens services et deux anciens graphiques list�s ci-dessus ; suppression diff�r�e et non effectu�e.
- **Fichiers de sch�ma/compatibilit� n�cessitant une migration pr�alable : 13** � `MkddDbContext`, `MkddDatabaseSchemaInspector`, quatre entit�s/navigations (`MkddSnapshot`, `MkddPerformanceEntity`, `MkddCourse`, `MkddPlayer`), les quatre artefacts EF (`InitialCreate.cs`, son `.Designer.cs`, `AddHistoricalYahooImport.Designer.cs`, `MkddDbContextModelSnapshot.cs`), `historical-import-migration.sql`, `MkddDatabaseInitializer.cs` et `MkddDatabaseMigrationMetadata.cs`.
- **Pages UI encore d�pendantes : 3** � `/`, `/database-check`, `/import`.
- **Tests r�ussis : 80 exactement**.
- **Ordre recommand� :** dashboard ? import moderne ? anciens services Evolution et graphiques ? DI/code mort ? tests legacy ? entit�s/DbSet ? migration DB finale.

- **R�f�rences textuelles Snapshot restantes dans les fichiers C#/Razor source : 371 occurrences** apr�s le retrait des quatre candidats G et du test d�di� supprim�. Ce chiffre est une mesure de contr�le textuelle, pas un d�compte de d�pendances activ�es; les usages runtime sont document�s dans les sections pr�c�dentes.
- **Tests pertinents restants : 104 r�ussis, 0 �chec, 0 ignor�.**
- **Avertissements de build restants : 3 avertissements pr�existants** (duplicate using CS0105 dans un test; 2 recommandations analytiques xUnit2013). Aucun avertissement nouveau li� au nettoyage.

## Cleanup Step 2 � Legacy Evolution Engine

### S�curisation DI

La configuration des services du projet Web est d�sormais centralis�e dans `MkddWebServiceRegistration.AddMkddWebServices`. `MKDDTracker.Web/Program.cs` utilise cette m�me m�thode; il n�existe plus de seconde configuration DI parall�le.

Le test `MkddWebServiceRegistrationTests.CriticalHistoricalServices_CanBeResolved` v�rifie explicitement la r�solution de:

- `MkddHistoricalModernImportService`
- `MkddHistoricalDatabaseCheckService`
- `MkddHistoricalDashboardService`
- `MkddHistoricalRankingService`
- `MkddHistoricalRankingComparisonService`
- `MkddHistoricalRankingSelectionService`
- `MkddHistoricalPlayerTimelineService`
- `MkddHistoricalPlayerProfileService`

La configuration fournit d�sormais aussi `MkddDbContext` comme service scoped en plus du `IDbContextFactory<MkddDbContext>`, afin que les services historiques utilisant directement `MkddDbContext` soient r�solvables.

### Validation Step 2

- Branche: `dev`; aucun commit cr��.
- Tests avant ajout du test DI: 104; apr�s ajout: 105; r�ussi: 105, �chec: 0, ignor�: 0.
- `dotnet build`: r�ussi avec les 3 avertissements pr�existants, aucun avertissement nouveau.
- Dry-run Release: 600 performances analys�es, 200 s�ries, 204 �v�nements PR existants, 0 �v�nement � cr�er, 394 snapshots identiques ignor�s, 2 chronos moins bons ignor�s.
- Aucun mode d��criture, aucune migration EF, aucune modification SQLite.

### Classification finale

| �l�ment | D�cision | Justification |
|---|---|---|
| `MkddEvolutionService` | `SafeToDelete` | D�j� absent du workspace; aucune r�f�rence runtime, Razor, DI, console, migration ou test trouv�e. |
| `MkddPlayerEvolutionService` | `SafeToDelete` | D�j� absent du workspace; ancien moteur Snapshot sans appelant actuel. |
| `MkddEvolutionReportService` | `SafeToDelete` | D�j� absent du workspace; uniquement li� aux anciens rapports/�tapes d��volution. |
| `MkddSnapshotComparisonService` | `LegacyRequired` | Conserv�: utilis� par les tests de compatibilit� legacy et les chemins Snapshot conserv�s. |
| `MkddRankEvolutionChart` | `SafeToDelete` | D�j� absent; `/evolution` utilise uniquement `MkddHistoricalRankEvolutionChart`. |
| `MkddTimeEvolutionChart` | `SafeToDelete` | D�j� absent; `/evolution` utilise uniquement `MkddHistoricalTimeEvolutionChart`. |
| `MkddPlayerEvolution` | `SafeToDelete` | D�j� absent; ancien mod�le Snapshot sans appelant. |
| `MkddCompletePlayerEvolution` | `SafeToDelete` | D�j� absent; exclusif � l�ancien moteur `MkddPlayerEvolutionService`. |
| `MkddPlayerEvolutionStep` | `SafeToDelete` | D�j� absent; exclusif � l�ancien moteur/rendements de rapport. |
| Mod�les exclusifs `MkddPlayerEvolutionPoint`, `MkddPlayerSnapshotComparison`, `MkddPlayerEvolutionReport`, renderer console | `SafeToDelete` | D�j� absents; aucune r�f�rence restante hors documentation historique. |

### �l�ments conserv�s

- `MkddSnapshot`, `MkddPerformanceEntity`, leurs DbSet et les tables SQLite sont conserv�s.
- `MkddDatabaseInitializer`, `MkddDatabaseSchemaInspector`, les migrations EF Core et les scripts SQL sont conserv�s.
- `MkddSnapshotHistoryMigrationService` reste le pont vers `HistoricalPerformances`.
- L�ancien import Snapshot et `/import` restent actifs.
- Les tests historiques, de migration, de sauvegarde/initialisation SQLite et de compatibilit� restent conserv�s.

### Nettoyage effectu�

Aucune inscription DI utile n�a �t� retir�e dans cette �tape hormis celles d�j� absentes avec les anciens services. Les anciens services et mod�les n�apportent aucune r�f�rence actuelle. Aucun ancien composant graphique ni test exclusivement associ� � ces composants ne reste.

### D�pendances Snapshot restantes

Les d�pendances Snapshot restantes sont le dashboard, `/database-check`, `/import`, la compatibilit� SQLite, `MkddSnapshotComparisonService` et `MkddSnapshotHistoryMigrationService` jusqu�� validation finale de la migration de donn�es.

### Prochaine �tape recommand�e

Valider le dry-run de migration, puis moderniser `/import` et le diagnostic/dashboard. Supprimer les entit�s, DbSet et tables Snapshot seulement apr�s rapprochement des donn�es et nouvelle migration EF explicitement planifi�e.

Cleanup Step 4 — Demo History
Modifié
MkddDemoDataSeeder
écrit désormais directement dans HistoricalPerformances;
ne crée plus de MkddSnapshot;
ne crée plus de MkddPerformanceEntity;
crée uniquement le premier PR et les améliorations strictes;
utilise des dates et clés externes déterministes;
utilise SourceSystem = MKDDTrackerDemo;
est idempotent.
Conservés
MkddSnapshotHistoryMigrationService
toujours nécessaire pour migrer les anciennes bases.
Entités et tables Snapshot
toujours nécessaires à la compatibilité des bases legacy.