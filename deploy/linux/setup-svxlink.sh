#!/bin/sh
# =============================================================================
# setup-svxlink.sh — Installation de SVXLink (19.09.2 legacy + 25.05 moderne)
# pour Orange Pi / Armbian (linux-arm / armhf)
#
# Usage: sudo ./setup-svxlink.sh
#
# Ce script compile et installe deux versions de SVXLink en isolation complète :
#   - SVXLink 19.09.2  → /opt/svxlink-legacy  (protocole V2, AUTH_KEY)
#   - SVXLink 25.05    → /opt/svxlink-modern   (protocole V3, X.509)
#
# Les deux compilations sont indépendantes du svxlink éventuellement installé
# via apt. Le service systemd svxlink.service de l'apt est désactivé pour éviter
# tout conflit (le gestionnaire SvxlinkManagerV2 pilote ses propres processus).
#
# Durée estimée : 30-60 min selon la puissance du Orange Pi, davantage sur une
# carte à faible mémoire, où les fichiers sont compilés un par un.
# Idempotent : relancer le script ne recompile que ce qui manque.
# =============================================================================
set -eu

LEGACY_VERSION="19.09.2"
MODERN_VERSION="25.05"
LEGACY_PREFIX="/opt/svxlink-legacy"
MODERN_PREFIX="/opt/svxlink-modern"
# Sur disque, pas dans /tmp : Armbian monte /tmp en tmpfs, et les ~2 Go de
# sources et d'objets y seraient pris sur la RAM.
BUILD_DIR="/var/tmp/svxlink-build"

# Mémoire de compilation. Un compilateur C++ peut occuper plusieurs centaines
# de Mo sur certains fichiers de SVXLink : chaque compilation parallèle se voit
# réserver MB_PER_JOB, et RAM + swap doivent atteindre MIN_BUILD_MEMORY_MB, à
# défaut de quoi un swap temporaire est ajouté le temps de la compilation.
MB_PER_JOB=700
MIN_BUILD_MEMORY_MB=1536
BUILD_DISK_MB=2048
SWAP_FILE="/var/tmp/svxlink-build.swap"

log() {
  printf '\n=== %s ===\n' "$1"
}

fail() {
  printf 'ERREUR: %s\n' "$1" >&2
  exit 1
}

# Valeur d'une ligne de /proc/meminfo, en Mo (0 si absente)
meminfo_mb() {
  awk -v key="$1:" '$1 == key { mb = int($2 / 1024) } END { print mb + 0 }' /proc/meminfo
}

# Retire le swap temporaire, y compris après un échec ou une interruption
remove_build_swap() {
  if [ -f "$SWAP_FILE" ]; then
    swapoff "$SWAP_FILE" 2>/dev/null || true
    rm -f "$SWAP_FILE"
  fi
}
trap remove_build_swap EXIT
trap 'exit 1' INT TERM

# Vérification root
if [ "$(id -u)" -ne 0 ]; then
  fail "Ce script doit être exécuté avec les droits root (sudo)."
fi

# Vérification de la connectivité réseau
if ! curl -s --max-time 5 https://github.com > /dev/null 2>&1; then
  fail "Impossible de contacter github.com. Vérifiez la connexion réseau."
fi

# =============================================================================
# Désinstallation complète du svxlink installé par apt (si présent).
# SvxlinkManagerV2 pilote directement ses propres processus via les binaires
# dans /opt/svxlink-{legacy,modern}. Laisser le paquet apt en place entraîne :
#   - Conflits de processus (pgrep/pkill affectent les deux processus)
#   - Fichiers de config dans /etc/svxlink/ qui peuvent interférer
#   - Service systemd qui redémarre svxlink de façon autonome
# =============================================================================
SVXLINK_APT_PACKAGES="svxlink-server svxlink-sounds-en-us svxlink-sounds-fr svxlink"
INSTALLED=""
for pkg in $SVXLINK_APT_PACKAGES; do
  if dpkg -l "$pkg" 2>/dev/null | grep -q "^ii"; then
    INSTALLED="$INSTALLED $pkg"
  fi
done

if [ -n "$INSTALLED" ]; then
  log "Désinstallation complète des paquets SVXLink apt :$INSTALLED"
  # Arrêt du service avant purge pour éviter les erreurs dpkg
  systemctl stop svxlink.service 2>/dev/null || true
  systemctl disable svxlink.service 2>/dev/null || true
  # Purge : supprime les binaires, les fichiers de config dpkg ET les conffiles
  apt-get purge -y $INSTALLED
  apt-get autoremove -y
  log "Paquets SVXLink apt supprimés."
else
  log "Aucun paquet SVXLink apt détecté — rien à désinstaller."
fi

log "Installation des dépendances de compilation"
apt-get update
apt-get install -y --no-install-recommends \
  git cmake g++ make ca-certificates curl \
  libsigc++-2.0-dev libgsm1-dev libpopt-dev tcl8.6-dev \
  libgcrypt20-dev libspeex-dev libasound2-dev libopus-dev \
  libcurl4-openssl-dev libssl-dev libjsoncpp-dev

rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# =============================================================================
# Adaptation de la compilation à la mémoire disponible
# Un "make -j$(nproc)" lance une compilation par cœur : sur un Orange Pi Zero
# (4 cœurs, 256 à 512 Mo de RAM), le noyau tue alors le compilateur faute de
# mémoire. Le parallélisme est limité à ce que la RAM disponible permet, et un
# swap temporaire couvre ce qui manque à une compilation séquentielle.
# =============================================================================
JOBS=1
if [ ! -f "$LEGACY_PREFIX/bin/svxlink" ] || [ ! -f "$MODERN_PREFIX/bin/svxlink" ]; then
  # Swap laissé par une exécution interrompue
  remove_build_swap

  MEM_AVAILABLE_MB=$(meminfo_mb MemAvailable)
  SWAP_FREE_MB=$(meminfo_mb SwapFree)

  JOBS=$((MEM_AVAILABLE_MB / MB_PER_JOB))
  if [ "$JOBS" -lt 1 ]; then
    JOBS=1
  fi
  if [ "$JOBS" -gt "$(nproc)" ]; then
    JOBS=$(nproc)
  fi

  MISSING_MB=$((MIN_BUILD_MEMORY_MB - MEM_AVAILABLE_MB - SWAP_FREE_MB))
  if [ "$MISSING_MB" -gt 0 ]; then
    # Au moins 512 Mo : un swap plus petit ne vaut pas l'écriture sur la carte
    SWAP_SIZE_MB=$MISSING_MB
    if [ "$SWAP_SIZE_MB" -lt 512 ]; then
      SWAP_SIZE_MB=512
    fi

    DISK_FREE_MB=$(df -Pm "$(dirname "$SWAP_FILE")" | awk 'NR == 2 { print $4 }')
    if [ "$DISK_FREE_MB" -lt $((SWAP_SIZE_MB + BUILD_DISK_MB)) ]; then
      fail "Espace disque insuffisant : ${DISK_FREE_MB} Mo libres, $((SWAP_SIZE_MB + BUILD_DISK_MB)) Mo requis (compilation + swap temporaire de ${SWAP_SIZE_MB} Mo). Agrandissez la partition ou libérez de la place."
    fi

    log "Mémoire faible (${MEM_AVAILABLE_MB} Mo de RAM disponible, ${SWAP_FREE_MB} Mo de swap) — ajout d'un swap temporaire de ${SWAP_SIZE_MB} Mo"
    # fallocate n'est pas supporté par tous les systèmes de fichiers
    if ! fallocate -l "${SWAP_SIZE_MB}M" "$SWAP_FILE" 2>/dev/null; then
      dd if=/dev/zero of="$SWAP_FILE" bs=1M count="$SWAP_SIZE_MB"
    fi
    chmod 600 "$SWAP_FILE"
    mkswap "$SWAP_FILE" > /dev/null
    swapon "$SWAP_FILE"
  fi

  log "Compilation avec $JOBS tâche(s) en parallèle (${MEM_AVAILABLE_MB} Mo de RAM disponible, $(nproc) cœur(s))"
fi

# =============================================================================
# SVXLink 19.09.2 — Legacy (protocole V2 AUTH_KEY)
# Compilé dans /opt/svxlink-legacy — isolation totale du svxlink apt.
# =============================================================================
if [ -f "$LEGACY_PREFIX/bin/svxlink" ]; then
  log "SVXLink $LEGACY_VERSION déjà installé dans $LEGACY_PREFIX — ignoré"
else
  log "Clonage de SVXLink $LEGACY_VERSION"
  git clone --depth 1 --branch "$LEGACY_VERSION" \
    https://github.com/sm0svx/svxlink.git "$BUILD_DIR/svxlink-legacy-src"

  log "Compilation de SVXLink $LEGACY_VERSION (peut prendre 30 min)"
  mkdir -p "$BUILD_DIR/svxlink-legacy-build"
  cd "$BUILD_DIR/svxlink-legacy-build"
  cmake -DCMAKE_INSTALL_PREFIX="$LEGACY_PREFIX" \
        -DUSE_QT=OFF \
        -DCMAKE_BUILD_TYPE=Release \
        "$BUILD_DIR/svxlink-legacy-src/src"
  make -j"$JOBS"
  make install

  log "SVXLink $LEGACY_VERSION installé dans $LEGACY_PREFIX"
fi

# =============================================================================
# SVXLink 25.05 — Moderne (protocole V3 X.509)
# Compilé dans /opt/svxlink-modern — isolation totale.
# =============================================================================
if [ -f "$MODERN_PREFIX/bin/svxlink" ]; then
  log "SVXLink $MODERN_VERSION déjà installé dans $MODERN_PREFIX — ignoré"
else
  log "Clonage de SVXLink $MODERN_VERSION"
  git clone --depth 1 --branch "$MODERN_VERSION" \
    https://github.com/sm0svx/svxlink.git "$BUILD_DIR/svxlink-modern-src"

  log "Compilation de SVXLink $MODERN_VERSION (peut prendre 30 min)"
  mkdir -p "$BUILD_DIR/svxlink-modern-build"
  cd "$BUILD_DIR/svxlink-modern-build"
  cmake -DCMAKE_INSTALL_PREFIX="$MODERN_PREFIX" \
        -DUSE_QT=OFF \
        -DCMAKE_BUILD_TYPE=Release \
        "$BUILD_DIR/svxlink-modern-src/src"
  make -j"$JOBS"
  make install

  log "SVXLink $MODERN_VERSION installé dans $MODERN_PREFIX"
fi

# =============================================================================
# Configuration système
# =============================================================================
log "Configuration des bibliothèques partagées"
echo "$LEGACY_PREFIX/lib" > /etc/ld.so.conf.d/svxlink-legacy.conf
echo "$MODERN_PREFIX/lib" > /etc/ld.so.conf.d/svxlink-modern.conf
ldconfig

log "Création des répertoires requis"
mkdir -p /var/spool/svxlink
mkdir -p /var/log/svxlink
mkdir -p /var/lib/svxlink/pki/pending_csrs
mkdir -p /var/lib/svxlink/pki/csrs
mkdir -p /var/lib/svxlink/pki/certs
mkdir -p /var/lib/svxlink/pki/ca
mkdir -p /etc/svxlink
mkdir -p "$LEGACY_PREFIX/share/svxlink/sounds/fr_FR/svxlinkmanager"
mkdir -p "$MODERN_PREFIX/share/svxlink/sounds/fr_FR/svxlinkmanager"
mkdir -p "$LEGACY_PREFIX/share/svxlink/events.d/local"
mkdir -p "$MODERN_PREFIX/share/svxlink/events.d/local"

# Hook de signature automatique des certificats X.509 (réflecteur V3 local)
HOOK_SRC="/opt/svxlinkmanagerv2/dev-ca-hook.sh"
HOOK_DST="/usr/local/bin/dev-ca-hook.sh"
if [ -f "$HOOK_SRC" ]; then
  cp "$HOOK_SRC" "$HOOK_DST"
  chmod +x "$HOOK_DST"
  log "dev-ca-hook.sh installé dans $HOOK_DST"
fi

# Nettoyage des sources (libère ~2 Go de sources/objets de compilation)
log "Nettoyage des fichiers de compilation temporaires"
rm -rf "$BUILD_DIR"
remove_build_swap

log "Installation terminée"
printf '\nRésumé:\n'
printf '  SVXLink %s (legacy)  : %s/bin/svxlink\n' "$LEGACY_VERSION" "$LEGACY_PREFIX"
printf '  SVXLink %s (moderne) : %s/bin/svxlink\n' "$MODERN_VERSION" "$MODERN_PREFIX"
printf '  SVXLink apt          : désinstallé (purge)\n'
printf '\nProchaine étape: installez le paquet .deb de SvxlinkManagerV2.\n'
