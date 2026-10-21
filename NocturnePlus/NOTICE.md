# Nocturne Plus – notice

Lanceur pour **Nocturne** (Terminal Reality, 1999), version française Abandonware France.

## Installation

1. Copier `NocturnePlus.exe` dans le dossier du jeu (`C:\Jeux\ABWFR\Nocturne`, à côté de `nocturne.exe`).
2. Ne **jamais** le lancer en administrateur.
3. Si Windows Defender bloque le lancement (règle anti-ransomware ASR), ajouter une exclusion ASR,
   dans un PowerShell administrateur :

   ```powershell
   Add-MpPreference -AttackSurfaceReductionOnlyExclusions "C:\Jeux\ABWFR\Nocturne\NocturnePlus.exe"
   ```

Au premier lancement, le lanceur crée `_NocturnePlus\` dans le dossier du jeu :

| Dossier / fichier | Contenu |
|---|---|
| `journal.txt` | tout ce que fait le lanceur, et les gels/erreurs exacts du jeu |
| `NocturnePlus.ini` | vos réglages |
| `sauvegardes\<date>\` | copie des fichiers touchés, avant chaque lancement |
| `originaux\nocturne.exe` | l'exécutable d'origine, conservé avant tout correctif |
| `wrappers\` | réserve de cnc-ddraw et dgVoodoo 2 |

Rien n'est jamais supprimé : les DLL sont rangées par renommage (`ddraw.dll.cnc`, `ddraw.dll.dgv`…).

## Les modes

**Ultra HD (recommandé)** : vraie 3D matérielle via dgVoodoo 2 (Direct3D 11). La 3D est calculée à la
hauteur de votre écran (1920x1440 sur un 2560x1440, en 4:3 avec bandes noires), filtrage anisotrope
16x, 60 images/s, V-Sync. Menus et texte restent dessinés en 640x480 par le moteur.

**Stable** : rendu logiciel 640x480 agrandi par cnc-ddraw (filtre lanczos2-sharp). Le mode de secours.

## Réglages conseillés (interface du lanceur en anglais)

- Display : **Borderless window** (Alt+Tab fonctionne).
- Frame rate limit : **60** (la cape et les vêtements dépendent de la cadence).
- Advanced → Processor : **Game on one core, rendering free** (recommandé). « No restriction » fonctionne
  aussi avec le correctif d'horloge (voir le rapport).
- Advanced → patches : **Precise engine clock** et **No false low-memory warning** cochés (activés par défaut).
- 16:9 natif : Display → Game resolution **854x480**. Le lanceur applique alors seul le correctif caméra
  large ; revenir à 640x480 pour le 4:3 d'origine.

- Graphics → **AI-remastered backgrounds and HD textures** : décors nettoyés par IA et textures des
  personnages/objets agrandies en 256x256 (pack dans `_NocturnePlus\hd`).
  Décocher pour revenir aux décors d'origine ; les archives .POD ne sont jamais modifiées.

## Boutons

- **Launch** : prépare le dossier (wrapper, .ini, correctifs), lance le jeu au premier plan et le surveille.
- **Repair** : remet la configuration connue-bonne (mode Stable, cnc-ddraw, exécutable d'origine).
- **Release mouse/screen** : rend la souris et la résolution du bureau si le jeu les a gardées.

## En cas de problème

- Le jeu affiche le bureau au lieu de l'image : quittez-le et relancez ; vérifiez que le mode est
  « Borderless window ».
- Le jeu gèle : le garde-fou note dans `journal.txt` où chaque fil est bloqué, puis l'arrête (60 s par
  défaut). Envoyez ce journal pour diagnostic.
- Après un arrêt forcé, si DirectDraw refuse de démarrer (erreurs `wddvmem.cpp`) : redémarrez le PC.
- Pour revenir à l'état d'origine : bouton **Repair**.

## Mises à jour

En haut à droite, le lanceur affiche sa version et l'état de mise à jour :
- **● Up to date** : rien à faire ;
- **● Update available: vX.Y.Z (open)** : cliquer ouvre la page de la version sur GitHub (rien n'est
  téléchargé automatiquement) ;
- **○ Update check unavailable** : pas de connexion, ou aucune version publiée sur le dépôt.

Le dépôt vérifié se règle dans Advanced → Update repository (par défaut `skw-1337/nocturne-plus`).

**Publier une version** : modifier `AssemblyVersion` dans `src\Version.cs` (ex. `1.1.0.0`), compiler,
puis créer sur GitHub une *release* avec le tag `v1.1.0` et `NocturnePlus.exe` en pièce jointe. Les
lanceurs plus anciens afficheront alors « Update available ».

## Compilation

`compiler.bat` (csc de .NET Framework 4 fourni avec Windows, x86). Sources dans `src\`.
