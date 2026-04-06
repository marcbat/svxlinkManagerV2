###############################################################################
#
# Generic Logic event handlers — SvxlinkManagerV2
#
# Ce fichier surcharge uniquement proc startup {} pour jouer l'annonce du salon
# une seule fois au démarrage du daemon SVXLink (= au switch de salon).
# Il intercepte aussi les commandes DTMF pour permettre le changement de salon
# par radio.
#
# Compatible SVXLink 19.09.2.
# Déployé vers : /usr/share/svxlink/events.d/local/Logic.tcl
#
###############################################################################

namespace eval Logic {

#
# Executed when the SvxLink software is started.
# Plays the salon name WAV once if it exists (one-shot announcement).
#
proc startup {} {
    set name_wav "/usr/share/svxlink/sounds/fr_FR/svxlinkmanager/Name.wav"
    if {[file exists $name_wav] == 1} {
        puts "SvxlinkManagerV2: Name.wav found, playing salon announcement."
        playSilence 500
        playMsg "svxlinkmanager" "Name"
    }
}

#
# Executed when a complete DTMF command is received.
# Outputs the command on stdout for DtmfCommandTracker to parse.
# Returns 1 to indicate the command has been handled (prevents "unknown command").
#
# Info commands (300-399) — annonces vocales :
#   300 : Rejoue le nom du salon actuellement actif (Name.wav)
#   399 : Commande interne — joue /tmp/svxlink_tts.wav généré par .NET (pas de suppression, écrasé au prochain appel)
#
proc dtmf_cmd_received {cmd} {
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
    return 1
}

} ;# namespace eval Logic
