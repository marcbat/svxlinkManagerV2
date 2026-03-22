# Backup Configuration — Orange Pi Zero (SvxlinkManager)

> **Date de collecte** : 2026-03-22  
> **Objectif** : Documentation complète de la configuration avant flash de la carte SD  
> **Nouvelle image cible** : `Armbian_25.5.1_Orangepizero_noble_current_6.12.23` (Ubuntu 24.04 Noble)

---

## 1. Matériel et Système

| Paramètre         | Valeur                                        |
|-------------------|-----------------------------------------------|
| Carte             | Orange Pi Zero                                |
| Architecture      | ARMv7l (32-bit)                               |
| RAM               | 512 MB                                        |
| Carte SD          | ~3.5 GB utilisés                              |
| OS actuel         | Armbian 21.02.2 (Ubuntu 20.04 Focal)          |
| Kernel            | 5.10.43-sunxi armv7l                          |
| GLIBC             | 2.31                                          |
| GLIBCXX           | 3.4.28                                        |
| Hostname          | `SvxlinkManager`                             |
| Board family      | sun8i (H3 SoC)                                |

---

## 2. Réseau

| Paramètre         | Valeur                     |
|-------------------|----------------------------|
| Interface         | eth0                       |
| Adresse IP        | 10.0.0.10/24 (DHCP)        |
| Passerelle        | 10.0.0.1                   |
| MAC (eth0)        | 02:42:1f:66:b0:4f          |
| WiFi (wlan0)      | NO-CARRIER (non utilisé)   |

> **Note** : L'IP est assignée par DHCP. Sur la nouvelle image, **configurer une IP fixe 10.0.0.10**
> pour éviter de perdre l'accès SSH.  
> Connexion NetworkManager : `Wired connection 1` (UUID: `dc185baf-5903-3c86-a8e0-fc97000f5a0b`)

---

## 3. Boot Armbian (`/boot/armbianEnv.txt`)

```
verbosity=1
bootlogo=false
console=serial
disp_mode=1920x1080p60
overlay_prefix=sun8i-h3
overlays=analog-codec uart2 usbhost2 usbhost3
rootdev=UUID=679a67d0-bcee-48d0-b314-0dafcda1991e
rootfstype=ext4
usbstoragequirks=0x2537:0x1066:u,0x2537:0x1068:u
```

> **Overlays critiques** :
> - `analog-codec` — active le codec audio H3 intégré (H3_Audio_Codec)
> - `uart2` — active le port série `/dev/ttyS2` pour le module SA818
> - `usbhost2`, `usbhost3` — ports USB actifs

---

## 4. Audio

### Cartes son

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

### Config ALSA personnalisée

- `/etc/asound.conf` → **ABSENT** (configuration par défaut)
- `/root/.asoundrc` → **ABSENT**

### Modules kernel chargés

```
snd_aloop   24576  0
```

> `snd-aloop` est chargé au boot via **`/etc/rc.local`** (`modprobe snd-aloop`)

---

## 5. GPIO et Radio SA818

### Câblage GPIO (rc.local)

| GPIO | Direction | Rôle          |
|------|-----------|---------------|
| 7    | OUT       | PTT voie 1    |
| 10   | IN        | Squelch voie 1|
| 6    | OUT       | PTT voie 2    |
| 2    | IN        | Squelch voie 2|

### `/etc/rc.local` (intégralité)

```sh
#!/bin/sh -e
#
# rc.local
# This script is executed at the end of each multiuser runlevel.

#Voie radio1 7 = ptt / 10 = squelch
echo "7" > /sys/class/gpio/export
sleep 1
echo out > /sys/class/gpio/gpio7/direction
echo "10" > /sys/class/gpio/export
sleep 1
echo in > /sys/class/gpio/gpio10/direction

#Voie radio2 6 = ptt / 2 = squelch
echo "6" > /sys/class/gpio/export
sleep 1
echo out > /sys/class/gpio/gpio6/direction
echo "2" > /sys/class/gpio/export
sleep 1
echo in > /sys/class/gpio/gpio2/direction

modprobe snd-aloop

exit 0
```

> **⚠️ CRITIQUE** : Ce fichier doit être recopié **tel quel** sur la nouvelle image.

### Service systemd SVXLink GPIO

Fichier : `/lib/systemd/system/svxlink_gpio_setup.service`  
Script up : `/usr/share/svxlink/scripts/svxlink_gpio_up`  
Script down : `/usr/share/svxlink/scripts/svxlink_gpio_down`

Ce service est fourni par le paquet `svxlink-gpio 19.09.1-2` (armhf).  
Il lit `/etc/svxlink/gpio.conf` pour configurer les GPIOs via sysfs.

---

## 6. Ports Série

| Port     | Permissions       | Propriétaire | Groupe  | Usage           |
|----------|-------------------|--------------|---------|-----------------|
| /dev/ttyS0 | crw--w----     | root         | tty     | Console (réservé)|
| /dev/ttyS1 | crw-rw----     | root         | dialout | —               |
| /dev/ttyS2 | crw-rw----     | root         | dialout | **SA818 radio** |
| /dev/ttyS3 | crw-rw----     | root         | dialout | —               |
| /dev/ttyS4–7 | crw-rw---- | root         | dialout | —               |

> L'overlay `uart2` dans `armbianEnv.txt` active le port `/dev/ttyS2`.  
> Configuration SA818 : **9600 baud**, `/dev/ttyS2`

---

## 7. Système / Locale / Timezone

| Paramètre   | Valeur             |
|-------------|--------------------|
| Timezone    | Europe/Zurich (CET, +0100) |
| NTP         | Actif, synchronisé |
| RTC         | UTC                |
| LANG        | fr_FR.UTF-8        |
| LANGUAGE    | en_US.UTF-8        |
| LC_MESSAGES | en_US.UTF-8        |

---

## 8. Modules kernel au démarrage

### `/etc/modules`

```
g_serial
```

> Le module `snd-aloop` est chargé via `rc.local`, pas via `/etc/modules`.

---

## 9. PostgreSQL

| Paramètre          | Valeur                       |
|--------------------|------------------------------|
| Version            | 12.22                        |
| Port               | 5432                         |
| Socket Unix        | `/var/run/postgresql/`       |
| Data dir           | `/var/lib/postgresql/12/main`|
| shared_buffers     | 64 MB                        |
| max_connections    | 20                           |
| work_mem           | 2 MB                         |
| SSL                | Activé (snakeoil)            |

### Base de données applicative

| Paramètre    | Valeur                     |
|--------------|----------------------------|
| Nom DB       | `svxlinkmanager`           |
| Utilisateur  | `svxlink`                  |
| Mot de passe | `Svx1inkM@nager2!`         |

> Command de création :
> ```sql
> CREATE USER svxlink WITH PASSWORD 'Svx1inkM@nager2!';
> CREATE DATABASE svxlinkmanager OWNER svxlink;
> ```

---

## 10. SVXLink

### Version installée

```
svxlink-server    19.09.1-2  armhf
svxlink-gpio      19.09.1-2  all
```

### `/etc/svxlink/svxlink.conf`

```ini
[GLOBAL]
LOGICS=SimplexLogic
CFG_DIR=svxlink.d
TIMESTAMP_FORMAT="%c"
CARD_SAMPLE_RATE=48000

[SimplexLogic]
TYPE=Simplex
RX=Rx1
TX=Tx1
MODULES=ModuleHelp,ModuleParrot,ModuleEchoLink,ModuleTclVoiceMail
CALLSIGN=MYCALL
SHORT_IDENT_INTERVAL=60
LONG_IDENT_INTERVAL=60
EVENT_HANDLER=/usr/share/svxlink/events.tcl
DEFAULT_LANG=en_US
RGR_SOUND_DELAY=0
REPORT_CTCSS=136.5
MACROS=Macros
FX_GAIN_NORMAL=0
FX_GAIN_LOW=-12

[Rx1]
TYPE=Local
AUDIO_DEV=alsa:plughw:0
AUDIO_CHANNEL=0
SQL_DET=VOX
SQL_START_DELAY=0
SQL_DELAY=0
SQL_HANGTIME=2000
VOX_FILTER_DEPTH=20
VOX_THRESH=1000
CTCSS_FQ=136.5
SERIAL_PORT=/dev/ttyS0
SERIAL_PIN=CTS
SIGLEV_SLOPE=1
SIGLEV_OFFSET=0
SIGLEV_OPEN_THRESH=30
SIGLEV_CLOSE_THRESH=10
DEEMPHASIS=0
PEAK_METER=1
DTMF_DEC_TYPE=INTERNAL
DTMF_MUTING=1
DTMF_HANGTIME=40
DTMF_SERIAL=/dev/ttyS0

[Tx1]
TYPE=Local
AUDIO_DEV=alsa:plughw:0
AUDIO_CHANNEL=0
PTT_TYPE=NONE
PTT_PORT=/dev/ttyS0
PTT_PIN=DTRRTS
TIMEOUT=300
TX_DELAY=500
PREEMPHASIS=0
DTMF_TONE_LENGTH=100
DTMF_TONE_SPACING=50
DTMF_DIGIT_PWR=-15

[Macros]
1=EchoLink:9999#
9=Parrot:0123456789#
03400=EchoLink:9999#
```

> **Note** : La configuration SVXLink sur l'Orange Pi est la configuration **par défaut** du paquet.
> Elle n'est pas encore personnalisée — c'est SvxlinkManagerV2 qui gérera les fichiers de config.

### `/etc/svxlink/svxreflector.conf`

```ini
[GLOBAL]
TIMESTAMP_FORMAT="%c"
LISTEN_PORT=5300

[USERS]
# (vide)

[PASSWORDS]
# (vide)
```

### `/etc/svxlink/gpio.conf`

Config par défaut : `GPIO_PATH=/sys/class/gpio`, toutes les listes GPIO vides.  
> Les GPIOs PTT/Squelch sont gérés **manuellement via `rc.local`** (export direct sysfs),
> pas via le fichier gpio.conf.

---

## 11. Services systemd

| Service                          | État initial | Action effectuée             |
|----------------------------------|--------------|------------------------------|
| `svxlinkmanager.service`         | crash-loop   | ✅ Arrêté, désactivé, supprimé|
| `svxreflector.service`           | failed       | ✅ Désactivé                  |
| `svxlink_gpio_setup.service`     | actif (OK)   | ✅ Préservé                   |
| `svxlink.service`                | disabled     | ✅ Préservé (désactivé)       |
| `postgresql.service`             | actif (OK)   | ✅ Préservé                   |

---

## 12. Répertoires supprimés (nettoyage legacy)

| Répertoire             | Taille     | Raison                          |
|------------------------|------------|---------------------------------|
| `/etc/SvxlinkManager/` | ~200 MB    | Installation legacy corrompue   |

---

## 13. Problèmes de déploiement rencontrés (archivage)

### Raison de l'échec

| Tentative                         | Erreur                                      |
|-----------------------------------|---------------------------------------------|
| `.NET 9 self-contained linux-arm` | `GLIBC_2.34 not found` (Ubuntu 20.04 = 2.31)|
| `.NET 8 self-contained linux-arm` | `GLIBCXX_3.4.29 not found` (libstdc++ trop vieux)|
| `.NET 8 + runtime installé manuellement` | `System.Runtime, Version=9.0.0.0 not found` |

**Cause racine** : `Wolverine 5.14.0 → JasperFx.RuntimeCompiler → Roslyn 4.14.0`  
tire `System.Diagnostics.DiagnosticSource ≥ 9.0.5` et `System.IO.Pipelines ≥ 9.0.0`,  
tous deux compilés contre `System.Runtime 9.0.0` — impossible sur un runtime .NET 8.

**Conclusion** : Ubuntu 20.04 (GLIBC 2.31) est irrécupérable pour ce projet. Flash obligatoire.

---

## 14. Procédure de réinstallation sur nouvelle image

### Étapes post-flash (Noble, Ubuntu 24.04)

```bash
# 1. Premier boot — définir IP fixe via armbian-config ou nmcli
nmcli con mod "Wired connection 1" ipv4.addresses 10.0.0.10/24 ipv4.gateway 10.0.0.1 ipv4.dns 8.8.8.8 ipv4.method manual
nmcli con up "Wired connection 1"

# 2. Hostname
hostnamectl set-hostname SvxlinkManager

# 3. Timezone + Locale
timedatectl set-timezone Europe/Zurich
localectl set-locale LANG=fr_FR.UTF-8

# 4. Copier rc.local (GPIO SA818 + snd-aloop)
# Voir section 5 — contenu complet à recopier dans /etc/rc.local
chmod +x /etc/rc.local

# 5. Installer SVXLink 19.09.2 + librairies
apt update
apt install -y svxlink-server svxlink-gpio

# 6. Préparer armbianEnv.txt (overlays)
# overlays=analog-codec uart2 usbhost2 usbhost3

# 7. Installer PostgreSQL (version 16 sur Noble)
apt install -y postgresql postgresql-client

# 8. Créer DB applicative
sudo -u postgres psql -c "CREATE USER svxlink WITH PASSWORD 'Svx1inkM@nager2!';"
sudo -u postgres psql -c "CREATE DATABASE svxlinkmanager OWNER svxlink;"

# 9. Tuning PostgreSQL pour ARM 512 MB
# /etc/postgresql/16/main/postgresql.conf:
# shared_buffers = 64MB
# max_connections = 20
# work_mem = 2MB

# 10. Déployer SvxlinkManagerV2
# (publish net9.0 self-contained linux-arm depuis Windows)
# dotnet publish src/SvxlinkManagerV2.Presentation/SvxlinkManagerV2.Presentation.csproj \
#   -c Release -r linux-arm --self-contained true \
#   -o publish/linux-arm/output
# scp -r publish/linux-arm/output root@10.0.0.10:/opt/svxlinkmanagerv2/
# scp publish/linux-arm/svxlinkmanagerv2.service root@10.0.0.10:/etc/systemd/system/
# ssh root@10.0.0.10 "systemctl daemon-reload && systemctl enable --now svxlinkmanagerv2"
```

### Vérifications post-déploiement

```bash
# Audio
aplay -l      # card 0: H3_Audio_Codec, card 1: Loopback
arecord -l    # idem

# GPIO
ls /sys/class/gpio/gpio{2,6,7,10}/   # doivent exister après rc.local

# Série
ls -la /dev/ttyS2   # doit être présent (overlay uart2)

# Services
systemctl status svxlinkmanagerv2
systemctl status postgresql
systemctl status svxlink_gpio_setup
```

---

## 15. Fichiers de déploiement dans le repo

Chemin : `c:\repos\svxlinkmanagerV2\publish\linux-arm\`

| Fichier                          | Description                              |
|----------------------------------|------------------------------------------|
| `appsettings.Production.json`    | Config app production (PG, SA818, port)  |
| `svxlinkmanagerv2.service`       | Unité systemd                            |
| `install.sh`                     | Script d'installation complet            |
