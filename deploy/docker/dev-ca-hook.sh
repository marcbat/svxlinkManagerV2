#!/bin/bash
###############################################################################
# dev-ca-hook.sh — Hook CERT_CA_HOOK pour svxreflector en mode développement
#
# Auto-signe tous les CSR entrants sans vérification.
# NE PAS UTILISER EN PRODUCTION — accepte aveuglément toutes les demandes.
#
# Variables d'environnement fournies par svxreflector :
#   CA_OP             : Type d'opération (PENDING_CSR_CREATE, CSR_SIGNED, etc.)
#   CA_CSR_PEM        : Contenu PEM du CSR (pour PENDING_CSR_CREATE/UPDATE)
#   CA_CRT_PEM        : Contenu PEM du certificat (pour CSR_SIGNED, CRT_RENEWED)
###############################################################################

set -e

case "${CA_OP}" in
    PENDING_CSR_CREATE|PENDING_CSR_UPDATE)
        # En mode dev, signer automatiquement le CSR
        # svxreflector détecte les fichiers .csr déplacés de pending_csrs/ vers csrs/
        # et les signe avec l'Issuing CA
        PKI_DIR="${CERT_PKI_DIR:-/var/lib/svxlink/pki}"
        PENDING_DIR="${PKI_DIR}/pending_csrs"
        CSRS_DIR="${PKI_DIR}/csrs"

        # Déplacer tous les CSR en attente vers le dossier de signature
        if [ -d "${PENDING_DIR}" ]; then
            for csr in "${PENDING_DIR}"/*.csr; do
                [ -f "${csr}" ] || continue
                cp "${csr}" "${CSRS_DIR}/"
                echo "[dev-ca-hook] Auto-signé: $(basename "${csr}")"
            done
        fi
        ;;
    CSR_SIGNED)
        echo "[dev-ca-hook] Certificat signé: opération terminée"
        ;;
    CRT_RENEWED)
        echo "[dev-ca-hook] Certificat renouvelé"
        ;;
    *)
        echo "[dev-ca-hook] Opération inconnue: ${CA_OP}"
        ;;
esac

exit 0
