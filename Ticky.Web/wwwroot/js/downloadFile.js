function triggerFileDownload(fileName, url) {
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? 'file';
    anchorElement.click();
    anchorElement.remove();
}

function downloadJsonText(fileName, jsonText) {
    var anchorElement = document.createElement('a')
    anchorElement.href = "data:text/plain;charset=utf-8," + encodeURIComponent(jsonText.toString())
    anchorElement.download = fileName ?? 'file'
    anchorElement.click()
    anchorElement.remove()
}