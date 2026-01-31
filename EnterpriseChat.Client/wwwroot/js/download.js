export async function downloadFileFromStream(fileName, contentType, data) {
    let arrayBuffer;

    // ?? ??? DotNetStreamReference
    if (data && typeof data.arrayBuffer === "function") {
        arrayBuffer = await data.arrayBuffer();
    }
    // ?? ??? Uint8Array / ArrayBuffer
    else if (data instanceof ArrayBuffer) {
        arrayBuffer = data;
    }
    else {
        // Blazor ?????? ????? Uint8Array
        const u8 = data instanceof Uint8Array ? data : new Uint8Array(data);
        // ???: ???? ????? ???? ?? ??? buffer (???? offsets)
        arrayBuffer = u8.buffer.slice(u8.byteOffset, u8.byteOffset + u8.byteLength);
    }

    const blob = new Blob([arrayBuffer], { type: contentType || "application/octet-stream" });

    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName || "download";
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
}
