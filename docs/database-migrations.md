# Cycle de vie SQLite et migrations EF Core

MKDDTracker conserve la base utilisateur dans `%LOCALAPPDATA%\MKDDTracker\mkddtracker.db` et la base de démonstration dans `%LOCALAPPDATA%\MKDDTracker\mkddtracker-demo.db`.

## Démarrage des applications

Les applications Web et Console doivent toujours passer par `MkddDatabaseInitializer` :

- une base absente est créée avec `MigrateAsync` ;
- une base PR4 sans `__EFMigrationsHistory` est vérifiée, sauvegardée (base utilisateur), puis baselinée sur `InitialCreate` ;
- une base moderne applique uniquement les migrations en attente ;
- une base utilisateur est sauvegardée avant une baseline ou une migration ;
- une base déjà à jour ne produit aucune sauvegarde ;
- un schéma inconnu bloque le démarrage et n'est jamais supprimé.

Les sauvegardes cohérentes sont créées avec l'API SQLite dans le même dossier que la base, sous la forme :

```text
mkddtracker.backup-yyyyMMdd-HHmmss.db
```

La base de démonstration n'est pas sauvegardée. `--reset-demo` supprime la base et ses fichiers `-wal`, `-shm` et `-journal`, puis la recrée via les migrations avant le seed.

## Outil EF Core

Le SDK attendu est défini par `MKDDTracker/global.json`. Installer ou mettre à jour l'outil .NET 9 :

```powershell
dotnet tool install --global dotnet-ef --version 9.*
# ou
dotnet tool update --global dotnet-ef --version 9.*
```

Vérifier la factory de conception :

```powershell
dotnet ef dbcontext info `
  --project ".\MKDDTracker\MKDDTracker.csproj"
```

## Créer une migration

Depuis la racine du dépôt :

```powershell
dotnet ef migrations add NomMigration `
  --project ".\MKDDTracker\MKDDTracker.csproj" `
  --startup-project ".\MKDDTracker\MKDDTracker.Web\MKDDTracker.Web.csproj" `
  --output-dir "Data\Database\Migrations"
```

La migration, son fichier `Designer` et `MkddDbContextModelSnapshot` doivent être versionnés ensemble.

## Lister et appliquer les migrations

```powershell
dotnet ef migrations list `
  --project ".\MKDDTracker\MKDDTracker.csproj" `
  --startup-project ".\MKDDTracker\MKDDTracker.Web\MKDDTracker.Web.csproj"

dotnet ef database update `
  --project ".\MKDDTracker\MKDDTracker.csproj" `
  --startup-project ".\MKDDTracker\MKDDTracker.Web\MKDDTracker.Web.csproj"
```

## Générer un script SQL

SQLite ne prend pas en charge les scripts de migration idempotents EF Core. Générer plutôt un script borné, puis le relire avant exécution :

```powershell
dotnet ef migrations script 0 `
  --project ".\MKDDTracker\MKDDTracker.csproj" `
  --startup-project ".\MKDDTracker\MKDDTracker.Web\MKDDTracker.Web.csproj"
```

## Règle d'équipe

Toute évolution du modèle EF qui modifie le schéma doit inclure :

1. la migration ;
2. le snapshot EF mis à jour ;
3. un test d'évolution ;
4. un test de conservation des données ;
5. une validation sur une copie de base utilisateur, jamais sur l'unique original.

## Validation PR5

```powershell
dotnet test ".\MKDDTracker.Tests\MKDDTracker.Tests.csproj"
dotnet build ".\MKDDTracker\MKDDTracker.sln"
git diff --check
git status
```

Aucun fichier `.db`, `-wal`, `-shm`, `-journal`, `bin` ou `obj` ne doit être ajouté au dépôt.
