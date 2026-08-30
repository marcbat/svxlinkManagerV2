// Téléchargement d'un fichier généré côté serveur.
// Blazor Server n'a pas d'accès direct au système de fichiers du client : le contenu
// transite par le circuit en base64, est reconstitué en Blob puis proposé au navigateur.
window.fileDownload = {
    fromBase64: function (fileName, contentType, base64) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);

        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }

        const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
        const link = document.createElement('a');

        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        // L'URL d'objet reste allouée tant qu'elle n'est pas révoquée ; le délai laisse
        // au navigateur le temps d'amorcer le téléchargement avant sa libération.
        setTimeout(() => URL.revokeObjectURL(url), 10000);
    }
};
