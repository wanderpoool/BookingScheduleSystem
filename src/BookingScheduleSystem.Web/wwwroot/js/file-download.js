window.downloadFileFromBase64 = function (fileName, base64Data) {
    const link = document.createElement("a");
    link.href = "data:image/png;base64," + base64Data;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
