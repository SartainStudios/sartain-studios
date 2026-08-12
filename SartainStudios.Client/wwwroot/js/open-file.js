window.openPdfInNewTab = function (base64Data) {
    const bytes = Uint8Array.from(atob(base64Data), c => c.charCodeAt(0));
    const blob = new Blob([bytes], {type: 'application/pdf'});
    const url = URL.createObjectURL(blob);
    const opened = window.open(url, '_blank');
    if (!opened) {
        URL.revokeObjectURL(url);
        return false;
    }
    setTimeout(() => URL.revokeObjectURL(url), 60000);
    return true;
};