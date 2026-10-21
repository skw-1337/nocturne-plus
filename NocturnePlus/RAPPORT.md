# Nocturne Plus – rapport (21/10/2026)

PC : Windows 11, Ryzen 7 9800X3D (16 fils), RTX 5080, écran principal 2560x1440 à 360 Hz + second écran.
Jeu : Nocturne VF Abandonware France, `nocturne.exe` SHA1 `c0730723…0d89`.

## Ce qui marche

| | Statut | Preuve |
|---|---|---|
| Mode Stable (cnc-ddraw, logiciel 640x480) | OK | lancement au premier plan, 1 min 18 s sans gel |
| Mode Ultra : 3D matérielle | OK | modules chargés : `tridx7.dll`, dgVoodoo `DDraw`/`D3DImm`, `D3D11`, pilote `nvwgf2um.dll` ; `useDirect3D=1` |
| Ultra : résolution | 3D rendue en **1920x1440** (hauteur native, 4:3 + bandes) | `dgVoodoo.conf [DirectX] Resolution = h:1920, v:1440` ; netteté confirmée à l'œil par l'utilisateur |
| Ultra : image affichée | OK en fenêtre sans bordure | fenêtre 2560x1440, non « toujours au-dessus » |
| Alt+Tab | OK | 8 Alt+Tab en multi-cœur sans gel ; retour rapide |
| Cape / vêtements | OK | 60 FPS plafonnés + horloge corrigée ; confirmé à l'œil |
| Menus | fluides | plus de blocage de 5–8 s après le correctif d'horloge |
| Multi-cœur (16 fils, aucune restriction) | OK sur **3 min 01 s** | 0 non-réponse sur la surveillance, jeu quitté par l'utilisateur |
| 16:9 natif | OK | jeu en 854x480 + correctif caméra, 3D en 2560x1440 sans bandes, champ de vision élargi (capture de l'utilisateur) |
| Avertissement « 0.0 Mo d'échange » | supprimé | correctif `memoire`, confirmé par l'utilisateur |

## Les causes trouvées

### 1. Horloge du moteur (cause du gel multi-cœur et des lenteurs) — corrigée
`FUN_00558A30` cumule à chaque appel le temps écoulé depuis l'appel précédent, en unités de
1/1 179 648 s (65536 × 18, l'horloge PIT des PC de 1999), **tronqué** (`frndint` en mode troncature).
Le `QueryPerformanceCounter` de Windows 11 tourne à 10 MHz : un appel en boucle serrée mesure moins
d'une unité, compte 0, et le temps du jeu s'arrête. Les boucles d'attente du moteur (ex. `00403F7C`
au retour de premier plan) tournent alors jusqu'à ce que Windows interrompe le fil.
- En mono-cœur, les autres fils interrompent souvent le jeu → il avance, lentement (menus lents).
- En multi-cœur, rien ne l'interrompt → gel définitif (écran des griffes, ~30 s).

Correctif `horloge` (16 octets, 5 endroits) : temps total depuis l'initialisation, calculé d'un bloc
sur 64 bits (base QPC fixe, `fistp qword`, `mov` au lieu de `add`). Mêmes unités, même rebouclage.

### 2. dgVoodoo étouffé en mono-cœur
Avec tout le processus sur un cœur, le fil principal attend en boucle dans `DDRAW.dll` (dgVoodoo)
des fils de dgVoodoo/D3D11 coincés sur le même cœur : 5,1 s de CPU en 7 s, aucune image présentée.
Mode « Jeu sur un cœur, rendu libre » : fil principal épinglé avant son démarrage, CPU Set par défaut
du processus = ce cœur, fils de rendu (dgVoodoo, DXGI, D3D11, NVIDIA) libérés sur les autres cœurs.

### 3. Affichage dgVoodoo
`AppControlledScreenMode=false` + `FullScreenMode=false` = fenêtre forcée à la taille 0x0 créée par le
jeu → rien d'affiché. Solution : fenêtré forcé avec `WindowedAttributes = borderless, fullscreensize`
(fenêtre sans bordure plein écran, jamais de plein écran exclusif en multi-écrans).

### 4. Alt+Tab
- `nocturne.sdb` (shim ABWFR) applique `IgnoreAltTab` (crochet clavier) : désinstallé par l'utilisateur
  (copie gardée dans `_NocturnePlus\originaux\`, réinstallable avec `sdbinst nocturne.sdb`).
- En perdant le focus, le moteur se met en pause ; le lanceur relance l'activation si Windows rend le
  premier plan sans `WM_ACTIVATEAPP` (drapeau `0x005C1660`).
- En 3D, le moteur détruit/reconstruit tout son rendu à chaque Alt+Tab (`FUN_00553B70/BA0`). Un
  correctif `alttab3d` existe mais n'est **pas activé** : testé avant le correctif d'horloge, il
  n'apportait rien ; avec l'horloge corrigée le retour est déjà rapide.

### 5. Hold buffer (« Unable to lock hold buffer », dcamera.cpp:3627)
Dans `tridx7.dll`, au-delà de 480 lignes, les menus passent par une surface 2D **codée en dur à
640x480** (`0x10003286`), créée seulement si `useHoldBuffer=1`, puis étirée sur l'écran. La 3D,
elle, est déjà rendue en HD par dgVoodoo ; seuls menus/HUD restent en 640x480. Résolution interne
> 640x480 **non testée** (option expérimentale du lanceur).

### 6. 16:9 natif
La caméra du moteur (`FUN_00440010`) plafonne sa hauteur à 480 lignes et calcule sa largeur
`hauteur × 4 / 3` : toujours 640x480. Correctif `camera169` (19 octets à `0x440038`) : largeur =
largeur du jeu × hauteur caméra / hauteur du jeu (640 en 4:3, 854 en 854x480). Le lanceur l'applique
seul quand une résolution large est choisie et le retire en 4:3. 854x480 garde 480 lignes, donc pas
de hold buffer. Les bandes noires vues en 16:9 sont celles des cinématiques du jeu.
Textures : floues/pixelisées de toute façon, ce sont les originaux de 1999 (agrandir le cache de
RENDER.INI n'a rien changé).

### 7. Avertissement mémoire au démarrage
Sans `nocturne.sdb`, `GlobalMemoryStatus` renvoie « 0.0 Mo » d'échange à ce programme 32 bits sur un
PC à beaucoup de RAM. Correctif `memoire` (12 octets, `0x4C879E` et `0x4C8A1B`) : les deux tests
RAM/échange sautent toujours l'avertissement (mêmes cibles de saut).

### 8. Textures en blocs
Cause : `dgVoodoo.conf [DirectXExt] Dithering = forcealways`, qui simule un affichage 16 bits de 1999
(couleurs réduites + trame agrandie à l'ancienne résolution) : blocs dans les dégradés sombres.
Le lanceur écrit `Dithering = disabled` en Ultra. (Filtrage des textures
vérifié : toujours bilinéaire, drapeau `0x005C0F80` = 1.)

### 9. Décors précalculés remasterisés par IA
Les « blocs » restants venaient surtout des décors : Nocturne affiche des images précalculées
640x480 en 256 couleurs avec tramage (1 611 décors, `BACKDROP\*.RAW` + palette `.ACT` dans les POD
de niveau). Le moteur les dessine en 2D dans sa fenêtre caméra 640x480 : impossible de les afficher
plus grands sans réécrire le moteur.
- Traitement (`outils\decors_hd.py`) : Real-ESRGAN x4 (`realesrgan-x4plus`, C:\Tools\realesrgan-ncnn),
  retour 640x480, contraste local, luminosité calée sur l'original, palette d'origine sans tramage
  (les `.ACT` et `.FOG` restent valables). 15 min pour les 1 611 décors (RTX 5080).
- Injection sans toucher aux POD : correctif `fichierslibres` (10 octets, `0x4C86DA`/`0x4C86E7`) qui
  fait chercher le disque avant les archives ; pack dans `BACKDROP\` du jeu, rangé dans
  `_NocturnePlus\hd` quand l'option est décochée ou après « Repair ».
- Résultat : grain des 256 couleurs supprimé, bois et motifs plus nets ; léger effet de bandes dans
  certains dégradés sombres.
- Quadrillage fin visible en jeu (pas sur les captures) : présent aussi avec les décors d'origine,
  donc pas lié au remaster ; attribué au rendu du jeu, non poursuivi à la demande de l'utilisateur.
- Lissage 2D de dgVoodoo (`Bilinear2DOperations`) désactivé par défaut.

### 10. Textures 3D agrandies (objets, personnages)
`CTextureCache::load` (`FUN_00544EF0`) déduit la taille d'une texture de la longueur du `.RAW`
(4096 = 64², 16384 = 128², 65536 = 256²) : une texture réécrite en 256x256 est lue sans correctif.
`outils\textures_hd.py` : 1 069 textures `ART\*.RAW` (64/128/256) passées par Real-ESRGAN x4, ramenées
à 256x256, palette d'origine sans tramage ; le noir pur (transparent pour le moteur sans `.OPA`)
garde son masque d'origine ; 335 `.OPA` agrandis. Exclues : images 2D à taille fixe (`F0100`..`F1500`,
`MENUCLD2`). Pack dans `ART\` (même interrupteur que les décors) ; `RENDER.INI maxTextures256 = 256`
quand il est actif. **Pas encore vérifié en jeu.**

## Mesures

- Gel au démarrage : 5–8 s avant correctif → **aucun** après (4 lancements).
- CPU du jeu : 13 s pour ~50 s avant correctif (boucle) → 5 s pour 32 s après.
- Multi-cœur : 3 min 01 s, 8 Alt+Tab, 0 non-réponse.
- FPS : **non mesurés** objectivement (pas de capture d'écran, à la demande de l'utilisateur ; pas
  d'outil de mesure installé). Configuration : 60 FPS plafonnés par dgVoodoo + V-Sync.

## Ce qui reste

- **Stabilité 30 min** : non mesurée, test écarté par l'utilisateur (sessions de 3 min max). Si un gel
  survient en jouant, le garde-fou du lanceur relève dans le journal où chaque fil est bloqué.
- Résolution interne HD au-delà de 480 lignes (hold buffer) : non testée.
- Chemin D3D → Vulkan : non nécessaire à ce stade (dgVoodoo D3D11 fluide).
- Defender : la règle ASR anti-ransomware bloque un exe neuf qui renomme des DLL ; exclusion ASR
  ajoutée par l'utilisateur pour `NocturnePlus.exe`.
