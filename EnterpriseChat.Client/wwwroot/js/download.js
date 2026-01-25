export function downloadBytes(fileName, contentType, bytesBase64) {
    const byteCharacters = atob(bytesBase64);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) byteNumbers[i] = byteCharacters.charCodeAt(i);

    const blob = new Blob([new Uint8Array(byteNumbers)], { type: contentType });
    const url = URL.createObjectURL(blob);

    const a = document.createElement("a");
    a.href = url;
    a.download = fileName || "download";
    document.body.appendChild(a);
    a.click();
    a.remove();

    URL.revokeObjectURL(url);
}
