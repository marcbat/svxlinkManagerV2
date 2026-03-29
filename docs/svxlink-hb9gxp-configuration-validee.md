# Configuration SVXLink HB9GXP — Validée et Fonctionnelle

> Configuration validée le 28 mars 2026 sur Orange Pi Zero (10.0.0.10).  
> Connexion bidirectionnelle établie avec le **Salon Suisse Romand** (`salonsuisseromand.hbspot.ch`).  
> Cette configuration sert de référence pour SvxlinkManagerV2.

---

## Informations Système

| Propriété          | Valeur                                                              |
|--------------------|---------------------------------------------------------------------|
| **Adresse IP**     | 10.0.0.10                                                           |
| **Hostname**       | SvxlinkManager                                                      |
| **OS**             | Ubuntu 20.04.2 LTS (Focal Fossa) — image Spotnik/Armbian modifiée  |
| **Kernel**         | `5.10.43-sunxi #21.05.6` (armv7l — Orange Pi Zero H3)              |
| **Architecture**   | armv7l (ARM 32-bit)                                                 |
| **SVXLink**        | v1.7.0 (paquet `svxlink-server 19.09.1-2`)                          |
| **Carte audio**    | `H3 Audio Codec` — card 0 (`alsa:plughw:0`)                        |
| **Mode lancement** | Service systemd `svxlink.service`                                   |

> ⚠️ **Image source** : image Spotnik Legacy (sur laquelle tournait l'ancienne app SvxlinkManager). C'est cette image précise qui fonctionne — elle inclut le bon kernel `5.10.43-sunxi` sans le bug de capture ADC présent sur les kernels `6.x`.

---

## Configuration `/boot/armbianEnv.txt`

```ini
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

> L'overlay **`analog-codec`** est indispensable — sans lui, la carte H3 Audio Codec n'est pas détectée (`/proc/asound/cards` = `--- no soundcards ---`).

---

## Configuration ALSA — Carte son H3 Audio Codec

### Niveaux validés (persistés via `alsactl store`)

| numid | Nom                        | Valeur validée | Commande                              |
|-------|----------------------------|----------------|---------------------------------------|
| 17    | Line In Capture Switch     | `on,on`        | `amixer -c 0 cset numid=17 on,on`    |
| 9     | ADC Gain Capture Volume    | `7` (max)      | `amixer -c 0 cset numid=9 7`         |
| 1     | DAC Playback Volume        | `63` (max)     | `amixer -c 0 cset numid=1 63`        |
| 3     | Line Out Playback Volume   | `22`           | *(valeur par défaut, non modifiée)*   |

> **Point critique** : `Line In Capture Switch (numid=17)` est **off,off par défaut** à chaque boot. Il doit être activé. La commande `alsactl store` sauvegarde l'état dans `/var/lib/alsa/asound.state`, chargé automatiquement au démarrage par `alsa-restore.service`.

### Test de capture validé

```bash
arecord -D plughw:0,0 -f S16_LE -r 48000 -c 1 -d 3 /dev/null
# Résultat attendu : "Recording WAVE '/dev/null'" sans erreur I/O — exitcode=0
```

> ⚠️ Sur kernel `6.x` (Armbian 24.x), `arecord` retourne `pcm_read: Input/output error` même avec les bons niveaux ALSA — problème driver non résolu. **Rester sur kernel `5.10.43-sunxi`.**

---

## Problèmes rencontrés et solutions

### 1. Fichiers TCL corrompus (`local/Logic.tcl`, `local/Locale.tcl`)

**Symptôme :** Au démarrage, SVXLink produisait une erreur :
```
/usr/share/svxlink/events.tcl in logic SimplexLogic: invalid command name "████████████..."
```
Les fichiers `/usr/share/svxlink/events.d/local/Logic.tcl` et `local/Locale.tcl` étaient de type **data** (binaire) — laissés par l'ancienne installation Legacy Spotnik (août 2021).

**Solution :** Ces fichiers ont été sauvegardés en `.bak` et remplacés par des fichiers vides :
```bash
mv /usr/share/svxlink/events.d/local/Logic.tcl /usr/share/svxlink/events.d/local/Logic.tcl.bak
mv /usr/share/svxlink/events.d/local/Locale.tcl /usr/share/svxlink/events.d/local/Locale.tcl.bak
touch /usr/share/svxlink/events.d/local/Logic.tcl
touch /usr/share/svxlink/events.d/local/Locale.tcl
```

### 2. GPIO non accessibles par l'utilisateur `svxlink`

**Symptôme :** `ERROR: Could not open GPIO /sys/class/gpio/gpio7/value for writing in transmitter Tx1.`

**Cause :** Le service `svxlink.service` tourne sous l'utilisateur `svxlink` (pas root). Les GPIO exportés manuellement appartiennent à `root`.

**Solution :** Déclarer les GPIO dans `/etc/svxlink/gpio.conf`. Le service `svxlink_gpio_setup.service` (qui tourne en root avant SVXLink) exporte les GPIO et leur attribue le bon propriétaire (`svxlink:svxlink`).

### 3. Niveau ALSA — `Line In Capture Switch` désactivé par défaut

**Symptôme :** `arecord: pcm_read: Input/output error`

**Cause :** Le codec H3 démarre avec `Line In Capture Switch = off,off`.

**Solution :** Activé via `amixer` et persisté avec `alsactl store` :
```bash
amixer -c 0 cset numid=17 on,on   # Line In Capture Switch
amixer -c 0 cset numid=9 7         # ADC Gain : max (7/7)
alsactl store
```

---

## `/etc/svxlink/svxlink.conf` — Configuration validée

```ini
[GLOBAL]
LOGICS=SimplexLogic,ReflectorLogic
CFG_DIR=svxlink.d
TIMESTAMP_FORMAT=%c
CARD_SAMPLE_RATE=48000
CARD_CHANNELS=1
LINKS=ALLlink

[SimplexLogic]
TYPE=Simplex
RX=Rx1
TX=Tx1
MODULES=ModuleHelp,ModuleParrot
CALLSIGN=HB9GXP
SHORT_IDENT_INTERVAL=15
LONG_IDENT_INTERVAL=60
IDENT_ONLY_AFTER_TX=10
EXEC_CMD_ON_SQL_CLOSE=500
EVENT_HANDLER=/usr/share/svxlink/events.tcl
DEFAULT_LANG=fr_FR
RGR_SOUND_ALWAYS=1
RGR_SOUND_DELAY=0
MUTE_RX_ON_TX=1

[ALLlink]
CONNECT_LOGICS=SimplexLogic:434MHZ:945,ReflectorLogic
DEFAULT_ACTIVE=1
TIMEOUT=0

[Rx1]
TYPE=Local
AUDIO_DEV=alsa:plughw:0
AUDIO_CHANNEL=0
SQL_DET=GPIO
SQL_START_DELAY=500
SQL_DELAY=0
SQL_HANGTIME=40
SQL_EXTENDED_HANGTIME=1000
SQL_EXTENDED_HANGTIME_THRESH=13
SQL_TIMEOUT=600
GPIO_PATH=/sys/class/gpio
GPIO_SQL_PIN=gpio10
PREAMP=-4
PEAK_METER=1
DTMF_DEC_TYPE=INTERNAL
DTMF_MUTING=1
DTMF_HANGTIME=40
1750_MUTING=1

[Tx1]
TYPE=Local
AUDIO_DEV=alsa:plughw:0
AUDIO_CHANNEL=0
PTT_TYPE=GPIO
GPIO_PATH=/sys/class/gpio
PTT_PIN=gpio7
TIMEOUT=300
TX_DELAY=550
PREAMP=0

[ReflectorLogic]
TYPE=Reflector
CALLSIGN=(CH) HB9GXP H
AUDIO_CODEC=OPUS
JITTER_BUFFER_DELAY=2
HOST=salonsuisseromand.hbspot.ch
AUTH_KEY=xD9wW5gO7yD9hN5o
PORT=5300
```

---

## `/etc/svxlink/gpio.conf` — Configuration GPIO validée

```bash
GPIO_PATH=/sys/class/gpio
GPIO_IN_HIGH="gpio10"
GPIO_IN_LOW=""
GPIO_OUT_HIGH="gpio7"
GPIO_OUT_LOW=""
GPIO_USER="svxlink"
GPIO_GROUP="svxlink"
```

| GPIO    | Rôle       | Direction | Logique |
|---------|------------|-----------|---------|
| gpio7   | PTT        | OUT       | Active HIGH |
| gpio10  | Squelch    | IN        | Active HIGH |

---

## Différences vs configuration Legacy Spotnik

| Paramètre             | Legacy Spotnik           | V2 (validée)                         |
|-----------------------|--------------------------|--------------------------------------|
| `CARD_SAMPLE_RATE`    | 16000                    | **48000** (H3 Audio Codec natif)     |
| `AUDIO_DEV`           | `alsa:plughw:1`          | **`alsa:plughw:0`** (card 0)         |
| `RUNASUSER`           | `root`                   | **`svxlink`** (service systemd)      |
| Lancement             | Script `restart.rrf`     | **`systemctl start svxlink`**        |
| TCL locaux            | Fichiers binaires corrompus | **Fichiers vides** (non utilisés)    |
| GPIO permissions      | root (accès libre)       | **`gpio.conf` + `svxlink_gpio_setup`** |

---

## Notes pour SvxlinkManagerV2

- **`CARD_SAMPLE_RATE=48000`** est obligatoire sur cette image — le codec H3 ne supporte pas 16000 Hz nativement (plughw fait la conversion mais cause des erreurs I/O avec certains kernels).
- **`AUDIO_DEV=alsa:plughw:0`** — la carte H3 Audio Codec est toujours `card 0` sur cette image.
- **GPIO gérés par `gpio.conf`** — c'est le mécanisme officiel SVXLink. SvxlinkManagerV2 devra écrire ce fichier (pas exporter les GPIO directement).
- **Les fichiers `events.d/local/*.tcl` doivent rester vides** — ne pas y écrire de contenu binaire.
- **Persistance ALSA** : `alsactl store` sauvegarde dans `/var/lib/alsa/asound.state` — chargé automatiquement au boot par `alsa-restore.service`.

---

## Déploiement SvxlinkManagerV2 — Validé le 28 mars 2026

### Stack installée sur le Pi

| Composant        | Version / Détail                                              |
|------------------|---------------------------------------------------------------|
| **.NET Runtime** | ASP.NET Core 8.0.15 (tarball officiel `linux-arm`)           |
| **PostgreSQL**   | 12.22 (paquet `postgresql` Ubuntu focal)                      |
| **App**          | SvxlinkManagerV2, publication framework-dependent, ~19 MB     |
| **Port**         | HTTP port **80**                                              |
| **Swap**         | 512 MB (`/swapfile`) — indispensable                         |

### Installation .NET 8 Runtime

Le dépôt apt Microsoft Ubuntu 20.04 **ne fournit pas** `aspnetcore-runtime-8.0` pour `armhf`. Installation via tarball officiel :

```bash
mkdir -p /usr/share/dotnet
wget "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/8.0.15/aspnetcore-runtime-8.0.15-linux-arm.tar.gz" -O /tmp/aspnetcore-8.0.tar.gz
tar -xzf /tmp/aspnetcore-8.0.tar.gz -C /usr/share/dotnet
ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
```

> ⚠️ **Utiliser .NET 8, pas .NET 9** — .NET 9 requiert `GLIBC_2.34` et `GLIBCXX_3.4.29`, absents sur Ubuntu 20.04 (glibc 2.31). L'app plante immédiatement au démarrage avec `.NET 9`.

### Création de la base de données PostgreSQL

```bash
sudo -u postgres psql << 'EOSQL'
CREATE USER svxlinkv2 WITH PASSWORD 'svxlinkmanager';
CREATE DATABASE svxlinkv2 OWNER svxlinkv2;
EOSQL
```

### Swap obligatoire

L'app (Marten + Wolverine + Blazor) consomme ~290 MB de RAM à chaud. Le Pi n'ayant que 491 MB, le **OOM Killer** tue le processus sans swap.

```bash
fallocate -l 512M /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
```

### Service systemd `/etc/systemd/system/svxlinkmanagerv2.service`

```ini
[Unit]
Description=SvxlinkManager V2
After=network.target postgresql.service
Requires=postgresql.service

[Service]
Type=simple
User=root
WorkingDirectory=/opt/svxlinkv2
ExecStart=/usr/local/bin/dotnet /opt/svxlinkv2/SvxlinkManagerV2.Presentation.dll
Restart=on-failure
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://*:80
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

### `appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=svxlinkv2;Username=svxlinkv2;Password=svxlinkmanager"
  },
  "SA818": { "UseMock": false },
  "SvxLink": { "UseMockDaemon": false }
}
```

### Compilation sur Windows pour le Pi

```powershell
# Migrer les .csproj de net9.0 → net8.0 (obligatoire)
# Puis publier framework-dependent :
dotnet publish -c Release -r linux-arm --no-self-contained -o C:\deploy\svxlinkv2-fd
```

> Le build framework-dependent produit ~19 MB (vs ~148 MB en self-contained).

### Temps de démarrage

Marten et Wolverine initialisent leurs schémas PostgreSQL au **premier lancement** — compter **~90 secondes** avant que le port 80 soit disponible. Les démarrages suivants sont identiques (les schémas existent déjà mais Wolverine effectue quand même ses vérifications).
