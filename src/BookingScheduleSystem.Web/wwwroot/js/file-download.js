window.downloadFileFromBase64 = function (fileName, base64Data) {
    const link = document.createElement("a");
    link.href = "data:image/png;base64," + base64Data;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.copyToClipboard = function (text) {
    if (navigator.clipboard && window.isSecureContext) {
        return navigator.clipboard.writeText(text).then(function () {
            return true;
        }).catch(function () {
            return fallbackCopy(text);
        });
    }
    return Promise.resolve(fallbackCopy(text));
};

function fallbackCopy(text) {
    var ta = document.createElement("textarea");
    ta.value = text;
    ta.style.position = "fixed";
    ta.style.left = "-9999px";
    document.body.appendChild(ta);
    ta.select();
    try {
        document.execCommand("copy");
        return true;
    } catch (e) {
        return false;
    } finally {
        document.body.removeChild(ta);
    }
}
