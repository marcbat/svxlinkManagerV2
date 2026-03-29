# Backup Configuration Spotnik — Orange Pi (10.0.0.10)

> Sauvegarde effectuée le 25 mars 2026 — **configuration validée et fonctionnelle** (nœud connecté au salon RRF).  
> Cette documentation est une capture de l'installation Legacy fonctionnelle servant de référence pour le développement de SvxlinkManagerV2.

---

## Informations Système

| Propriété          | Valeur                                                      |
|--------------------|-------------------------------------------------------------|
| **Adresse IP**     | 10.0.0.10                                                   |
| **Hostname**       | spotnikv42                                                  |
| **OS**             | Debian GNU/Linux 10 (Buster)                                |
| **Kernel**         | Linux 4.19.62-sunxi #5.92 SMP Wed Jul 31 22:07:23 CEST 2019 armv7l |
| **Architecture**   | armv7l (ARM 32-bit)                                         |
| **SVXLink**        | v1.7.0                                                      |
| **Spotnik**        | v4.1                                                        |

---

## Architecture de l'installation

SVXLink est lancé en tant que daemon **sans service systemd** via le script `/etc/spotnik/restart.rrf`.  
La configuration active utilisée est `/etc/spotnik/svxlink.rrf` (générée dynamiquement à partir de `svxlink.cfg`).

```
/etc/spotnik/
├── svxlink.cfg          # Template de base (sans HOST/AUTH_KEY du reflector)
├── svxlink.rrf          # Config active (générée par restart.rrf = svxlink.cfg + reflector RRF)
├── svxlink.conf         # Config alternative (même contenu que svxlink.cfg + ModuleParrot)
├── svxlink.el           # Profil EchoLink
├── svxlink.loc          # Profil local
├── svxlink.num          # Profil numérique
├── svxlink.fdv          # Profil FDV
├── svxreflector.conf    # Configuration du reflector local
├── config.json          # Configuration utilisateur (JSON)
├── restart.rrf          # Script de démarrage mode RRF (actif)
├── restart.el           # Script de démarrage mode EchoLink
├── restart.*            # Autres profils de démarrage
├── gpio.conf            # Configuration GPIO
├── network              # Fichier indiquant le mode réseau actif ("rrf")
└── svxlink.d/           # Modules SVXLink
    ├── ModuleHelp.conf
    ├── ModuleEchoLink.conf
    ├── ModuleMetarInfo.conf
    ├── ModuleParrot.conf
    ├── ModulePropagationMonitor.conf
    ├── ModuleDtmfRepeater.conf
    ├── ModuleFrn.conf
    ├── ModuleSelCallEnc.conf
    └── ModuleTclVoiceMail.conf
```

**Commande de lancement :**
```bash
svxlink --daemon --logfile=/tmp/svxlink.log --pidfile=/var/run/svxlink.pid --runasuser=root --config=/etc/spotnik/svxlink.rrf
```

---

## `config.json` — Configuration utilisateur

```json
{
  "callsign": "HB9GXP",
  "sql_det": "GPIO",
  "ctcss_fq": "71.9",
  "default_lang": "fr_FR",
  "type": "EL",
  "echolink_proxy_server": "",
  "echolink_proxy_port": "",
  "echolink_proxy_password": "",
  "echolink_password": "",
  "mail_server": "",
  "mail_username": "",
  "mail_password": "",
  "location_enabled": false,
  "location_latitude": "0.0.0N",
  "location_longitude": "0.0.0W",
  "airport_code": "LFRO",
  "Departement": "CH",
  "band_type": "H",
  "wifi_ssid": "BOX",
  "wpa_key": "0000000001",
  "tx_qrg": "432.975",
  "rx_qrg": "432.975",
  "sql_lvl": "2",
  "lSA818": "No",
  "master_ip_bm": "164.132.195.103"
}
```

---

## `svxlink.rrf` — Configuration active (mode RRF)

> Ce fichier est généré dynamiquement par `restart.rrf` : il prend `svxlink.cfg` comme base et y ajoute les paramètres `HOST`, `AUTH_KEY` et `PORT` du reflector RRF.

```ini
[GLOBAL]
LOGICS=SimplexLogic,ReflectorLogic
CFG_DIR=svxlink.d
TIMESTAMP_FORMAT=%c
CARD_SAMPLE_RATE=16000
CARD_CHANNELS=1
LINKS=ALLlink

[SimplexLogic]
TYPE=Simplex
RX=Rx1
TX=Tx1
MODULES=ModuleHelp,ModuleMetarInfo,ModulePropagationMonitor
CALLSIGN=HB9GXP
SHORT_IDENT_INTERVAL=15
LONG_IDENT_INTERVAL=60
IDENT_ONLY_AFTER_TX=10
EXEC_CMD_ON_SQL_CLOSE=500
EVENT_HANDLER=/usr/share/svxlink/events.tcl
DEFAULT_LANG=fr_FR
RGR_SOUND_ALWAYS=1
RGR_SOUND_DELAY=0
REPORT_CTCSS=71.9
TX_CTCSS=ALWAYS
MACROS=Macros
FX_GAIN_NORMAL=0
FX_GAIN_LOW=-12
ACTIVATE_MODULE_ON_LONG_CMD=10:PropagationMonitor
MUTE_RX_ON_TX=1
DTMF_CTRL_PTY=/tmp/dtmf_uhf

[ALLlink]
CONNECT_LOGICS=SimplexLogic:434MHZ:945,ReflectorLogic
DEFAULT_ACTIVE=1
TIMEOUT=0

[Rx1]
TYPE=Local
AUDIO_DEV=alsa:plughw:1
AUDIO_CHANNEL=0
SQL_DET=GPIO
SQL_START_DELAY=500
SQL_DELAY=100
SQL_HANGTIME=40
SQL_EXTENDED_HANGTIME=1000
SQL_EXTENDED_HANGTIME_THRESH=13
SQL_TIMEOUT=600
VOX_FILTER_DEPTH=300
VOX_THRESH=1000
CTCSS_MODE=2
CTCSS_FQ=71.9
CTCSS_SNR_OFFSET=0
CTCSS_OPEN_THRESH=15
CTCSS_CLOSE_THRESH=9
CTCSS_BPF_LOW=60
CTCSS_BPF_HIGH=260
GPIO_PATH=/sys/class/gpio
GPIO_SQL_PIN=gpio10
DEEMPHASIS=0
SQL_TAIL_ELIM=0
PREAMP=-4
PEAK_METER=1
DTMF_DEC_TYPE=INTERNAL
DTMF_MUTING=1
DTMF_HANGTIME=40
1750_MUTING=1

[Tx1]
TYPE=Local
AUDIO_DEV=alsa:plughw:1
AUDIO_CHANNEL=0
PTT_TYPE=GPIO
GPIO_PATH=/sys/class/gpio
PTT_PIN=gpio7
TIMEOUT=300
TX_DELAY=550
PREAMP=0
CTCSS_FQ=71.9
CTCSS_LEVEL=9
PREEMPHASIS=0
DTMF_TONE_LENGTH=100
DTMF_TONE_SPACING=50
DTMF_DIGIT_PWR=-15

[LocationInfo]
APRS_SERVER_LIST=euro.aprs2.net:14580
LON_POSITION=0.0.0W
LAT_POSITION=0.0.0N
CALLSIGN=EL-HB9GXP
FREQUENCY=432.975
TX_POWER=1
ANTENNA_GAIN=6
ANTENNA_HEIGHT=8m
ANTENNA_DIR=-1
PATH=WIDE1-1
BEACON_INTERVAL=10
TONE=71
COMMENT=>>> SpotNik V4.1 432.975 MHz Tone 71.9Hz <<<<<

[ReflectorLogic]
TYPE=Reflector
CALLSIGN=(CH) HB9GXP H
AUDIO_CODEC=OPUS
JITTER_BUFFER_DELAY=2
HOST=rrf.f5nlg.ovh
AUTH_KEY=Magnifique123456789!
PORT=5300
```

---

## `svxlink.conf` — Configuration alternative (avec ModuleParrot)

> Même base que `svxlink.cfg`, avec `ModuleParrot` ajouté aux modules de `SimplexLogic`.

```ini
[GLOBAL]
LOGICS=SimplexLogic,ReflectorLogic
CFG_DIR=svxlink.d
TIMESTAMP_FORMAT=%c
CARD_SAMPLE_RATE=16000
CARD_CHANNELS=1
LINKS=ALLlink

[SimplexLogic]
TYPE=Simplex
RX=Rx1
TX=Tx1
MODULES=ModuleHelp,ModuleMetarInfo,ModulePropagationMonitor,ModuleParrot
CALLSIGN=
SHORT_IDENT_INTERVAL=15
LONG_IDENT_INTERVAL=60
IDENT_ONLY_AFTER_TX=10
EXEC_CMD_ON_SQL_CLOSE=500
EVENT_HANDLER=/usr/share/svxlink/events.tcl
DEFAULT_LANG=fr_FR
RGR_SOUND_ALWAYS=1
RGR_SOUND_DELAY=0
REPORT_CTCSS=71.9
TX_CTCSS=ALWAYS
MACROS=Macros
FX_GAIN_NORMAL=0
FX_GAIN_LOW=-12
ACTIVATE_MODULE_ON_LONG_CMD=10:PropagationMonitor
MUTE_RX_ON_TX=1
DTMF_CTRL_PTY=/tmp/dtmf_uhf

[ALLlink]
CONNECT_LOGICS=SimplexLogic:434MHZ:945,ReflectorLogic
DEFAULT_ACTIVE=1
TIMEOUT=0

[Rx1]
TYPE=Local
AUDIO_DEV=alsa:plughw:1
AUDIO_CHANNEL=0
SQL_DET=GPIO
SQL_START_DELAY=500
SQL_DELAY=100
SQL_HANGTIME=40
SQL_EXTENDED_HANGTIME=1000
SQL_EXTENDED_HANGTIME_THRESH=13
SQL_TIMEOUT=600
VOX_FILTER_DEPTH=300
VOX_THRESH=1000
CTCSS_MODE=2
CTCSS_FQ=71.9
CTCSS_SNR_OFFSET=0
CTCSS_OPEN_THRESH=15
CTCSS_CLOSE_THRESH=9
CTCSS_BPF_LOW=60
CTCSS_BPF_HIGH=260
GPIO_PATH=/sys/class/gpio
GPIO_SQL_PIN=gpio10
DEEMPHASIS=0
SQL_TAIL_ELIM=0
PREAMP=-4
PEAK_METER=1
DTMF_DEC_TYPE=INTERNAL
DTMF_MUTING=1
DTMF_HANGTIME=40
1750_MUTING=1

[Tx1]
TYPE=Local
AUDIO_DEV=alsa:plughw:1
AUDIO_CHANNEL=0
PTT_TYPE=GPIO
GPIO_PATH=/sys/class/gpio
PTT_PIN=gpio7
TIMEOUT=300
TX_DELAY=550
PREAMP=0
CTCSS_FQ=71.9
CTCSS_LEVEL=9
PREEMPHASIS=0
DTMF_TONE_LENGTH=100
DTMF_TONE_SPACING=50
DTMF_DIGIT_PWR=-15

[LocationInfo]
APRS_SERVER_LIST=euro.aprs2.net:14580
LON_POSITION=0.0.0W
LAT_POSITION=0.0.0N
CALLSIGN=
FREQUENCY=432.975
TX_POWER=1
ANTENNA_GAIN=6
ANTENNA_HEIGHT=8m
ANTENNA_DIR=-1
PATH=WIDE1-1
BEACON_INTERVAL=10
TONE=71
COMMENT=>>> SpotNik V4.1 432.975 MHz Tone 71.9Hz <<<<<

[ReflectorLogic]
TYPE=Reflector
CALLSIGN=
AUDIO_CODEC=OPUS
JITTER_BUFFER_DELAY=2
```

---

## `svxreflector.conf` — Configuration du reflector local

```ini
###################################################################
#
# Configuration file for the SvxReflector SvxLink conference node
#
###################################################################

[GLOBAL]
#CFG_DIR=svxreflector.d
TIMESTAMP_FORMAT="%c"
LISTEN_PORT=5300
SQL_TIMEOUT=300
SQL_TIMEOUT_BLOCKTIME=30
CODECS=OPUS

## RRF design ##
AUTH_KEY="Magnifique123456789!"
```

---

## Modules SVXLink (`/etc/spotnik/svxlink.d/`)

### `ModuleHelp.conf`

```ini
[ModuleHelp]
NAME=Help
ID=0
TIMEOUT=60
```

### `ModuleParrot.conf`

```ini
[ModuleParrot]
NAME=Parrot
ID=1
TIMEOUT=600
FIFO_LEN=60
REPEAT_DELAY=1000
```

### `ModuleEchoLink.conf`

```ini
[ModuleEchoLink]
NAME=EchoLink
ID=2
SERVERS=servers.echolink.org
CALLSIGN=F1AMM-L
PASSWORD=
SYSOPNAME=SPOTNIK V4.1
LOCATION="[Svx] Fq, MyTown"
MAX_QSOS=4
MAX_CONNECTIONS=5
LINK_IDLE_TIMEOUT=300
USE_GSM_ONLY=0
DESCRIPTION=SPOTNIK V4.0
DEFAULT_LANG=fr_FR
ACCEPT_INCOMING=^(F1AMM)$
```

### `ModuleMetarInfo.conf`

```ini
[ModuleMetarInfo]
NAME=MetarInfo
ID=5
TIMEOUT=120
TYPE=XML
SERVER=https://aviationweather.gov
LINK="/adds/dataserver_current/httpparam?dataSource=metars&requestType=retrieve&format=xml&hoursBeforeNow=3&mostRecent=true&stationString="
STARTDEFAULT=LFRO
AIRPORTS=LFRO
```

### `ModulePropagationMonitor.conf`

```ini
[ModulePropagationMonitor]
NAME=PropagationMonitor
PLUGIN_NAME=Tcl
ID=10
#TIMEOUT=10
SPOOL_DIR=/var/spool/svxlink/propagation_monitor
```

---

## `restart.rrf` — Script de démarrage mode RRF

```bash
#!/bin/bash
# DTMF 96 RRF #
# stop numeric modes
/etc/spotnik/num.sh stop
pkill -f svxbridge.py

# Stop svxlink
if pgrep -x svxlink >/dev/null
then
    pkill -TERM svxlink
    pkill -f timersalon
fi

# stop vncserver
if pgrep -x Xtightvnc >/dev/null
then
    pkill -TERM vncserver:1
fi

# Save network
echo "rrf" > /etc/spotnik/network

# gestion des annonces vocales
rm /usr/share/svxlink/sounds/fr_FR/PropagationMonitor/name.wav
ln -s /usr/share/svxlink/sounds/fr_FR/RRF/Srrf.wav /usr/share/svxlink/sounds/fr_FR/PropagationMonitor/name.wav

# creation du svxlink.rrf
rm -f /etc/spotnik/svxlink.rrf
sleep 1
cat /etc/spotnik/svxlink.cfg >/etc/spotnik/svxlink.rrf
# copie du host pour le reflector
echo "HOST=rrf.f5nlg.ovh" >>/etc/spotnik/svxlink.rrf
echo "AUTH_KEY=Magnifique123456789!" >>/etc/spotnik/svxlink.rrf
echo "PORT=5300" >>/etc/spotnik/svxlink.rrf

sleep 1

# Clear logs
> /tmp/svxlink.log

# Launch svxlink
svxlink --daemon --logfile=/tmp/svxlink.log --pidfile=/var/run/svxlink.pid --runasuser=root --config=/etc/spotnik/svxlink.rrf
sleep 1

# Enable propagation monitor module
echo "10#" > /tmp/dtmf_uhf
echo "10#" > /tmp/dtmf_vhf
```

---

## Résumé des paramètres clés

| Paramètre               | Valeur                        |
|-------------------------|-------------------------------|
| **Mode actif**          | RRF (Réseau Radio Francophone) |
| **Callsign**            | `HB9GXP`                      |
| **CTCSS**               | 71.9 Hz                       |
| **Fréquence TX/RX**     | 432.975 MHz                   |
| **Audio device**        | `alsa:plughw:1`               |
| **Squelch**             | GPIO — `gpio10`               |
| **PTT**                 | GPIO — `gpio7`                |
| **DTMF PTY**            | `/tmp/dtmf_uhf`               |
| **Langue**              | `fr_FR`                       |
| **Reflector RRF**       | `rrf.f5nlg.ovh:5300`          |
| **Callsign Reflector**  | `(CH) HB9GXP H`               |
| **Callsign EchoLink**   | `EL-HB9GXP`                   |
| **Département**         | CH (Suisse)                   |
| **Codec Reflector**     | OPUS                          |
| **Jitter Buffer**       | 2 ms                          |
| **Aéroport Météo**      | LFRO                          |
| **Log SVXLink**         | `/tmp/svxlink.log`            |
| **PID file**            | `/var/run/svxlink.pid`        |

---

## Notes pour SvxlinkManagerV2

- **Pas de service systemd** : le pilotage du daemon doit se faire par gestion du PID (`/var/run/svxlink.pid`) et appel direct au script `restart.rrf` (ou équivalent), via la couche Infrastructure.
- **Config dynamique** : le fichier `svxlink.rrf` est regénéré à chaque démarrage depuis `svxlink.cfg` — la V2 devra reproduire ce mécanisme de génération ou écrire directement le fichier complet.
- **`config.json`** est la source de vérité des paramètres utilisateur côté Legacy — bon candidat pour le mapping vers les entités du domaine.
- **GPIO** utilisé pour PTT (`gpio7`) et Squelch (`gpio10`) — spécifique au hardware Orange Pi.
- **DTMF via PTY** (`/tmp/dtmf_uhf`) : permet l'activation des modules par injection de commandes DTMF sans passer par la radio.
- **Profils multiples** : Spotnik gère plusieurs profils de démarrage (`restart.rrf`, `restart.el`, `restart.loc`, etc.) — à modéliser dans le domaine V2 comme des "modes" configurables.
