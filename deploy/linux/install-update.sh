#!/bin/sh
set -eu

APP_DIR="${APP_DIR:-/opt/svxlinkmanagerv2}"
SERVICE_NAME="${SERVICE_NAME:-svxlinkmanagerv2}"
LOG_DIR="${LOG_DIR:-$APP_DIR/logs}"
LOG_FILE="${LOG_FILE:-$LOG_DIR/update-install.log}"
RUNNER_DELAY_SECONDS="${RUNNER_DELAY_SECONDS:-2}"

log() {
  mkdir -p "$LOG_DIR"
  printf '%s %s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')" "$1" >> "$LOG_FILE"
}

ensure_root() {
  if [ "$(id -u)" -ne 0 ]; then
    echo "Ce helper doit être exécuté avec les droits root." >&2
    exit 1
  fi
}

resolve_package_path() {
  case "$1" in
    /*) printf '%s' "$1" ;;
    *) printf '%s/%s' "$(pwd)" "$1" ;;
  esac
}

install_package() {
  package_path="$1"

  export DEBIAN_FRONTEND=noninteractive

  if command -v apt-get >/dev/null 2>&1; then
    apt-get update >> "$LOG_FILE" 2>&1
    apt-get install -y "$package_path" >> "$LOG_FILE" 2>&1
    return
  fi

  if command -v dpkg >/dev/null 2>&1; then
    dpkg -i "$package_path" >> "$LOG_FILE" 2>&1 || true
    if command -v apt-get >/dev/null 2>&1; then
      apt-get install -f -y >> "$LOG_FILE" 2>&1
    fi
    return
  fi

  echo "Ni apt-get ni dpkg ne sont disponibles pour installer le paquet." >&2
  exit 1
}

restart_service_if_possible() {
  if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload >> "$LOG_FILE" 2>&1 || true
    systemctl restart "$SERVICE_NAME" >> "$LOG_FILE" 2>&1
    return
  fi

  log "systemctl indisponible: aucun redémarrage de service n'a été tenté."
}

stop_service_if_possible() {
  if command -v systemctl >/dev/null 2>&1; then
    systemctl stop "$SERVICE_NAME" >> "$LOG_FILE" 2>&1 || true
    return
  fi

  log "systemctl indisponible: aucun arrêt de service n'a été tenté."
}

run_install() {
  package_path="$1"

  ensure_root

  if [ ! -f "$package_path" ]; then
    log "Paquet introuvable: $package_path"
    exit 1
  fi

  log "Début d'installation du paquet $package_path"
  sleep "$RUNNER_DELAY_SECONDS"

  stop_service_if_possible
  install_package "$package_path"
  restart_service_if_possible

  log "Installation terminée avec succès pour $package_path"
}

spawn_runner() {
  package_path="$1"

  ensure_root

  mkdir -p "$LOG_DIR"

  nohup "$0" --run "$package_path" >> "$LOG_FILE" 2>&1 &
  runner_pid=$!

  log "Installation planifiée pour $package_path (PID $runner_pid)"
  echo "Installation planifiée (PID $runner_pid). Voir $LOG_FILE pour le suivi."
}

usage() {
  echo "Usage: $0 <package.deb> | --run <package.deb>" >&2
  exit 1
}

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  usage
fi

if [ "$1" = "--run" ]; then
  if [ "$#" -ne 2 ]; then
    usage
  fi

  run_install "$(resolve_package_path "$2")"
  exit 0
fi

if [ "$#" -ne 1 ]; then
  usage
fi

spawn_runner "$(resolve_package_path "$1")"