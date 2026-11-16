# Tests du moteur de correctifs de Nocturne Plus sur une copie isolee (lance en PowerShell 32 bits).
param([string]$Lanceur, [string]$Jeu, [string]$Base)
$ErrorActionPreference = 'Stop'
$asm = [Reflection.Assembly]::LoadFile($Lanceur)
$F = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
function T($n) { $asm.GetType("NocturnePlus.$n", $true) }
$tInst = T 'Installation'; $tSig = T 'Signatures'; $tJournal = T 'Journal'
(T 'Correctifs').GetMethod('Enregistrer', $F).Invoke($null, @()) | Out-Null
$catalogue = $tInst.GetField('Correctifs', $F).GetValue($null)
$etatDe = $tSig.GetMethod('EtatDe', $F); $modifier = $tSig.GetMethod('Modifier', $F)
$preparer = (T 'Programme').GetMethod('Preparer', $F)
function Sha($p) { (Get-FileHash -Algorithm SHA1 $p).Hash.ToLower() }
function ShaB($b) { $s = [Security.Cryptography.SHA1]::Create(); ([BitConverter]::ToString($s.ComputeHash($b)) -replace '-', '').ToLower() }
function Inv($meth, $p) { $cible = $p[0]; $a = $p[1]; $u = New-Object object[] $a.Count; for ($i = 0; $i -lt $a.Count; $i++) { $x = $a[$i]; if ($x -is [Management.Automation.PSObject]) { $x = $x.PSObject.BaseObject }; $u[$i] = $x }; $meth.Invoke($cible, [object[]]$u) }
$script:ok = 0; $script:ko = 0
function Verifier($nom, $cond) { if ($cond) { $script:ok++; "  OK   $nom" } else { $script:ko++; "  ECHEC $nom" } }
function Etats($b) { ($catalogue | ForEach-Object { "$($_.Id)=$(Inv $etatDe ($null, @($b, $_)))" }) -join ' ' }

$orig = [IO.File]::ReadAllBytes("$Base\orig.exe")
$utilisateur = [IO.File]::ReadAllBytes("$Base\utilisateur.exe")

"== 1. Exe d'origine (Patch 2, 1.00.52) : tous les sites trouves, etat absent"
$e = Etats $orig; "  $e"
Verifier 'tout absent' ($e -notmatch 'Applique|Partiel|Inconnu')

"== 2. Application par signature = octets de l'ancien moteur a adresses fixes"
$b = [byte[]]$orig.Clone()
foreach ($c in $catalogue) { Verifier "applique $($c.Id)" (Inv $modifier ($null, @($b, $c, $true))) }
$ancien = @(
  @(0xC7ADA, '6800684500'), @(0xC7AE7, '6850924F00'), @(0xC7B9E, 'E96E02000090'), @(0xC7E1B, 'E97B01000090'),
  @(0x3F438, 'A11C765B000FAFC699F73D20765B0090909090'), @(0x157C2C, '68D007DE02'), @(0x157E5E, '9090'),
  @(0x157EC8, 'DF7C2418'), @(0x157ECC, '8B442418'), @(0x157ED0, '89C6'), @(0x152F80, '9090'), @(0x152FBD, 'E9BF000000'))
$attendu = [byte[]]$orig.Clone()
foreach ($a in $ancien) { $h = $a[1]; for ($i = 0; $i -lt $h.Length / 2; $i++) { $attendu[$a[0] + $i] = [Convert]::ToByte($h.Substring($i * 2, 2), 16) } }
Verifier 'identique octet par octet a l ancien moteur' ((ShaB $b) -eq (ShaB $attendu))
$e = Etats $b; "  $e"
Verifier 'tout applique' ($e -notmatch 'Absent|Partiel|Inconnu')
foreach ($c in $catalogue) { Inv $modifier ($null, @($b, $c, $false)) | Out-Null }
Verifier 'retrait complet = exe d origine' ((ShaB $b) -eq (ShaB $orig))

"== 3. Exe actuel de l'utilisateur (corrige par la v1.1.0) : reconnu"
$e = Etats $utilisateur; "  $e"
Verifier 'aucun site inconnu' ($e -notmatch 'Inconnu')
$b = [byte[]]$utilisateur.Clone()
foreach ($c in $catalogue) { Inv $modifier ($null, @($b, $c, $false)) | Out-Null }
Verifier 'retrait de tout = exe d origine' ((ShaB $b) -eq (ShaB $orig))

# exe "d'une autre version" : le contexte du site de l'horloge et de la memoire change
$inconnu = [byte[]]$orig.Clone(); $inconnu[0x157E4D] = 0x90; $inconnu[0xC7B94 + 6] = 0x01
[IO.File]::WriteAllBytes("$Base\inconnu.exe", $inconnu)
"  exe simule : $(Etats $inconnu)"

function Dossier($exe, $correctifs) {
  if (Test-Path $Jeu) { Remove-Item -Recurse -Force $Jeu }
  New-Item -ItemType Directory "$Jeu\SYSTEM", "$Jeu\_NocturnePlus" | Out-Null
  Copy-Item "$Base\$exe" "$Jeu\nocturne.exe"
  Copy-Item "$Base\nocturne.ini", "$Base\RENDER.INI" "$Jeu\SYSTEM\"
  [IO.File]::WriteAllText("$Jeu\_NocturnePlus\NocturnePlus.ini", "Mode=Stable`r`nCoeurs=MultiCoeur`r`nDecorsHD=0`r`nCorrectifs=$correctifs`r`n")
  $script:inst = [Activator]::CreateInstance($tInst, $F, $null, @($Jeu), $null)
  $tJournal.GetField('Fichier', $F).SetValue($null, "$Jeu\_NocturnePlus\journal.txt")
}
function Preparer() { Inv $preparer ($null, @($script:inst, [string[]]@('/preparer', 'stable'))) }
function Journal() { Get-Content "$Jeu\_NocturnePlus\journal.txt" }
function Cnc($cle) { ((Get-Content "$Jeu\ddraw.ini") | Where-Object { $_ -match "^$cle\s*=" }) -replace '.*=\s*', '' }

"== 4. Exe normal + correctifs coches (horloge, memoire) : applique, copie d'origine gardee"
Dossier 'orig.exe' 'horloge,memoire'
$r = Preparer; Verifier "code retour 0 ($r)" ($r -eq 0)
$b = [IO.File]::ReadAllBytes("$Jeu\nocturne.exe"); $e = Etats $b; "  $e"
Verifier 'horloge et memoire appliques, le reste absent' ($e -match 'horloge=Applique' -and $e -match 'memoire=Applique' -and $e -match 'camera169=Absent' -and $e -match 'fichierslibres=Absent')
Verifier 'copie d origine = exe d origine' ((Sha "$Jeu\_NocturnePlus\originaux\nocturne.exe") -eq (Sha "$Base\orig.exe"))
Verifier 'ddraw.ini ecrit (singlecpu=false en multi-coeur)' ((Cnc 'singlecpu') -eq 'false')
"== 4b. Reparer : exe remis d'origine"
$m = $tInst.GetMethod('Reparer', $F); Inv $m ($script:inst, @($null)) | Out-Null
Verifier 'exe = origine apres reparation' ((Sha "$Jeu\nocturne.exe") -eq (Sha "$Base\orig.exe"))

"== 5. Exe inconnu + correctifs coches : rien n'est ecrit dans l'exe, preparation 'jouer quand meme'"
Dossier 'inconnu.exe' 'horloge,memoire'
$avant = Sha "$Jeu\nocturne.exe"
$r = Preparer; Verifier "code retour 5 ($r)" ($r -eq 5)
Verifier 'exe intact' ((Sha "$Jeu\nocturne.exe") -eq $avant)
Verifier 'pas de copie d origine creee' (-not (Test-Path "$Jeu\_NocturnePlus\originaux\nocturne.exe"))
Verifier 'wrapper pose quand meme (ddraw.dll cnc-ddraw)' (Test-Path "$Jeu\ddraw.dll")
Verifier 'mono-coeur force (singlecpu=true)' ((Cnc 'singlecpu') -eq 'true')
Journal | Select-String 'non reconnu|Jouer quand' | ForEach-Object { "  journal : " + ($_.Line -replace '^\S+ \S+\s+', '') }
Verifier 'message cite le Patch 2 (Build 1.00.52)' ((Journal) -match 'Nocturne Patch 2 \(Build 1\.00\.52\)')
$m = $tInst.GetMethod('Appliquer', $F)
try { Inv $m ($script:inst, @(($catalogue | Where-Object Id -eq 'horloge'), $true)); Verifier 'Appliquer refuse un exe inconnu' $false }
catch { Verifier "Appliquer refuse un exe inconnu ($($_.Exception.InnerException.GetType().Name))" ($_.Exception.InnerException.GetType().Name -eq 'ExeNonReconnu') }
Verifier 'exe toujours intact' ((Sha "$Jeu\nocturne.exe") -eq $avant)

"== 6. Exe inconnu + tout decoche : exe ni verifie ni touche"
Dossier 'inconnu.exe' ''
$avant = Sha "$Jeu\nocturne.exe"
$r = Preparer; Verifier "code retour 0 ($r)" ($r -eq 0)
Verifier 'exe intact' ((Sha "$Jeu\nocturne.exe") -eq $avant)
Verifier 'journal : exe ni verifie ni modifie' ((Journal) -match 'ni verifie ni modifie')
Verifier 'aucune mention non reconnu' (-not ((Journal) -match 'non reconnu'))

"== 7. Exe normal + tout decoche : exe ni verifie ni touche"
Dossier 'orig.exe' ''
$r = Preparer; Verifier "code retour 0 ($r)" ($r -eq 0)
Verifier 'exe intact' ((Sha "$Jeu\nocturne.exe") -eq (Sha "$Base\orig.exe"))

"== 8. Liste du lanceur : etat affiche pour un exe inconnu"
Dossier 'inconnu.exe' 'horloge'
$m = $tInst.GetMethod('Etat', $F)
foreach ($c in $catalogue) { "  $($c.Id) : $(Inv $m ($script:inst, @($c)))" }
Verifier 'horloge et memoire incompatibles' ((Inv $m ($script:inst, @(($catalogue | Where-Object Id -eq 'horloge')))) -eq 'incompatible')

""
"Bilan : $script:ok OK, $script:ko echec(s)"
