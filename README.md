# CaisseQuartier

Application .NET MAUI de gestion partagée de la caisse d'une association de quartier au Cameroun. Les montants sont enregistrés en FCFA. L'application permet de définir un projet commun, de gérer les membres et leurs profils, d'enregistrer cotisations et dépenses, de consulter les activités et d'exporter des rapports PDF.

## Fonctionnalités

- Création d'une association par son premier administrateur, avec objectif collectif, montant mensuel cible et code d'invitation.
- Inscription autonome avec un code d'association, ou activation d'un profil préparé par l'administrateur avec le même contact.
- Tableau de bord partagé, annuaire et profils modifiables avec photo JPEG/PNG (5 Mo maximum).
- Enregistrement des cotisations, dépenses et événements, avec historique commun aux membres.
- Rapports PDF pour les cotisations, les dépenses ou les événements : jour, semaine, quinzaine, mois, périodes de 2 à 6 mois ou période personnalisée.
- Rapport PDF de présentation du projet en deux pages : objectifs, taux d'avancement estimatif (87,5 %, soit 7 lots sur 8), difficultés, limites et prochaines étapes. Le dernier lot correspond à la recette avec les comptes réels de plusieurs membres.
- Backend ASP.NET Core et stockage SQLite local sur le PC hôte ; aucune donnée de démonstration n'est préchargée.

## Lancer le serveur sur le PC

Installer le SDK .NET 10, ouvrir PowerShell à la racine du projet, puis démarrer l'API :

```powershell
dotnet build .\CaisseQuartier.Api\CaisseQuartier.Api.csproj
if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation de l'API" }

$env:ASPNETCORE_URLS = "http://0.0.0.0:5080"
dotnet run --project .\CaisseQuartier.Api\CaisseQuartier.Api.csproj --no-launch-profile
```

Garder cette fenêtre ouverte pendant l'utilisation. Vérifier que le serveur répond depuis le PC :

```powershell
Invoke-RestMethod http://127.0.0.1:5080/health
```

La réponse attendue contient `"status":"ok"`. L'adresse du serveur doit être l'adresse IPv4 de la carte Wi-Fi du PC, pas une adresse VMware ou Tailscale :

```powershell
Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias "Wi-Fi" |
    Where-Object { $_.IPAddress -notlike "169.254*" } |
    Select-Object -ExpandProperty IPAddress
```

Pour autoriser le téléphone à joindre l'API sur un réseau Windows **privé**, une règle de pare-feu peut être nécessaire. À exécuter dans PowerShell en tant qu'administrateur :

```powershell
New-NetFirewallRule -DisplayName "CaisseQuartier API (Wi-Fi privé)" `
  -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow -Profile Private
```

Ne pas exposer le port sur un réseau public. Cette version utilise HTTP sans chiffrement et convient uniquement à un test sur un réseau local de confiance, pas à un déploiement sur Internet.

## Installer et utiliser sur Android

Le PC et le téléphone doivent être connectés au même Wi-Fi. Activer le débogage USB sur le téléphone, le connecter au PC et accepter la demande d'autorisation. Puis, depuis la racine du projet :

```powershell
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
$adb = Join-Path $env:ANDROID_HOME "platform-tools\adb.exe"
& $adb devices

dotnet build .\CaisseQuartier.csproj -f net10.0-android -c Debug `
  -p:RuntimeIdentifier=android-arm64 `
  -p:AndroidSdkDirectory="$env:ANDROID_HOME"
if ($LASTEXITCODE -ne 0) { throw "Échec du build Android" }

$apk = Join-Path $PWD "bin\Debug\net10.0-android\android-arm64\com.caissequartier.app-Signed.apk"
& $adb install -r $apk
if ($LASTEXITCODE -ne 0) { throw "Échec de l'installation" }
& $adb shell monkey -p com.caissequartier.app 1
```

Dans l'application, saisir l'adresse du PC, par exemple `http://172.20.10.2:5080` si cette adresse est bien celle de son Wi-Fi. Le port `5080` est requis. Le téléphone doit rester sur le même réseau que le PC et le serveur doit rester lancé.

Au premier démarrage, le premier membre crée l'association en renseignant son nom, le projet collectif et l'objectif mensuel. Il devient administrateur et partage le code d'association avec les autres membres. Les suivants créent leur compte avec ce code. L'administrateur peut aussi préparer un profil à partir d'un contact : la personne devra utiliser ce même contact et le code d'association pour activer le profil.

Après connexion, ouvrir **Rapport** puis **Télécharger le rapport du projet (2 pages)** pour partager ou enregistrer le bilan PDF.

## Données locales

- Base SQLite : `%LOCALAPPDATA%\CaisseQuartier\caissequartier.db`
- Photos de profil : `%LOCALAPPDATA%\CaisseQuartier\uploads`
- Le répertoire peut être personnalisé avec la variable d'environnement `CAISSEQUARTIER_DATA_DIR` avant de démarrer le serveur.

Le serveur local n'est accessible que tant que le PC et l'API fonctionnent. Cette configuration n'est pas un hébergement permanent ni une sauvegarde distante.

## Compiler l'application de bureau

```powershell
dotnet build .\CaisseQuartier.csproj -f net10.0-windows10.0.19041.0
dotnet run --project .\CaisseQuartier.csproj -f net10.0-windows10.0.19041.0
```
