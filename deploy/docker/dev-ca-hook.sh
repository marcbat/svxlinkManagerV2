#!/bin/bash
###############################################################################
# dev-ca-hook.sh — Hook CERT_CA_HOOK pour svxreflector en mode développement
#
# Auto-signe tous les CSR entrants sans vérification via openssl.
# NE PAS UTILISER EN PRODUCTION — accepte aveuglément toutes les demandes.
#
# Variables d'environnement fournies par svxreflector :
#   CA_OP             : Type d'opération (PENDING_CSR_CREATE, CSR_SIGNED, etc.)
#   CA_CSR_PEM        : Contenu PEM du CSR (pour PENDING_CSR_CREATE/UPDATE)
#   CA_CRT_PEM        : Contenu PEM du certificat (pour CSR_SIGNED, CRT_RENEWED)
#   CERT_PKI_DIR      : Répertoire PKI de svxreflector
###############################################################################

case "${CA_OP}" in
    PENDING_CSR_CREATE|PENDING_CSR_UPDATE)
        PKI_DIR="${CERT_PKI_DIR:-/var/lib/svxlink/pki}"
        PENDING_DIR="${PKI_DIR}/pending_csrs"
        CSRS_DIR="${PKI_DIR}/csrs"
        CERTS_DIR="${PKI_DIR}/certs"
        CA_CRT="${CERTS_DIR}/svxreflector_issuing_ca.crt"
        CA_KEY="${PKI_DIR}/private/svxreflector_issuing_ca.key"

        # Écrire le CSR dans un fichier temporaire
        CSR_TMP=$(mktemp /tmp/svxlink-csr.XXXXXX.pem)
        trap "rm -f '${CSR_TMP}'" EXIT
        printf '%s' "${CA_CSR_PEM}" > "${CSR_TMP}"

        # Extraire le CN (callsign) du CSR
        CN=$(openssl req -in "${CSR_TMP}" -noout -subject -nameopt sep_multiline 2>/dev/null \
             | grep '^\s*CN=' | sed 's/^\s*CN=//')

        if [ -z "${CN}" ]; then
            echo "[dev-ca-hook] ERREUR: Impossible d'extraire le CN du CSR" >&2
            exit 0
        fi

        echo "[dev-ca-hook] Signature du certificat pour: ${CN}"

        CERT_OUT="${CERTS_DIR}/${CN}.crt"
        CSR_OUT="${CSRS_DIR}/${CN}.csr"

        # Signer le CSR avec l'Issuing CA (copie les extensions du CSR)
        openssl x509 -req \
            -in "${CSR_TMP}" \
            -CA "${CA_CRT}" \
            -CAkey "${CA_KEY}" \
            -CAcreateserial \
            -out "${CERT_OUT}" \
            -days 365 \
            -copy_extensions copy \
            2>/dev/null

        # Ajouter le certificat de l'Issuing CA pour la chaîne de vérification
        cat "${CA_CRT}" >> "${CERT_OUT}"

        # Copier le CSR vers csrs/ (pour enregistrement)
        cp "${CSR_TMP}" "${CSR_OUT}"

        echo "[dev-ca-hook] Certificat signé et enregistré: ${CERT_OUT}"
        ;;
    CSR_SIGNED)
        echo "[dev-ca-hook] Confirmation de signature: opération terminée"
        ;;
    CRT_RENEWED)
        echo "[dev-ca-hook] Renouvellement de certificat"
        ;;
    *)
        echo "[dev-ca-hook] Opération inconnue: ${CA_OP}"
        ;;
esac

exit 0
