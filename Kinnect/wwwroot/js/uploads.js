export function uploadInput(url, input, fields, dotNetRef) {
    const file = input?.files?.[0];
    if (!file) {
        return Promise.reject(new Error("Choose a file first."));
    }

    return upload(url, file, fields, dotNetRef);
}

export function upload(url, file, fields, dotNetRef) {
    return new Promise((resolve, reject) => {
        const data = new FormData();
        data.append("file", file);
        for (const [key, value] of Object.entries(fields ?? {})) {
            if (value !== null && value !== undefined && value !== "") {
                data.append(key, value);
            }
        }

        const request = new XMLHttpRequest();
        request.open("POST", url);
        request.withCredentials = true;
        request.upload.onprogress = (event) => {
            if (event.lengthComputable && dotNetRef) {
                dotNetRef.invokeMethodAsync("OnUploadProgress", Math.round((event.loaded / event.total) * 100));
            }
        };
        request.onload = () => {
            if (request.status >= 200 && request.status < 300) {
                resolve(request.responseText);
            } else {
                reject(new Error(request.status + " " + request.responseText));
            }
        };
        request.onerror = () => reject(new Error("Upload failed."));
        request.send(data);
    });
}
