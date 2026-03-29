# Configuration Audio — Orange Pi Zero (Nouvelle Image)

> **OS** : Armbian Noble (Ubuntu 24.04) — `Armbian_25.5.1_Orangepizero_noble_current_6.12.23`  
> **Date de validation** : 2026-03-25  
> **Matériel** : Orange Pi Zero, SoC H3, codec audio intégré  
>
> Ce fichier documente la configuration **audio validée et opérationnelle** sur la nouvelle image.
> Il s'agit de l'état courant de référence (non d'un backup legacy).

---

## Cartes son

| Index | Nom            | Description                       |
|-------|----------------|-----------------------------------|
| 0     | H3_Audio_Codec | Codec audio intégré SoC H3        |
| 1     | Loopback       | Carte virtuelle snd-aloop         |

### Périphériques ALSA

**Lecture (playback)** :
- `card 0: H3_Audio_Codec` — `CDC PCM Codec-0`
- `card 1: Loopback` — `Loopback PCM` (8 sous-périphériques par flux)

**Capture** :
- `card 0: H3_Audio_Codec` — `CDC PCM Codec-0`
- `card 1: Loopback` — `Loopback PCM` (8 sous-périphériques)

---

## Config ALSA personnalisée

- `/etc/asound.conf` → **ABSENT** (configuration par défaut)
- `/root/.asoundrc` → **ABSENT**

---

## Module kernel snd-aloop

```
snd_aloop   24576  0
```

> `snd-aloop` est chargé au boot via **`/etc/rc.local`** (`modprobe snd-aloop`)

---

## Niveaux ALSA validés ✅

> **⚠️ CRITIQUE** : Ces niveaux sont **obligatoires** pour que l'audio TX (reflector → SA818) fonctionne.  
> Sauvegardés avec `alsactl store` — persistés dans `/var/lib/alsa/asound.state`.

| numid | Contrôle                          | Valeur     | Remarque                                      |
|-------|-----------------------------------|------------|-----------------------------------------------|
| 1     | DAC Playback Volume               | **63/63**  | Maximum — signal suffisant vers Line Out      |
| 3     | Line Out Playback Volume          | **24/31**  | Niveau optimal pour le SA818 (testé)          |
| 4     | Line Out Playback Switch          | on,on      | Sortie activée                                |
| 10    | DAC Playback Switch               | on,on      | DAC activé                                    |
| **20**| **Line Out Source Playback Route**| **1,1**    | **⚠️ CRUCIAL : `1` = DAC→LineOut (pas Line In)** |
| 17    | Line In Capture Switch            | on,on      | Capture RX activée                            |
| 9     | ADC Gain Capture Volume           | 3/7        | Gain capture micro/line-in                    |

> **Piège identifié** : `numid=20` par défaut = `0` ("Stereo" = route Line In vers Line Out).  
> Il faut le passer à `1` ("Mono Differential" = route DAC → Line Out) pour avoir de l'audio en TX.

---

## Commandes de restauration manuelle

```bash
amixer -c 0 cset numid=1 63    # DAC Playback Volume
amixer -c 0 cset numid=3 24    # Line Out Playback Volume
amixer -c 0 cset numid=20 1,1  # Line Out Source = DAC (CRUCIAL)
alsactl store                   # Persister
```

---

## Vérification

```bash
amixer -c 0 contents | grep -A3 'numid=\(1\|3\|20\),'
```
