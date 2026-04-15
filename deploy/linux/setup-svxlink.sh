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
# Durée estimée : 30-60 min selon la puissance du Orange Pi.
# Idempotent : relancer le script ne recompile que ce qui manque.
# =============================================================================
set -eu

LEGACY_VERSION="19.09.2"
MODERN_VERSION="25.05"
LEGACY_PREFIX="/opt/svxlink-legacy"
MODERN_PREFIX="/opt/svxlink-modern"
BUILD_DIR="/tmp/svxlink-build"

log() {
  printf '\n=== %s ===\n' "$1"
}

fail() {
  printf 'ERREUR: %s\n' "$1" >&2
  exit 1
}

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
  make -j"$(nproc)"
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
  make -j"$(nproc)"
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

log "Installation terminée"
printf '\nRésumé:\n'
printf '  SVXLink %s (legacy)  : %s/bin/svxlink\n' "$LEGACY_VERSION" "$LEGACY_PREFIX"
printf '  SVXLink %s (moderne) : %s/bin/svxlink\n' "$MODERN_VERSION" "$MODERN_PREFIX"
printf '  SVXLink apt          : désinstallé (purge)\n'
printf '\nProchaine étape: installez le paquet .deb de SvxlinkManagerV2.\n'

LEGACY_VERSION="19.09.2"
MODERN_VERSION="25.05"
LEGACY_PREFIX="/opt/svxlink-legacy"
MODERN_PREFIX="/opt/svxlink-modern"
BUILD_DIR="/tmp/svxlink-build"

log() {
  printf '\n=== %s ===\n' "$1"
}

fail() {
  printf 'ERREUR: %s\n' "$1" >&2
  exit 1
}

# Vérification root
if [ "$(id -u)" -ne 0 ]; then
  fail "Ce script doit être exécuté avec les droits root (sudo)."
fi

# Vérification de la connectivité réseau
if ! curl -s --max-time 5 https://github.com > /dev/null 2>&1; then
  fail "Impossible de contacter github.com. Vérifiez la connexion réseau."
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
# SVXLink Legacy — protocole V2 AUTH_KEY
# Stratégie :
#   1. Si SVXLink est déjà installé via apt (/usr/bin/svxlink), créer des
#      symlinks dans /opt/svxlink-legacy pour satisfaire la SvxLinkLegacyStrategy.
#   2. Sinon, compiler 19.09.2 depuis les sources GitHub.
# =============================================================================
if [ -f "$LEGACY_PREFIX/bin/svxlink" ]; then
  log "SVXLink legacy déjà disponible dans $LEGACY_PREFIX — ignoré"
elif [ -f /usr/bin/svxlink ]; then
  log "SVXLink détecté via apt (/usr/bin/svxlink) — création des symlinks dans $LEGACY_PREFIX"

  # Binaire
  mkdir -p "$LEGACY_PREFIX/bin"
  ln -sf /usr/bin/svxlink "$LEGACY_PREFIX/bin/svxlink"

  # Le répertoire lib reste vide : les bibliothèques système sont déjà
  # dans les chemins ldconfig standard. LD_LIBRARY_PATH pointera ici mais
  # les libs seront résolues via les paths système.
  mkdir -p "$LEGACY_PREFIX/lib"

  # Symlink sur le répertoire share du système → Logic.tcl et les sons WAV
  # déployés dans $LEGACY_PREFIX/share/svxlink/ atterriront dans /usr/share/svxlink/
  mkdir -p "$LEGACY_PREFIX/share"
  if [ ! -L "$LEGACY_PREFIX/share/svxlink" ]; then
    ln -sf /usr/share/svxlink "$LEGACY_PREFIX/share/svxlink"
  fi

  log "Symlinks créés : $LEGACY_PREFIX → /usr (binaire + share)"
else
  log "SVXLink introuvable via apt — compilation de $LEGACY_VERSION depuis les sources"
  git clone --depth 1 --branch "$LEGACY_VERSION" \
    https://github.com/sm0svx/svxlink.git "$BUILD_DIR/svxlink-legacy-src"

  mkdir -p "$BUILD_DIR/svxlink-legacy-build"
  cd "$BUILD_DIR/svxlink-legacy-build"
  cmake -DCMAKE_INSTALL_PREFIX="$LEGACY_PREFIX" \
        -DUSE_QT=OFF \
        -DCMAKE_BUILD_TYPE=Release \
        "$BUILD_DIR/svxlink-legacy-src/src"
  make -j"$(nproc)"
  make install

  log "SVXLink $LEGACY_VERSION installé dans $LEGACY_PREFIX"
fi

# =============================================================================
# SVXLink 25.05 — Moderne (protocole V3 X.509)
# =============================================================================
if [ -f "$MODERN_PREFIX/bin/svxlink" ]; then
  log "SVXLink $MODERN_VERSION déjà installé dans $MODERN_PREFIX — ignoré"
else
  log "Clonage de SVXLink $MODERN_VERSION"
  git clone --depth 1 --branch "$MODERN_VERSION" \
    https://github.com/sm0svx/svxlink.git "$BUILD_DIR/svxlink-modern-src"

  log "Compilation de SVXLink $MODERN_VERSION"
  mkdir -p "$BUILD_DIR/svxlink-modern-build"
  cd "$BUILD_DIR/svxlink-modern-build"
  cmake -DCMAKE_INSTALL_PREFIX="$MODERN_PREFIX" \
        -DUSE_QT=OFF \
        -DCMAKE_BUILD_TYPE=Release \
        "$BUILD_DIR/svxlink-modern-src/src"
  make -j"$(nproc)"
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
# Pour la version moderne, les répertoires sont toujours dans /opt/svxlink-modern
mkdir -p "$MODERN_PREFIX/share/svxlink/sounds/fr_FR/svxlinkmanager"
mkdir -p "$MODERN_PREFIX/share/svxlink/events.d/local"
# Pour le legacy : si symlink en place → les répertoires existent déjà via /usr/share/svxlink
# Si installation compilée → créer explicitement
if [ ! -L "$LEGACY_PREFIX/share/svxlink" ]; then
  mkdir -p "$LEGACY_PREFIX/share/svxlink/sounds/fr_FR/svxlinkmanager"
  mkdir -p "$LEGACY_PREFIX/share/svxlink/events.d/local"
fi

# Hook de signature automatique des certificats X.509 (réflecteur V3 local)
HOOK_SRC="/opt/svxlinkmanagerv2/dev-ca-hook.sh"
HOOK_DST="/usr/local/bin/dev-ca-hook.sh"
if [ -f "$HOOK_SRC" ]; then
  cp "$HOOK_SRC" "$HOOK_DST"
  chmod +x "$HOOK_DST"
  log "dev-ca-hook.sh installé dans $HOOK_DST"
fi

# Nettoyage des sources
log "Nettoyage des fichiers de compilation temporaires"
rm -rf "$BUILD_DIR"

log "Installation terminée"
printf '\nRésumé:\n'
if [ -L "$LEGACY_PREFIX/bin/svxlink" ]; then
  printf '  SVXLink legacy (apt)    : %s/bin/svxlink → /usr/bin/svxlink\n' "$LEGACY_PREFIX"
else
  printf '  SVXLink %s (legacy) : %s/bin/svxlink\n' "$LEGACY_VERSION" "$LEGACY_PREFIX"
fi
printf '  SVXLink %s (moderne) : %s/bin/svxlink\n' "$MODERN_VERSION" "$MODERN_PREFIX"
printf '\nProchaine étape: installez le paquet .deb de SvxlinkManagerV2.\n'
