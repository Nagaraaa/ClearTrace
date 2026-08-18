# ClearTrace

Un désinstalleur Windows libre, transparent et prudent. ClearTrace lance l’outil officiel de désinstallation du logiciel, conserve un journal local lisible et affiche des traces candidates sans jamais les supprimer automatiquement.

## MVP v0.1.0

- inventaire depuis les emplacements Windows 64 bits, 32 bits et utilisateur ;
- recherche par nom ou éditeur ;
- lancement confirmé de la commande de désinstallation officielle ;
- scan prudent de l’emplacement d’installation et des dossiers AppData portant exactement le nom du logiciel ;
- journal local JSONL : `%LOCALAPPDATA%\\ClearTrace\\audit.jsonl`.

## Limites assumées

Cette version ne supprime aucune trace, ne modifie pas le Registre et ne prétend pas distinguer avec certitude une donnée partagée d’une donnée propre à une application. C’est un choix de sécurité : l’étape suivante sera une corbeille/quarantaine avec prévisualisation et score de confiance.

## Compiler

```powershell
dotnet publish .\\ClearTrace\\ClearTrace.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

L’exécutable est produit dans `ClearTrace\\bin\\Release\\net8.0-windows\\win-x64\\publish\\ClearTrace.exe`.

## Licence

MIT. Contributions bienvenues.
