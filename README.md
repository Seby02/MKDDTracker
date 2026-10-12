# 🏎️ MKDD Tracker

**Track the records. Understand the progression. Preserve the history.**

*Suivre les records. Comprendre la progression. Préserver l'histoire.*

🇫🇷 [Français](#-français) | 🇬🇧 [English](#-english)

---

# 🇫🇷 Français

## 🎮 Présentation

**MKDD Tracker** est un projet open source développé en **C# / .NET** côté back et en Blazor côté Front, dédié à l'analyse et à l'historisation des performances Time Trial de **Mario Kart: Double Dash!!**

Le projet s'appuie sur les données publiques de la [Mario Kart: Double Dash!! Players' Page](https://www.mariokart64.com/mkdd/), une référence pour les classements et les records de la communauté.

**L'objectif : transformer des classements statiques en une histoire vivante de la compétition.**

## 🌍 Pourquoi MKDD Tracker ?

Depuis plus de vingt ans, des joueurs du monde entier repoussent les limites de Mario Kart: Double Dash!!

Chaque semaine, des records sont améliorés, des positions changent et de nouvelles rivalités apparaissent.

Le site communautaire permet de consulter les classements généraux, les records sur les 16 circuits, les meilleurs tours, les profils des joueurs et les dates des performances.

Mais il reste difficile de répondre à certaines questions :

- Quelle était la position d'un joueur il y a trois mois ?
- Combien de places a-t-il gagnées ou perdues ?
- Quels circuits ont contribué à sa progression ?
- Comment ses records personnels ont-ils évolué ?
- Quels joueurs ont le plus progressé cette semaine ?
- Comment comparer deux périodes de compétition ?

**MKDD Tracker a pour objectif de rendre ces informations accessibles, compréhensibles et visuelles.**

## 🎯 La vision

MKDD Tracker ne se limite pas à reproduire les classements existants.

Il cherche à leur donner une dimension historique.

En conservant régulièrement l'état des classements, le projet pourra mesurer les progressions, analyser les changements et raconter l'évolution de la communauté.

Chaque performance représente des heures d'entraînement.

Chaque position gagnée marque une étape.

Chaque classement constitue une photographie de la compétition à un instant donné.

**En reliant ces photographies, MKDD Tracker souhaite construire une véritable mémoire historique du Time Trial de Mario Kart: Double Dash!!**

## 🚀 Fonctionnalités prévues

### 👤 Profils des joueurs

- Classement général et Average Finish (AF).
- Performances sur les 16 circuits.
- Meilleurs tours individuels (Fastest Laps).
- Rangs, temps et standards.
- Dates des records personnels.
- Liens vers les vidéos disponibles.
- Historique des performances.

### 📈 Suivi de progression

- Évolution du classement général.
- Places gagnées ou perdues.
- Progression par circuit.
- Amélioration des records personnels.
- Comparaison entre deux dates.
- Graphiques d'évolution.

### 🏁 Analyse par circuit

- Classements des courses complètes et des tours individuels.
- Comparaison entre joueurs.
- Évolution des positions.
- Écarts entre les performances.
- Historique des records.

### 📅 Snapshots historiques

Le système conservera périodiquement une copie de l'état des classements.

Ces **snapshots** permettront de comparer différentes dates et de calculer les évolutions.

Une distinction essentielle sera conservée :

- **RecordDate** : date à laquelle le joueur a réalisé sa performance.
- **SnapshotDate** : date à laquelle MKDD Tracker a observé cette performance.

L'historique sera construit progressivement à partir du début de la collecte. Les périodes antérieures pourront être étudiées à partir des archives disponibles.

### 🤖 Bot et commandes

À terme, un bot pourrait permettre d'interroger directement les statistiques :

```text
/mkdd joueur "Mattilde F"
/mkdd semaine "Mattilde F"
/mkdd course "Luigi Circuit" "Mattilde F"
/mkdd progression "Mattilde F"
```

### 🏆 Statistiques communautaires

- Plus fortes progressions hebdomadaires.
- Joueurs les plus actifs.
- Records personnels récemment améliorés.
- Circuits les plus disputés.
- Évolution des rivalités.
- Tendances mensuelles et annuelles.

## 🛠️ Technologies

| Technologie | Utilisation |
|---|---|
| C# / .NET | Développement principal |
| HtmlAgilityPack | Parsing HTML |
| HttpClient | Collecte des pages publiques |
| Entity Framework Core | Accès aux données (prévu) |
| SQLite | Stockage initial (prévu) |
| ASP.NET Core (Blazor) | API et interface web (prévu) |
| xUnit | Tests automatisés (prévu) |

## 🏗️ Architecture actuelle

MKDD Tracker repose actuellement sur une architecture .NET organisée autour de la collecte des données, de leur persistance, de leur analyse et de leur visualisation.

```text
MKDDTracker/
│
├── MKDDTracker.sln
├── MKDDTracker.csproj
├── Program.cs
│
├── Data/
│   ├── Database/
│   │   └── MkddDbContext.cs
│   └── Entities/
│       ├── MkddCourse.cs
│       ├── MkddPlayer.cs
│       ├── MkddPerformanceEntity.cs
│       └── MkddSnapshot.cs
│
├── Scraper/
│   ├── Models/
│   │   ├── MkddPerformance.cs
│   │   ├── MkddPlayerEvolution.cs
│   │   ├── MkddPlayerSnapshotComparison.cs
│   │   ├── MkddRankMovementAnalysis.cs
│   │   └── Evolution/
│   │       └── ...
│   └── Parsing/
│       └── MkddCoursePageParser.cs
│
├── Services/
│   ├── MkddTestDataSeeder.cs
│   ├── EvolutionService/
│   │   ├── MkddEvolutionService.cs
│   │   ├── MkddPlayerEvolutionService.cs
│   │   ├── MkddEvolutionReportService.cs
│   │   └── MkddEvolutionConsoleRenderer.cs
│   └── SnapshotService/
│       ├── MkddSnapshotService.cs
│       ├── MkddSnapshotImportService.cs
│       └── MkddSnapshotComparisonService.cs
│
├── MKDDTracker.Web/
│   ├── MKDDTracker.Web.csproj
│   ├── Program.cs
│   ├── Components/
│   │   ├── App.razor
│   │   ├── Routes.razor
│   │   ├── Layout/
│   │   └── Pages/
│   │       ├── Home.razor
│   │       ├── Ranking.razor
│   │       ├── Evolution.razor
│   │       ├── Import.razor
│   │       └── DatabaseCheck.razor
│   └── wwwroot/
│       └── app.css
│
└── Samples/
    └── luigi-circuit.html
```

### Organisation des composants

| Composant | Responsabilité |
|---|---|
| **Data** | Modèles de persistance et contexte Entity Framework Core |
| **Scraper** | Parsing des classements HTML et modèles d'analyse |
| **Services / SnapshotService** | Import, conservation et comparaison des snapshots |
| **Services / EvolutionService** | Calcul et restitution des évolutions des joueurs |
| **MKDDTracker.Web** | Interface web Blazor et navigation entre les fonctionnalités |
| **Samples** | Fichiers HTML locaux pour le développement et les tests |

L'architecture actuelle constitue une première séparation des responsabilités. Elle pourra évoluer vers des projets distincts (`Domain`, `Application`, `Infrastructure`) lorsque la complexité du code le justifiera.

### Fonctionnement actuel et évolution prévue

```text
          MKDD Players' Page
                   │
                   ▼
          Fichiers HTML locaux
                   │
                   ▼
        MkddCoursePageParser
                   │
                   ▼
           Modèles métier
                   │
                   ▼
        Entity Framework Core
                   │
                   ▼
             SQLite
                   │
                   ▼
          Snapshots historiques
                   │
                   ▼
      Services d'analyse et d'évolution
                   │
           ┌───────┴────────┐
           ▼                ▼
      Console .NET     Application Blazor
                            │
                            ▼
                 Classements / Évolution
                            │
                            ▼
                   Futures intégrations
                    API REST / Discord
```

La collecte directe depuis le site, l'automatisation des snapshots et les intégrations externes font partie des évolutions envisagées.

---

## 🗺️ Roadmap

### Phase 1 — Fondations et parsing HTML

- [x] Initialisation de la solution .NET.
- [x] Intégration de HtmlAgilityPack.
- [x] Création des modèles de performances.
- [x] Analyse du HTML de Luigi Circuit.
- [x] Création du parser `MkddCoursePageParser`.
- [x] Mise en place de fichiers HTML locaux pour le développement.
- [x] Validation initiale du parsing sur plusieurs circuits (Luigi Circuit et Peach Beach).
- [ ] Validation complète du parsing sur plusieurs structures de pages HTML.
- [ ] Gestion robuste des variations HTML et des erreurs.
- [ ] Tests automatisés du parser.

### Phase 2 — Collecte des performances

- [x] Mise en place des premiers modèles de joueurs et de circuits.
- [x] Structure initiale pour représenter les performances.
- [x] Prise en charge initiale de plusieurs circuits.
- [x] Importation des 100 premières performances de Luigi Circuit et Peach Beach.
- [ ] Prise en charge complète des 16 circuits.
- [ ] Gestion des courses complètes et des tours individuels.
- [ ] Gestion des classements Any Hz, 50 Hz et 60 Hz.
- [ ] Gestion de la pagination.
- [ ] Collecte des profils des joueurs.
- [ ] Collecte des classements généraux.
- [ ] Automatisation responsable de la collecte, sous réserve des autorisations du site.

### Phase 3 — Persistance et snapshots historiques

- [x] Intégration d'Entity Framework Core.
- [x] Intégration de SQLite.
- [x] Création du contexte `MkddDbContext`.
- [x] Définition des entités `Player`, `Course`, `Performance` et `Snapshot`.
- [x] Création des services de gestion des snapshots.
- [x] Création d'un service d'import des snapshots.
- [x] Création d'un service de comparaison des snapshots.
- [x] Validation du stockage et de la récupération des performances.
- [x] Validation de la persistance de plusieurs snapshots pour un même circuit.
- [x] Vérification des données enregistrées via une page Blazor dédiée.
- [ ] Validation des comparaisons sur plusieurs snapshots réels.
- [ ] Planification de snapshots périodiques.
- [ ] Gestion complète des doublons et de l'intégrité historique.

### Phase 4 — Analyse et évolution des joueurs

- [x] Création des modèles d'évolution des joueurs.
- [x] Création des services d'analyse de progression.
- [x] Création des modèles d'analyse des mouvements de classement.
- [x] Création d'un service de génération de rapports d'évolution.
- [x] Création d'un moteur de restitution des résultats en console.
- [x] Implémentation de l'analyse des gains et pertes de positions.
- [x] Distinction entre les positions gagnées par amélioration personnelle et les mouvements des autres joueurs.
- [x] Création d'un graphique d'évolution du classement.
- [x] Création d'un graphique d'évolution des chronos.
- [x] Analyse de l'évolution sur une période sélectionnée.
- [ ] Validation des calculs sur plusieurs snapshots réels.
- [ ] Comparaison entre plusieurs joueurs.
- [ ] Statistiques individuelles et communautaires avancées.

### Phase 5 — Interface web et visualisation

- [x] Création du projet `MKDDTracker.Web`.
- [x] Mise en place de Blazor Interactive Server.
- [x] Connexion de l'application web à la base SQLite existante.
- [x] Création de la page d'accueil.
- [x] Création de la page des classements (`/classements`).
- [x] Création de la page d'évolution (`/evolution`).
- [x] Création de la page d'import (`/import`).
- [x] Création d'une page de vérification de la base de données (`/database-check`).
- [x] Validation initiale des pages avec des classements HTML réels.
- [x] Ajout de la sélection dynamique des joueurs et des circuits.
- [x] Ajout de la recherche de joueurs dans les classements.
- [x] Consultation des classements par circuit et par date.
- [x] Création de graphiques d'évolution du classement et des chronos.
- [x] Création d'un tableau de bord avec statistiques principales.
- [x] Affichage des derniers snapshots sur le tableau de bord.
- [x] Ajout de raccourcis vers les fonctionnalités principales.
- [ ] Création d'une page de recherche et de consultation des profils des joueurs.
- [ ] Amélioration du design et de l'expérience utilisateur.
- [ ] Ajout de statistiques avancées au tableau de bord.
- [ ] Ajout d'interactions avancées aux graphiques (infobulles, zoom, etc.).
- [ ] Tests fonctionnels et de robustesse approfondis.

### Phase 6 — Automatisation et intégrations

- [ ] Automatisation des imports et snapshots.
- [ ] Gestion des tâches planifiées.
- [ ] API REST publique.
- [ ] Bot Discord.
- [ ] Rapports hebdomadaires automatiques.
- [ ] Notifications des progressions importantes.
- [ ] Déploiement de l'application web.

## ⚙️ Installation

### Prérequis

- SDK .NET 9.
- Visual Studio, Rider ou VS Code.
- Git.

### Compilation

```bash
git clone <REPOSITORY_URL>
cd MKDDTracker
dotnet restore
dotnet build
```

### Exécution du scraper

```bash
dotnet run --project MKDDTracker.Scraper
```

Pendant la phase initiale, le parser peut utiliser des fichiers HTML locaux dans `Samples`, ce qui permet de développer et tester sans accès réseau.

## 🤝 Contributions

Les idées, suggestions, corrections et contributions seront les bienvenues à mesure que le projet évoluera.

MKDD Tracker est développé avec l'ambition de fournir des outils utiles aux passionnés de Mario Kart: Double Dash!! Time Trial.

## ⚖️ Avertissement

MKDD Tracker est un projet indépendant, non officiel et non affilié à Nintendo ni aux administrateurs de la MKDD Players' Page.

Les données proviennent des pages publiques de [mariokart64.com](https://www.mariokart64.com/mkdd/). Leur collecte devra respecter les conditions d'utilisation, les restrictions d'accès et les ressources du site source.

## ❤️ Pour la communauté

Mario Kart: Double Dash!! est sorti en 2003.

Plus de vingt ans après, sa communauté Time Trial continue de faire vivre le jeu et de repousser ses limites.

Derrière chaque record se trouvent des joueurs, des heures d'entraînement et des histoires de progression.

**MKDD Tracker existe pour rendre ces histoires visibles et préserver leur mémoire.**

> **Parce qu'un classement montre où l'on est, mais que l'histoire montre le chemin parcouru.** 🏁

---

# 🇬🇧 English

## 🎮 Overview

**MKDD Tracker** is an open-source project built with **C# / .NET**, dedicated to tracking, analyzing, and preserving the history of **Mario Kart: Double Dash!! Time Trial** performances.

The project uses publicly available data from the [Mario Kart: Double Dash!! Players' Page](https://www.mariokart64.com/mkdd/), a community resource for rankings and records.

**Our mission is to transform static leaderboards into a living history of competitive progression.**

## 🌍 Why MKDD Tracker?

For more than twenty years, players around the world have continued pushing the limits of Mario Kart: Double Dash!!

Records are broken, rankings change, personal bests improve, and rivalries evolve.

The community website already provides valuable information, including overall rankings, course records, fastest laps, player profiles, and performance dates.

However, answering historical questions is not always straightforward:

- Where was a player ranked three months ago?
- How many positions have they gained or lost?
- Which courses contributed to their improvement?
- How have their personal records evolved?
- Who made the biggest progress this week?
- How can we compare two different competitive periods?

**MKDD Tracker aims to make these insights accessible, understandable, and visual.**

## 🎯 Our Vision

MKDD Tracker is not intended to simply reproduce existing leaderboards.

Its purpose is to add a historical dimension to them.

By regularly recording leaderboard snapshots, the project will be able to measure progress, analyze ranking changes, and highlight the evolution of the competitive community.

Every performance represents hours of practice.

Every position gained marks another milestone.

Every leaderboard captures a moment in competitive history.

**By connecting these moments, MKDD Tracker aims to build a lasting historical memory of the Mario Kart: Double Dash!! Time Trial community.**

## 🚀 Planned Features

### 👤 Player Profiles

- Overall rankings and Average Finish (AF).
- Performances across all 16 courses.
- Fastest Lap records.
- Rankings, times, and standards.
- Personal record dates.
- Available video links.
- Historical performance data.

### 📈 Progress Tracking

- Overall ranking evolution.
- Positions gained or lost.
- Course-by-course progression.
- Personal best improvements.
- Comparisons between two dates.
- Interactive progression charts.

### 🏁 Course Analysis

- Full-course and fastest-lap leaderboards.
- Player comparisons.
- Ranking evolution.
- Time differences between competitors.
- Historical record analysis.

### 📅 Historical Snapshots

The system will periodically capture the state of the leaderboards.

These **snapshots** will make it possible to compare rankings across different dates and calculate changes over time.

Two dates must be distinguished:

- **RecordDate**: when the player achieved a performance.
- **SnapshotDate**: when MKDD Tracker observed that performance in the leaderboard.

Historical tracking will begin when data collection starts. Earlier periods may be reconstructed where suitable archives are available.

### 🤖 Bot Commands

A future bot integration could provide direct access to player statistics.

Example commands:

```text
/mkdd player "Mattilde F"
/mkdd week "Mattilde F"
/mkdd course "Luigi Circuit" "Mattilde F"
/mkdd progress "Mattilde F"
```

### 🏆 Community Statistics

- Biggest weekly ranking improvements.
- Most active players.
- Recently improved personal bests.
- Most competitive courses.
- Evolving rivalries.
- Monthly and yearly trends.

## 🛠️ Tech Stack

| Technology | Purpose |
|---|---|
| C# / .NET | Main development platform |
| HtmlAgilityPack | HTML parsing |
| HttpClient | Public page retrieval |
| Entity Framework Core | Data access (planned) |
| SQLite | Initial database (planned) |
| ASP.NET Core | API and web interface (planned) |
| xUnit | Automated testing (planned) |

## 🏗️ Current Architecture

MKDD Tracker currently uses a .NET architecture organized around data collection, persistence, analysis, and visualization.

The project structure is shared with the French section above.

### Component Responsibilities

| Component | Responsibility |
|---|---|
| **Data** | Persistence entities and Entity Framework Core database context |
| **Scraper** | HTML leaderboard parsing and analysis models |
| **Services / SnapshotService** | Snapshot import, storage, and comparison |
| **Services / EvolutionService** | Player progression calculations and reporting |
| **MKDDTracker.Web** | Blazor web interface and feature navigation |
| **Samples** | Local HTML files used for development and testing |

The current architecture provides an initial separation of responsibilities. It may evolve into dedicated `Domain`, `Application`, and `Infrastructure` projects as the codebase grows.

### Data Processing Flow

```text
          MKDD Players' Page
                   │
                   ▼
           Local HTML Files
                   │
                   ▼
        MkddCoursePageParser
                   │
                   ▼
             Domain Models
                   │
                   ▼
        Entity Framework Core
                   │
                   ▼
               SQLite
                   │
                   ▼
          Historical Snapshots
                   │
                   ▼
       Evolution & Analysis Services
                   │
           ┌───────┴────────┐
           ▼                ▼
        .NET Console     Blazor Web App
                            │
                            ▼
                  Rankings / Evolution
                            │
                            ▼
                   Future Integrations
                    REST API / Discord
```

Direct website collection, automated snapshots, and external integrations are planned future improvements.

---

## 🗺️ Roadmap

### Phase 1 — Foundations and HTML Parsing

- [x] Initialize the .NET solution.
- [x] Integrate HtmlAgilityPack.
- [x] Create performance models.
- [x] Analyze Luigi Circuit HTML.
- [x] Implement `MkddCoursePageParser`.
- [x] Set up local HTML samples for development.
- [x] Perform initial parsing validation across multiple courses (Luigi Circuit and Peach Beach).
- [ ] Fully validate parsing across different HTML page structures.
- [ ] Improve handling of HTML variations and errors.
- [ ] Add automated parser tests.

### Phase 2 — Performance Collection

- [x] Create initial player and course models.
- [x] Establish the performance data structure.
- [x] Add initial support for multiple courses.
- [x] Import the top 100 performances from Luigi Circuit and Peach Beach.
- [ ] Support all 16 courses.
- [ ] Support full-course and fastest-lap records.
- [ ] Support Any Hz, 50 Hz, and 60 Hz rankings.
- [ ] Implement pagination.
- [ ] Collect player profiles.
- [ ] Collect overall rankings.
- [ ] Implement responsible automated collection, subject to website permissions.

### Phase 3 — Persistence and Historical Snapshots

- [x] Integrate Entity Framework Core.
- [x] Integrate SQLite.
- [x] Create `MkddDbContext`.
- [x] Define `Player`, `Course`, `Performance`, and `Snapshot` entities.
- [x] Implement snapshot management services.
- [x] Implement snapshot import services.
- [x] Implement snapshot comparison services.
- [x] Validate performance storage and retrieval.
- [x] Validate persistence of multiple snapshots for a single course.
- [x] Verify stored data through a dedicated Blazor page.
- [ ] Validate comparisons across multiple real-world snapshots.
- [ ] Schedule periodic snapshots.
- [ ] Fully handle duplicates and historical data integrity.

### Phase 4 — Player Progression and Analytics

- [x] Create player evolution models.
- [x] Implement progression analysis services.
- [x] Create ranking movement analysis models.
- [x] Implement evolution reporting services.
- [x] Create console-based evolution report rendering.
- [x] Implement ranking gain and loss analysis.
- [x] Distinguish positions gained through personal improvement from other players' movements.
- [x] Create a ranking progression chart.
- [x] Create a time progression chart.
- [x] Support progression analysis over a selected date range.
- [ ] Validate calculations across multiple real-world snapshots.
- [ ] Compare multiple players.
- [ ] Develop advanced individual and community statistics.

### Phase 5 — Web Interface and Visualization

- [x] Create the `MKDDTracker.Web` project.
- [x] Set up Blazor Interactive Server.
- [x] Connect the web application to the existing SQLite database.
- [x] Create the home page.
- [x] Create the rankings page (`/classements`).
- [x] Create the progression page (`/evolution`).
- [x] Create the import page (`/import`).
- [x] Create a database verification page (`/database-check`).
- [x] Perform initial page validation using real HTML rankings.
- [x] Add dynamic player and course selection.
- [x] Add player search within rankings.
- [x] Browse rankings by course and snapshot date.
- [x] Create ranking and time progression charts.
- [x] Build a dashboard with key statistics.
- [x] Display the latest snapshots on the dashboard.
- [x] Add quick navigation to the main features.
- [ ] Create player profile search and browsing pages.
- [ ] Improve the application's design and user experience.
- [ ] Add advanced dashboard statistics.
- [ ] Add advanced chart interactions (tooltips, zoom, etc.).
- [ ] Perform comprehensive functional and robustness testing.

### Phase 6 — Automation and Integrations

- [ ] Automate imports and snapshots.
- [ ] Implement scheduled background jobs.
- [ ] Create a public REST API.
- [ ] Develop a Discord bot.
- [ ] Generate automated weekly reports.
- [ ] Add notifications for significant ranking changes.
- [ ] Deploy the web application.

## ⚙️ Getting Started

### Requirements

- .NET 9 SDK.
- Visual Studio, Rider, or VS Code.
- Git.

### Build

```bash
git clone <REPOSITORY_URL>
cd MKDDTracker
dotnet restore
dotnet build
```

### Run the Scraper

```bash
dotnet run --project MKDDTracker.Scraper
```

During early development, the parser can operate on local HTML files stored in `Samples`, allowing offline development and testing.

## 🤝 Contributing

Ideas, suggestions, bug reports, and contributions are welcome as the project evolves.

MKDD Tracker is being developed with the goal of providing useful tools for the Mario Kart: Double Dash!! Time Trial community.

## ⚖️ Disclaimer

MKDD Tracker is an independent, unofficial project and is not affiliated with Nintendo or the administrators of the MKDD Players' Page.

Data originates from publicly accessible pages on [mariokart64.com](https://www.mariokart64.com/mkdd/). Automated collection must respect the source website's terms, access restrictions, and server resources.

## ❤️ Built for the Community

Mario Kart: Double Dash!! was released in 2003.

More than twenty years later, its Time Trial community continues to keep the game alive and push its limits.

Behind every record are players, countless hours of practice, and stories of improvement.

**MKDD Tracker exists to make those stories visible and preserve their history.**

> **A leaderboard shows where you stand. History shows how far you've come.** 🏁
