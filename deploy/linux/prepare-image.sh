#!/bin/bash
# =============================================================================
# prepare-image.sh — Généralisation d'une image SvxLink Manager avant
# distribution (l'équivalent d'un sysprep)
#
# Usage: sudo ./prepare-image.sh --root <point de montage> [--dry-run]
#
# Retire d'une image montée tout ce qui identifie le nœud dont elle provient :
# base de l'application, PKI, clés SSH, machine-id, profils Wi-Fi, journaux… ;
# remet root au mot de passe par défaut (« svxlinkmanager »), expiré, et ne
# l'accepte en SSH que depuis les réseaux privés ; puis réarme les services de
# premier démarrage d'Armbian, qui
# régénèrent les clés d'hôte SSH et les adresses MAC, et agrandissent la
# partition à la taille de la carte.
#
# Le script travaille sur une image MONTÉE, jamais sur le système en marche :
# lancé sur le nœud lui-même, il le remettrait à zéro. Il ne fait que manipuler
# des fichiers, sans rien exécuter de l'image : il fonctionne donc depuis un PC
# x86 (WSL, conteneur Docker privilégié) sur une image ARM.
#
# Idempotent : le relancer sur une image déjà préparée ne retire plus rien.
# Voir l'issue #171 pour le workflow complet (carte maîtresse → capture →
# préparation → réduction → test → publication).
# =============================================================================
set -eu
shopt -s nullglob dotglob

# Mot de passe root par défaut de l'image. Il est public par nature : ce qui protège,
# c'est qu'il soit expiré (changement imposé à la première connexion) et que SSH ne
# l'accepte que depuis les réseaux privés.
DEFAULT_ROOT_PASSWORD="svxlinkmanager"

ROOT=""
DRY_RUN=0
REMOVED=0
RESET=0
WARNINGS=0

usage() {
  echo "Usage: $0 --root <point de montage de la partition système> [--dry-run]" >&2
  exit 2
}

fail() {
  printf 'ERREUR: %s\n' "$1" >&2
  exit 1
}

log() {
  printf '\n=== %s ===\n' "$1"
}

warn() {
  printf '  ! %s\n' "$1"
  WARNINGS=$((WARNINGS + 1))
}

while [ $# -gt 0 ]; do
  case "$1" in
    --root) [ $# -ge 2 ] || usage; ROOT="$2"; shift 2 ;;
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help) usage ;;
    *) usage ;;
  esac
done

# -----------------------------------------------------------------------------
# Garde-fous : on ne doit JAMAIS pouvoir généraliser le système en marche.
# -----------------------------------------------------------------------------
[ -n "$ROOT" ] || usage
[ "$(id -u)" -eq 0 ] || fail "Ce script doit être exécuté avec les droits root (sudo)."
[ -d "$ROOT" ] || fail "$ROOT n'est pas un répertoire."
ROOT=$(realpath "$ROOT")
[ "$ROOT" != "/" ] || fail "--root / désignerait le système en marche."
if [ "$(stat -c '%d:%i' "$ROOT")" = "$(stat -c '%d:%i' /)" ]; then
  fail "$ROOT est la racine du système en marche."
fi
[ -f "$ROOT/etc/armbian-release" ] || fail "$ROOT ne contient pas d'Armbian (etc/armbian-release absent)."
[ -d "$ROOT/opt/svxlinkmanagerv2" ] || fail "$ROOT ne contient pas SvxLink Manager (opt/svxlinkmanagerv2 absent)."
for tool in openssl usermod chage truncate; do
  command -v "$tool" > /dev/null || fail "$tool est requis sur la machine qui prépare l'image."
done

# Chemins relatifs à l'image : un chemin absolu de l'hôte ne doit jamais être atteint.
img() {
  printf '%s/%s' "$ROOT" "${1#/}"
}

size_of() {
  du -sh --apparent-size "$1" 2>/dev/null | cut -f1
}

# Supprime chaque chemin (relatif à l'image) qui existe. Les motifs sont développés
# dans l'image, jamais sur l'hôte : seul le motif est laissé hors guillemets, pour que
# les noms contenant des espaces — ceux de la PKI, par exemple — restent entiers.
remove() {
  local pattern path
  for pattern in "$@"; do
    pattern=${pattern#/}
    for path in "$ROOT"/$pattern; do
      [ -e "$path" ] || [ -L "$path" ] || continue
      printf '  - retiré   %-60s %s\n' "/${path#"$ROOT"/}" "$(size_of "$path")"
      [ "$DRY_RUN" -eq 1 ] || rm -rf --one-file-system -- "$path"
      REMOVED=$((REMOVED + 1))
    done
  done
}

# Vide le contenu d'un répertoire sans supprimer le répertoire lui-même : les
# services et postinst s'attendent à le trouver.
empty_dir() {
  local dir
  dir=$(img "$1")
  [ -d "$dir" ] || return 0
  remove "$1/*"
}

# Supprime les fichiers sous un répertoire, en conservant l'arborescence.
remove_files_under() {
  local dir file
  dir=$(img "$1")
  [ -d "$dir" ] || return 0
  while IFS= read -r -d '' file; do
    printf '  - retiré   %-60s %s\n' "/${file#"$ROOT"/}" "$(size_of "$file")"
    [ "$DRY_RUN" -eq 1 ] || rm -f -- "$file"
    REMOVED=$((REMOVED + 1))
  done < <(find "$dir" \( -type f -o -type l \) -print0)
}

note() {
  printf '  * %s\n' "$1"
  RESET=$((RESET + 1))
}

[ "$DRY_RUN" -eq 1 ] && echo "Mode simulation : rien ne sera modifié."
echo "Image : $ROOT ($(grep -E '^(BOARD|VERSION)=' "$ROOT/etc/armbian-release" | tr '\n' ' '))"
USED_BEFORE=$(df --output=used -B1 "$ROOT" | tail -1)

# -----------------------------------------------------------------------------
log "Application : base, sauvegardes, clés ASP.NET"
# Sans base, l'application repart sur l'assistant /setup : c'est l'expérience voulue
# pour un nouvel utilisateur. Les sauvegardes de base faites avant chaque mise à jour
# en sont des copies, et emportent les mêmes données.
# -----------------------------------------------------------------------------
remove "opt/svxlinkmanagerv2/data/svxlinkmanager.db*"
remove "root/svxlinkmanager.db*"
# Paquets téléchargés par l'ancien mécanisme de mise à jour, remplacé par APT.
remove "opt/svxlinkmanagerv2/data/updates"
# Clés Data Protection : elles signent les cookies d'authentification. Partagées,
# un cookie émis par un nœud serait accepté par tous les autres.
remove "root/.aspnet" "root/.local/share/ASP.NET"

# -----------------------------------------------------------------------------
log "SVXLink : PKI et configurations générées"
# Le même CERT_PKI_DIR sert au nœud ET au réflecteur local : il contient la clé
# privée du nœud, mais aussi l'autorité racine du réflecteur. Laissée dans l'image,
# chaque nœud distribué aurait la même CA. L'arborescence est conservée : le
# postinst la crée et le réflecteur s'attend à la trouver.
# -----------------------------------------------------------------------------
remove_files_under "var/lib/svxlink/pki"
remove "etc/svxlink/svxreflector.conf" "etc/svxlink/node_info.json"

# svxlink.conf sert lui-même de template quand il existe : le supprimer ne suffit pas,
# il faut remettre celui livré par le paquet — ce que fait le postinst à l'installation.
TEMPLATE=$(img "opt/svxlinkmanagerv2/svxlink-config/svxlink.conf")
CONF=$(img "etc/svxlink/svxlink.conf")
if [ -f "$TEMPLATE" ]; then
  if ! cmp -s "$TEMPLATE" "$CONF"; then
    note "remis     /etc/svxlink/svxlink.conf au template du paquet"
    [ "$DRY_RUN" -eq 1 ] || install -m 0644 "$TEMPLATE" "$CONF"
  fi
else
  warn "template du paquet introuvable : /etc/svxlink/svxlink.conf est supprimé, le postinst le recréera"
  remove "etc/svxlink/svxlink.conf"
fi

# -----------------------------------------------------------------------------
log "SSH"
# authorized_keys est le point critique : la clé du mainteneur donnerait un accès
# root à tous les nœuds distribués. Les clés d'hôte sont régénérées au premier
# démarrage par armbian-firstrun, réarmé plus bas.
# -----------------------------------------------------------------------------
remove "root/.ssh/*" "home/*/.ssh/*"
remove "etc/ssh/ssh_host_*"

# Le mot de passe root par défaut est public : SSH ne l'accepte que depuis les réseaux
# privés. Un nœud exposé par erreur (redirection de port) reste ainsi fermé à Internet,
# où seule une clé permet d'entrer. Le fichier est inclus en tête de sshd_config, et
# sshd retient la première valeur lue : il l'emporte sur le « PermitRootLogin yes »
# d'Armbian sans toucher au fichier livré par le paquet.
SSHD_DROPIN=$(img "etc/ssh/sshd_config.d/svxlinkmanager.conf")
SSHD_DROPIN_CONTENT="# SvxLink Manager — posé par prepare-image.sh (#171)
# Le mot de passe root par défaut est public : il n'est accepté que depuis les réseaux
# privés. Ailleurs, root n'entre que par clé.
PermitRootLogin prohibit-password
Match Address 127.0.0.0/8,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16,169.254.0.0/16,::1,fe80::/10,fc00::/7
	PermitRootLogin yes"
if ! grep -q '^Include /etc/ssh/sshd_config.d/\*\.conf' "$(img etc/ssh/sshd_config)"; then
  warn "sshd_config n'inclut pas sshd_config.d : la restriction de root aux réseaux privés serait sans effet"
elif [ "$(cat "$SSHD_DROPIN" 2>/dev/null)" != "$SSHD_DROPIN_CONTENT" ]; then
  note "restreint  le mot de passe root en SSH aux réseaux privés (sshd_config.d/svxlinkmanager.conf)"
  if [ "$DRY_RUN" -eq 0 ]; then
    mkdir -p "$(dirname "$SSHD_DROPIN")"
    printf '%s\n' "$SSHD_DROPIN_CONTENT" > "$SSHD_DROPIN"
    chmod 0644 "$SSHD_DROPIN"
  fi
fi

# -----------------------------------------------------------------------------
log "Comptes"
# Root reçoit un mot de passe par défaut documenté, EXPIRÉ : il ne sert qu'une fois,
# PAM impose d'en choisir un autre à la première connexion. Il garde ainsi un accès
# de dépannage sur un nœud dont l'utilisateur n'a jamais ouvert de session, sans
# laisser dans l'image l'empreinte du mot de passe du mainteneur. Tant qu'il n'est
# pas changé, le champ « dernier changement » de /etc/shadow vaut 0 : c'est à cela
# que l'application le reconnaît (#170). Un mot de passe public ne protège rien en
# soi — d'où la restriction SSH aux réseaux privés, plus bas.
# -----------------------------------------------------------------------------
set_default_root_password() {
  local shadow current lastchg salt hash
  shadow=$(img etc/shadow)
  current=$(awk -F: '$1 == "root" {print $2}' "$shadow")
  lastchg=$(awk -F: '$1 == "root" {print $3}' "$shadow")
  salt=$(printf '%s' "$current" | cut -d'$' -f3)
  if [ "$lastchg" = "0" ] && [ -n "$salt" ] \
     && [ "$(openssl passwd -6 -salt "$salt" "$DEFAULT_ROOT_PASSWORD")" = "$current" ]; then
    return 0
  fi
  note "réinitialisé le mot de passe root au mot de passe par défaut, expiré"
  [ "$DRY_RUN" -eq 1 ] && return 0
  hash=$(openssl passwd -6 "$DEFAULT_ROOT_PASSWORD")
  usermod --root "$ROOT" --password "$hash" root
  chage --root "$ROOT" --lastday 0 root
}

lock_account() {
  local user="$1" current
  current=$(awk -F: -v u="$user" '$1 == u {print $2}' "$(img etc/shadow)")
  [ "$current" = "!" ] && return 0
  note "verrouillé le compte $user (mot de passe et empreinte retirés)"
  [ "$DRY_RUN" -eq 1 ] || usermod --root "$ROOT" --password '!' "$user"
}

set_default_root_password
# Un compte créé par armbian-firstlogin sur la carte maîtresse a un mot de passe connu
# du mainteneur : il est verrouillé de même, et signalé.
while IFS=: read -r user _ uid _; do
  if [ "$uid" -ge 1000 ] && [ "$uid" -lt 65534 ]; then
    warn "compte utilisateur présent : $user (uid $uid) — verrouillé, mais son dossier reste dans l'image"
    lock_account "$user"
  fi
done < "$(img etc/passwd)"
# Le témoin d'Armbian lancerait armbian-firstlogin après le changement imposé par PAM :
# second mot de passe à choisir, et création d'un utilisateur dont on n'a pas besoin.
remove "root/.not_logged_in_yet"

# -----------------------------------------------------------------------------
log "Identité de la machine"
# -----------------------------------------------------------------------------
MACHINE_ID=$(img "etc/machine-id")
if [ -s "$MACHINE_ID" ]; then
  note "vidé      /etc/machine-id (régénéré par systemd au démarrage)"
  [ "$DRY_RUN" -eq 1 ] || truncate --no-create --size 0 -- "$MACHINE_ID"
fi
DBUS_ID=$(img "var/lib/dbus/machine-id")
if [ -e "$DBUS_ID" ] && [ ! -L "$DBUS_ID" ]; then
  note "remplacé  /var/lib/dbus/machine-id par un lien vers /etc/machine-id"
  [ "$DRY_RUN" -eq 1 ] || ln -sf /etc/machine-id "$DBUS_ID"
fi
# Graine du générateur aléatoire : partagée, tous les nœuds démarreraient avec la même.
remove "var/lib/systemd/random-seed"

# -----------------------------------------------------------------------------
log "Réseau"
# Profils Wi-Fi (SSID et mot de passe), baux DHCP, clé secrète de NetworkManager
# (adresses IPv6 stables). L'Ethernet n'a pas besoin de profil : NetworkManager
# en crée un automatiquement.
# -----------------------------------------------------------------------------
empty_dir "etc/NetworkManager/system-connections"
empty_dir "var/lib/NetworkManager"
empty_dir "var/lib/dhcp"

# -----------------------------------------------------------------------------
log "Historiques et caches personnels"
# -----------------------------------------------------------------------------
for home in "$ROOT"/root "$ROOT"/home/*; do
  home=${home#"$ROOT"/}
  remove "$home/.bash_history" "$home/.zsh_history" "$home/.python_history" \
         "$home/.lesshst" "$home/.wget-hsts" "$home/.viminfo" "$home/.cache" \
         "$home/.local/share/nano"
done

# -----------------------------------------------------------------------------
log "Journaux"
# Les fichiers courants sont vidés plutôt que supprimés (certains démons ne les
# recréent pas), les archives sont supprimées. /var/log.hdd est la copie sur carte
# de armbian-ramlog.
# -----------------------------------------------------------------------------
for logdir in var/log var/log.hdd; do
  dir=$(img "$logdir")
  [ -d "$dir" ] || continue
  while IFS= read -r -d '' file; do
    case "$file" in
      *.gz|*.xz|*.[0-9]|*.[0-9].log|*.old)
        printf '  - retiré   %-60s %s\n' "/${file#"$ROOT"/}" "$(size_of "$file")"
        [ "$DRY_RUN" -eq 1 ] || rm -f -- "$file"
        REMOVED=$((REMOVED + 1))
        ;;
      *)
        if [ -s "$file" ]; then
          printf '  - vidé     %-60s %s\n' "/${file#"$ROOT"/}" "$(size_of "$file")"
          # Pas de « : > » : dans un répertoire sticky (/var/log/postgresql), la protection
          # fs.protected_regular refuse même à root l'ouverture O_CREAT du fichier d'un
          # autre utilisateur. truncate --no-create ouvre sans O_CREAT.
          [ "$DRY_RUN" -eq 1 ] || truncate --no-create --size 0 -- "$file"
          REMOVED=$((REMOVED + 1))
        fi
        ;;
    esac
  done < <(find "$dir" -type f -not -path '*/journal/*' -print0)
  empty_dir "$logdir/journal"
done

# -----------------------------------------------------------------------------
log "APT et fichiers temporaires"
# Les listes sont retirées aussi : toute installation commence de toute façon par
# un apt-get update, et elles seraient périmées au moment où l'image est flashée.
# -----------------------------------------------------------------------------
remove "var/cache/apt/archives/*.deb" "var/cache/apt/*.bin"
for f in "$ROOT"/var/lib/apt/lists/*; do
  case "$(basename "$f")" in lock|partial|auxfiles) continue ;; esac
  remove "${f#"$ROOT"/}"
done
empty_dir "tmp"
empty_dir "var/tmp"

# -----------------------------------------------------------------------------
log "Premier démarrage d'Armbian"
# armbian-firstrun régénère les clés d'hôte SSH et les adresses MAC (Ethernet dans
# armbianEnv.txt, Wi-Fi xradio de l'Orange Pi Zero) ; armbian-resize-filesystem
# agrandit la partition. Tous deux se désactivent après leur premier passage : on
# les réactive par lien symbolique, comme le ferait systemctl enable.
# -----------------------------------------------------------------------------
enable_unit() {
  local unit="$1" file target link
  file=$(img "lib/systemd/system/$unit")
  if [ ! -f "$file" ]; then
    warn "$unit introuvable dans l'image : mécanisme de premier démarrage à revérifier pour cette version d'Armbian"
    return 0
  fi
  target=$(sed -n 's/^WantedBy=//p' "$file" | head -1)
  link=$(img "etc/systemd/system/$target.wants/$unit")
  [ -L "$link" ] && return 0
  note "réactivé  $unit ($target)"
  [ "$DRY_RUN" -eq 1 ] && return 0
  mkdir -p "$(dirname "$link")"
  ln -s "/lib/systemd/system/$unit" "$link"
}

enable_unit armbian-firstrun.service
enable_unit armbian-resize-filesystem.service

# -----------------------------------------------------------------------------
log "À examiner"
# Ce que le script ne sait pas classer : il ne le retire pas, mais le signale.
# -----------------------------------------------------------------------------
for entry in "$ROOT"/root/*; do
  case "$(basename "$entry")" in
    .bashrc|.bash_logout|.profile|.zshrc|.oh-my-zsh|.config|.local|.ssh) continue ;;
  esac
  warn "/root/$(basename "$entry") ($(size_of "$entry"))"
done

USED_AFTER=$(df --output=used -B1 "$ROOT" | tail -1)
log "Bilan"
echo "  $REMOVED élément(s) retiré(s) ou vidé(s), $RESET réinitialisation(s), $WARNINGS avertissement(s)."
[ "$DRY_RUN" -eq 1 ] || echo "  Espace libéré : $(( (USED_BEFORE - USED_AFTER) / 1024 / 1024 )) Mio."
echo
echo "Étapes suivantes : démonter, zerofree sur la partition, PiShrink, xz."
