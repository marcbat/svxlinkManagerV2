// Fonction pour afficher un toast Bootstrap
function showToast(toastId, durationMs) {
    var toastElement = document.getElementById(toastId);
    if (toastElement) {
        var bsToast = new bootstrap.Toast(toastElement);
        bsToast.show();
        
        // Auto-hide après la durée spécifiée
        if (durationMs > 0) {
            setTimeout(function() {
                bsToast.hide();
            }, durationMs);
        }
    }
}
