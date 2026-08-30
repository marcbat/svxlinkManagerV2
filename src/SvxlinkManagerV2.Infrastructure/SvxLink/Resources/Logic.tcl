###############################################################################
#
# Generic Logic event handlers — SvxlinkManagerV2
#
# Ce fichier gère les annonces sonores et les commandes DTMF pour SVXLink.
# L'annonce de connexion au salon est déclenchée depuis .NET (via DTMF 398)
# une fois que la connexion réelle au réflecteur est confirmée dans les logs.
# Il intercepte aussi les commandes DTMF pour permettre le changement de salon
# par radio.
#
# Compatible SVXLink 19.09.2 et 25.05.
# Déployé vers : /usr/share/svxlink/events.d/local/Logic.tcl
#
# ── Plages DTMF (synchronisées avec DtmfCodeRanges.cs côté .NET) ───────────
#   1-19     : Modules SVXLink (Parrot, Help…) → return 0 (SVXLink traite)
#   20-299   : Codes salon → return 1 (.NET traite le changement de salon)
#   300-399  : Annonces vocales / commandes internes → return 1 (.NET traite)
#   400-9999 : Codes salon → return 1 (.NET traite le changement de salon)
#
###############################################################################

namespace eval Logic {

#
# Executed when the SvxLink software is started.
# L'annonce du salon n'est plus jouée ici — elle est déclenchée depuis .NET
# via la commande DTMF 398 une fois la connexion au réflecteur confirmée.
#
proc startup {} {
}

#
# Executed when a complete DTMF command is received.
# Route la commande selon la plage DTMF :
#   - 1-19   : émet DTMF_CMD + return 0 → SVXLink active le module correspondant
#   - 398    : joue Name.wav (annonce connexion réflecteur) + return 1
#   - 399    : joue le WAV TTS généré par .NET + return 1
#   - autres : émet DTMF_CMD + return 1 → .NET traite (salon switch ou annonce)
#
proc dtmf_cmd_received {cmd} {
    # --- Commande interne 398 : lecture de l'annonce de connexion réussie (Name.wav) ---
    if {$cmd eq "398"} {
        set name_wav "/usr/share/svxlink/sounds/fr_FR/svxlinkmanager/Name.wav"
        if {[file exists $name_wav] == 1} {
            playSilence 500
            playMsg "svxlinkmanager" "Name"
        }
        return 1
    }

    # --- Commande interne 399 : lecture du WAV TTS généré par .NET ---
    # Note: pas de file delete ici — playFile est asynchrone (queue audio) et le fichier
    # serait supprimé avant que SVXLink ait pu le lire. Le fichier sera écrasé au prochain appel TTS.
    if {$cmd eq "399"} {
        set tts_wav "/tmp/svxlink_tts.wav"
        if {[file exists $tts_wav] == 1} {
            playFile $tts_wav
        }
        return 1
    }

    # Toujours émettre la commande vers .NET pour logging et extensibilité
    puts "DTMF_CMD:$cmd"

    # --- Plage 1-19 : modules SVXLink (Parrot ID=2, Help ID=1, etc.) ---
    # return 0 → SVXLink continue le traitement normal (activation du module)
    if {[string is integer -strict $cmd]} {
        set code [expr {int($cmd)}]
        if {$code >= 1 && $code <= 19} {
            return 0
        }
    }

    # --- Plage 20-299, 300-399, 400-9999 : traité par .NET ---
    # return 1 → SVXLink ne traite pas davantage (pas de "unknown command")
    return 1
}

#
# Executed when the connection status to a reflector changes (SVXLink 25.05+)
#   status - 1 if connected, 0 if disconnected
#
proc reflector_connection_status_update {status} {
    if {$status == 1} {
        puts "REFLECTOR_CONNECTED"
    } else {
        puts "REFLECTOR_DISCONNECTED"
    }
}

} ;# namespace eval Logic
