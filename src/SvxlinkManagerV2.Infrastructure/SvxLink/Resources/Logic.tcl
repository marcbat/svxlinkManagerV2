###############################################################################
#
# Generic Logic event handlers — SvxlinkManagerV2
#
# Ce fichier surcharge uniquement proc startup {} pour jouer l'annonce du salon
# une seule fois au démarrage du daemon SVXLink (= au switch de salon).
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

} ;# namespace eval Logic
