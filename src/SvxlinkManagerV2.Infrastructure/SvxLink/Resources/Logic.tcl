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
        playMsg "svxlinkmanager" "Name"
    }
}

#
# Executed when a complete DTMF command is received.
# Outputs the command on stdout for DtmfCommandTracker to parse.
# Returns 1 to indicate the command has been handled (prevents "unknown command").
#
proc dtmf_cmd_received {cmd} {
    puts "DTMF_CMD:$cmd"
    return 1
}

} ;# namespace eval Logic
