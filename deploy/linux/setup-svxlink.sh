#!/bin/sh
# =============================================================================
# setup-svxlink.sh — Installation de SVXLink (19.09.2 legacy + 25.05 moderne)
# pour Orange Pi / Armbian (linux-arm / armhf)
#
# Usage: sudo ./setup-svxlink.sh
#
# Ce script compile et installe les deux versions de SVXLink :
#   - SVXLink 19.09.2  → /opt/svxlink-legacy  (protocole V2, AUTH_KEY)
#   - SVXLink 25.05    → /opt/svxlink-modern   (protocole V3, X.509)
#
# Durée estimée : 30-60 min selon la puissance du Orange Pi.
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
# =============================================================================
if [ -f "$LEGACY_PREFIX/bin/svxlink" ]; then
  log "SVXLink $LEGACY_VERSION déjà installé dans $LEGACY_PREFIX — ignoré"
else
  log "Clonage de SVXLink $LEGACY_VERSION"
  git clone --depth 1 --branch "$LEGACY_VERSION" \
    https://github.com/sm0svx/svxlink.git "$BUILD_DIR/svxlink-legacy-src"

  log "Compilation de SVXLink $LEGACY_VERSION"
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

# Nettoyage des sources
log "Nettoyage des fichiers de compilation temporaires"
rm -rf "$BUILD_DIR"

log "Installation terminée"
printf '\nRésumé:\n'
printf '  SVXLink %s (legacy)  : %s/bin/svxlink\n' "$LEGACY_VERSION" "$LEGACY_PREFIX"
printf '  SVXLink %s (moderne) : %s/bin/svxlink\n' "$MODERN_VERSION" "$MODERN_PREFIX"
printf '\nProchaine étape: installez le paquet .deb de SvxlinkManagerV2.\n'
